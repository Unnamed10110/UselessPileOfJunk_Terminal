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

    public event Action<byte[]>? OutputReceived;
    public event Action? ProcessExited;
    public int ProcessId { get; private set; }

    public void Start(string command, string? workingDirectory, short cols, short rows)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

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

        if (!CreateProcessW(
                null, command, 0, 0, false,
                EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT,
                0, workingDirectory, ref startupInfo, out var processInfo))
            throw new InvalidOperationException($"CreateProcess failed: {Marshal.GetLastWin32Error()}");

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
