using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using UselessTerminal.Interop;
using static UselessTerminal.Interop.NativeMethods;

namespace UselessTerminal.Services;

public sealed class ConPtySession : IDisposable
{
    private nint _pseudoConsoleHandle;
    private nint _processHandle;
    private nint _threadHandle;
    private nint _attributeList;
    private FileStream? _inputStream;
    private FileStream? _outputStream;
    private CancellationTokenSource? _cts;
    private Task? _readTask;
    private bool _disposed;
    private Dictionary<string, string>? _extraEnv;

    public event Action<byte[]>? OutputReceived;
    public event Action? ProcessExited;
    public int ProcessId { get; private set; }

    public bool IsProcessAlive
    {
        get
        {
            if (_disposed || _processHandle == 0) return false;
            return NativeMethods.GetExitCodeProcess(_processHandle, out uint code) && code == NativeMethods.STILL_ACTIVE;
        }
    }

    public void Start(string command, string? workingDirectory, short cols, short rows, Dictionary<string, string>? extraEnv = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _extraEnv = extraEnv;

        var size = new COORD(cols, rows);

        var secAttr = new SECURITY_ATTRIBUTES
        {
            nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
            bInheritHandle = 1
        };

        // Pipes: ptyIn feeds INTO the pseudo console, ptyOut reads FROM it
        if (!CreatePipe(out var ptyInRead, out var ptyInWrite, ref secAttr, 0))
            throw new InvalidOperationException($"CreatePipe(in) failed: {Marshal.GetLastWin32Error()}");

        if (!CreatePipe(out var ptyOutRead, out var ptyOutWrite, ref secAttr, 0))
            throw new InvalidOperationException($"CreatePipe(out) failed: {Marshal.GetLastWin32Error()}");

        int hr = CreatePseudoConsole(size, ptyInRead, ptyOutWrite, 0, out _pseudoConsoleHandle);
        if (hr != 0)
            throw new InvalidOperationException($"CreatePseudoConsole failed: 0x{hr:X8}");

        // These ends are now owned by the pseudo console
        ptyInRead.Dispose();
        ptyOutWrite.Dispose();

        _inputStream = new FileStream(ptyInWrite, FileAccess.Write, bufferSize: 1);
        _outputStream = new FileStream(ptyOutRead, FileAccess.Read, bufferSize: 4096);

        StartProcess(command, workingDirectory);
        StartReading();
    }

    private void StartProcess(string command, string? workingDirectory)
    {
        nint listSize = 0;
        InitializeProcThreadAttributeList(0, 1, 0, ref listSize);

        _attributeList = Marshal.AllocHGlobal(listSize);
        if (!InitializeProcThreadAttributeList(_attributeList, 1, 0, ref listSize))
            throw new InvalidOperationException($"InitializeProcThreadAttributeList failed: {Marshal.GetLastWin32Error()}");

        if (!UpdateProcThreadAttribute(
                _attributeList, 0,
                (nint)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                _pseudoConsoleHandle,
                nint.Size, 0, 0))
            throw new InvalidOperationException($"UpdateProcThreadAttribute failed: {Marshal.GetLastWin32Error()}");

        var startupInfo = new STARTUPINFOEX
        {
            StartupInfo = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFOEX>() },
            lpAttributeList = _attributeList
        };

        nint envBlock = 0;
        if (_extraEnv is { Count: > 0 })
        {
            envBlock = BuildEnvironmentBlock(_extraEnv);
        }

        if (!CreateProcessW(
                null, command, 0, 0, false,
                EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT,
                envBlock, workingDirectory, ref startupInfo, out var processInfo))
        {
            if (envBlock != 0) Marshal.FreeHGlobal(envBlock);
            throw new InvalidOperationException($"CreateProcess failed: {Marshal.GetLastWin32Error()}");
        }

        if (envBlock != 0) Marshal.FreeHGlobal(envBlock);

        _processHandle = processInfo.hProcess;
        _threadHandle = processInfo.hThread;
        ProcessId = processInfo.dwProcessId;
    }

    private void StartReading()
    {
        _cts = new CancellationTokenSource();

        _readTask = Task.Factory.StartNew(() =>
        {
            var buffer = new byte[4096];
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    int bytesRead = _outputStream!.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                        break;

                    var data = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, data, 0, bytesRead);
                    OutputReceived?.Invoke(data);
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            finally
            {
                ProcessExited?.Invoke();
            }
        }, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    public void WriteInput(byte[] data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_inputStream is null) return;

        _inputStream.Write(data, 0, data.Length);
        _inputStream.Flush();
    }

    public void WriteInput(string text)
    {
        WriteInput(System.Text.Encoding.UTF8.GetBytes(text));
    }

    private static nint BuildEnvironmentBlock(Dictionary<string, string> extra)
    {
        var env = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry e in Environment.GetEnvironmentVariables())
            env[e.Key?.ToString() ?? ""] = e.Value?.ToString() ?? "";
        foreach (var kv in extra)
            env[kv.Key] = kv.Value;

        var sb = new System.Text.StringBuilder();
        foreach (var kv in env)
        {
            sb.Append(kv.Key);
            sb.Append('=');
            sb.Append(kv.Value);
            sb.Append('\0');
        }
        sb.Append('\0');

        string block = sb.ToString();
        nint ptr = Marshal.StringToHGlobalUni(block);
        return ptr;
    }

    public void Resize(short cols, short rows)
    {
        if (_pseudoConsoleHandle != 0)
            ResizePseudoConsole(_pseudoConsoleHandle, new COORD(cols, rows));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();

        if (_pseudoConsoleHandle != 0)
        {
            ClosePseudoConsole(_pseudoConsoleHandle);
            _pseudoConsoleHandle = 0;
        }

        _inputStream?.Dispose();
        _outputStream?.Dispose();

        if (_threadHandle != 0) { CloseHandle(_threadHandle); _threadHandle = 0; }
        if (_processHandle != 0) { CloseHandle(_processHandle); _processHandle = 0; }

        if (_attributeList != 0)
        {
            DeleteProcThreadAttributeList(_attributeList);
            Marshal.FreeHGlobal(_attributeList);
            _attributeList = 0;
        }

        try { _readTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts?.Dispose();
    }
}
