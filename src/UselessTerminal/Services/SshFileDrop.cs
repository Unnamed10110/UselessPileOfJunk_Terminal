using System.Diagnostics;
using System.IO;
using System.Text;

namespace UselessTerminal.Services;

/// <summary>Copies Explorer-dropped files into a remote SSH working directory via scp.</summary>
internal static class SshFileDrop
{
    public static string FormatDestination(SshConnection ssh, string remoteDir)
        => $"{ssh.DisplayTarget}:{NormalizeRemoteDir(remoteDir)}";

    public static string NormalizeRemoteDir(string? remoteDir)
    {
        if (string.IsNullOrWhiteSpace(remoteDir)) return "~";
        remoteDir = remoteDir.Trim().Trim('"');
        if (remoteDir.Length >= 2 && remoteDir[1] == ':')
            return remoteDir.Replace('\\', '/');
        return remoteDir.Replace('\\', '/');
    }

    public static bool TryParseTitleCwd(string? title, out string cwd)
    {
        cwd = "";
        if (string.IsNullOrWhiteSpace(title)) return false;
        int colon = title.IndexOf(':');
        if (colon <= 0 || colon >= title.Length - 1) return false;
        string path = title[(colon + 1)..].Trim();
        if (path.Length == 0) return false;
        if (path.StartsWith('~') || path.StartsWith('/') || path.StartsWith('.'))
        {
            cwd = path;
            return true;
        }
        if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':')
        {
            cwd = path;
            return true;
        }
        return false;
    }

    public static List<string> FindNameConflicts(IReadOnlyList<string> sources, SshConnection ssh, string remoteDir)
    {
        remoteDir = NormalizeRemoteDir(remoteDir);
        var list = new List<string>();
        foreach (string src in sources)
        {
            string label = ShellFileDrop.ItemName(src);
            if (string.IsNullOrEmpty(label)) continue;
            if (RemoteExists(ssh, JoinRemote(remoteDir, label)) == true)
                list.Add(label);
        }

        return list;
    }

    public static ShellFileDrop.CopyResult CopyDroppedPaths(
        IReadOnlyList<string> sources,
        SshConnection ssh,
        string remoteDir,
        FileConflictChoice conflict = FileConflictChoice.Rename,
        Action<string>? progress = null)
    {
        var result = new ShellFileDrop.CopyResult();
        string? scp = ssh.ScpExe;
        if (string.IsNullOrEmpty(scp) || !File.Exists(scp))
        {
            result.Errors.Add("scp.exe was not found next to ssh. Install OpenSSH or Git for Windows.");
            return result;
        }

        remoteDir = NormalizeRemoteDir(remoteDir);
        int total = sources.Count;
        int i = 0;
        foreach (string src in sources)
        {
            i++;
            string label = ShellFileDrop.ItemName(src);
            progress?.Invoke(total <= 1 ? $"Copying {label}…" : $"Copying {i} of {total}…\n{label}");
            try
            {
                string destName = conflict == FileConflictChoice.Replace
                    ? label
                    : UniqueRemoteName(ssh, remoteDir, label);
                string remotePath = JoinRemote(remoteDir, destName);
                bool directory = Directory.Exists(src);
                if (!directory && !File.Exists(src))
                    throw new FileNotFoundException("Path not found.", src);

                if (conflict == FileConflictChoice.Replace && destName == label)
                    RemoveRemote(ssh, remotePath);

                var args = new List<string>();
                if (directory) args.Add("-r");
                ssh.AddConnectionArguments(args, scp: true);
                args.Add(src);
                args.Add(ssh.RemoteTarget(remotePath));

                var run = Run(scp, args, TimeSpan.FromHours(2));
                if (run.ExitCode != 0)
                    throw new InvalidOperationException(FormatScpError(run.StdErr, run.StdOut, run.ExitCode));

                result.CopiedNames.Add(destName);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{label}: {ex.Message}");
            }
        }

        return result;
    }

    private static string UniqueRemoteName(SshConnection ssh, string remoteDir, string fileName)
    {
        if (RemoteExists(ssh, JoinRemote(remoteDir, fileName)) != true)
            return fileName;

        string stem = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        for (int n = 1; n < 1000; n++)
        {
            string candidate = $"{stem} ({n}){ext}";
            if (RemoteExists(ssh, JoinRemote(remoteDir, candidate)) != true)
                return candidate;
        }

        return $"{stem} ({Guid.NewGuid():N}){ext}";
    }

    private static bool? RemoteExists(SshConnection ssh, string remotePath)
    {
        if (string.IsNullOrEmpty(ssh.SshExe)) return null;

        string quoted = QuoteSh(remotePath);
        var args = new List<string>();
        ssh.AddConnectionArguments(args, scp: false);
        args.Add(ssh.DisplayTarget);
        args.Add($"test -e {quoted} && echo UT_EXISTS || echo UT_MISSING");

        try
        {
            var run = Run(ssh.SshExe, args, TimeSpan.FromSeconds(20));
            if (run.StdOut.Contains("UT_EXISTS", StringComparison.Ordinal)) return true;
            if (run.StdOut.Contains("UT_MISSING", StringComparison.Ordinal)) return false;
        }
        catch { }

        return null;
    }

    private static void RemoveRemote(SshConnection ssh, string remotePath)
    {
        string normalized = NormalizeRemoteDir(remotePath).TrimEnd('/');
        if (normalized is "" or "." or "/" or "~" or "~/")
            throw new InvalidOperationException("Refusing to replace the remote home or root directory.");

        string quoted = QuoteSh(normalized);
        var args = new List<string>();
        ssh.AddConnectionArguments(args, scp: false);
        args.Add(ssh.DisplayTarget);
        args.Add($"rm -rf {quoted}");
        var run = Run(ssh.SshExe, args, TimeSpan.FromMinutes(2));
        if (run.ExitCode != 0)
        {
            string err = (run.StdErr + " " + run.StdOut).Trim();
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(err) ? "Could not replace the existing remote item." : err);
        }
    }

    private static string JoinRemote(string dir, string name)
    {
        dir = NormalizeRemoteDir(dir).TrimEnd('/');
        name = name.Replace('\\', '/');
        if (dir.Length == 0) dir = "~";
        return dir + "/" + name;
    }

    private static string QuoteSh(string path)
        => "'" + path.Replace("'", "'\"'\"'") + "'";

    private static string FormatScpError(string stderr, string stdout, int exit)
    {
        string text = (stderr + "\n" + stdout).Trim();
        if (text.Contains("Permission denied", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Host key verification failed", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Connection refused", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Connection timed out", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Could not resolve", StringComparison.OrdinalIgnoreCase))
        {
            return text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()
                   ?? "SSH copy failed.";
        }

        if (string.IsNullOrWhiteSpace(text))
            return $"scp exited {exit}. For password logins, open SSH as its own tab so the drop can reuse that connection.";

        string first = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).First();
        if (first.Contains("Permission denied", StringComparison.OrdinalIgnoreCase)
            || first.Contains("password", StringComparison.OrdinalIgnoreCase))
        {
            return first + " Open SSH as its own tab (Quick Connect or a saved session) so drops reuse the login.";
        }

        return first;
    }

    private readonly record struct RunResult(int ExitCode, string StdOut, string StdErr);

    private static RunResult Run(string fileName, List<string> args, TimeSpan timeout)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string a in args)
            psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {Path.GetFileName(fileName)}.");
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        if (!proc.WaitForExit((int)Math.Min(timeout.TotalMilliseconds, int.MaxValue)))
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"{Path.GetFileName(fileName)} timed out.");
        }

        return new RunResult(proc.ExitCode, stdoutTask.GetAwaiter().GetResult(), stderrTask.GetAwaiter().GetResult());
    }
}
