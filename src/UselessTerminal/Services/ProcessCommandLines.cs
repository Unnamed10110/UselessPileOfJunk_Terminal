using System.Runtime.InteropServices;
using System.Text;

namespace UselessTerminal.Services;

/// <summary>Reads command lines of a process and its descendants (for nested ssh detection).</summary>
internal static class ProcessCommandLines
{
    private const int Th32csSnapProcess = 2;
    private const int ProcessQueryLimitedInformation = 0x1000;
    private const int ProcessCommandLineInformation = 60;
    private const int MaxPath = 32768;

    public readonly record struct ProcessInfo(int Pid, int ParentPid, string ExeName, string? ExePath, string? CommandLine);

    public static List<ProcessInfo> GetDescendants(int rootPid)
    {
        var result = new List<ProcessInfo>();
        if (rootPid <= 0) return result;

        var all = SnapshotProcesses();
        var children = new Dictionary<int, List<int>>();
        foreach (var p in all.Values)
        {
            if (!children.TryGetValue(p.ParentPid, out var list))
            {
                list = [];
                children[p.ParentPid] = list;
            }
            list.Add(p.Pid);
        }

        var stack = new Stack<int>();
        stack.Push(rootPid);
        var seen = new HashSet<int>();
        while (stack.Count > 0)
        {
            int pid = stack.Pop();
            if (!seen.Add(pid)) continue;
            if (all.TryGetValue(pid, out var info))
                result.Add(Enrich(info));
            if (children.TryGetValue(pid, out var kids))
            {
                foreach (int child in kids)
                    stack.Push(child);
            }
        }

        return result;
    }

    private static ProcessInfo Enrich(ProcessInfo info)
    {
        string? path = QueryImagePath(info.Pid) ?? info.ExeName;
        string? cmd = QueryCommandLine(info.Pid);
        return info with { ExePath = path, CommandLine = cmd };
    }

    private static Dictionary<int, ProcessInfo> SnapshotProcesses()
    {
        var map = new Dictionary<int, ProcessInfo>();
        nint snap = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
        if (snap == nint.Zero || snap == (nint)(-1)) return map;

        try
        {
            var entry = new ProcessEntry32W { dwSize = Marshal.SizeOf<ProcessEntry32W>() };
            if (!Process32FirstW(snap, ref entry)) return map;
            do
            {
                map[entry.th32ProcessID] = new ProcessInfo(
                    entry.th32ProcessID,
                    entry.th32ParentProcessID,
                    entry.szExeFile ?? "",
                    null,
                    null);
            } while (Process32NextW(snap, ref entry));
        }
        finally
        {
            CloseHandle(snap);
        }

        return map;
    }

    private static string? QueryCommandLine(int pid)
    {
        nint handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle == nint.Zero) return null;
        nint buffer = nint.Zero;
        try
        {
            NtQueryInformationProcess(handle, ProcessCommandLineInformation, nint.Zero, 0, out int length);
            if (length <= 0) return null;
            buffer = Marshal.AllocHGlobal(length);
            int status = NtQueryInformationProcess(handle, ProcessCommandLineInformation, buffer, length, out _);
            if (status != 0) return null;

            short byteLen = Marshal.ReadInt16(buffer);
            nint strPtr = Marshal.ReadIntPtr(buffer, nint.Size);
            if (strPtr == nint.Zero || byteLen <= 0) return null;
            return Marshal.PtrToStringUni(strPtr, byteLen / 2);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (buffer != nint.Zero) Marshal.FreeHGlobal(buffer);
            CloseHandle(handle);
        }
    }

    private static string? QueryImagePath(int pid)
    {
        nint handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle == nint.Zero) return null;
        try
        {
            var sb = new StringBuilder(MaxPath);
            int size = sb.Capacity;
            if (!QueryFullProcessImageName(handle, 0, sb, ref size)) return null;
            return sb.ToString();
        }
        catch
        {
            return null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32W
    {
        public int dwSize;
        public int cntUsage;
        public int th32ProcessID;
        public nint th32DefaultHeapID;
        public int th32ModuleID;
        public int cntThreads;
        public int th32ParentProcessID;
        public int pcPriClassBase;
        public int dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateToolhelp32Snapshot(int dwFlags, int th32ProcessID);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32FirstW(nint hSnapshot, ref ProcessEntry32W lppe);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32NextW(nint hSnapshot, ref ProcessEntry32W lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        nint processHandle,
        int processInformationClass,
        nint processInformation,
        int processInformationLength,
        out int returnLength);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(
        nint hProcess,
        int dwFlags,
        StringBuilder lpExeName,
        ref int lpdwSize);
}
