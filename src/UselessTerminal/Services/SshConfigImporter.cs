using System.IO;
using UselessTerminal.Models;

namespace UselessTerminal.Services;

public static class SshConfigImporter
{
    public static List<SavedSession> Import()
    {
        var sessions = new List<SavedSession>();
        string configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ssh", "config");

        if (!File.Exists(configPath))
            return sessions;

        try
        {
            var lines = File.ReadAllLines(configPath);
            string? currentHost = null;
            string? hostname = null;
            string? user = null;
            string? port = null;
            string? identityFile = null;

            void Flush()
            {
                if (currentHost is null || currentHost.Contains('*') || currentHost.Contains('?'))
                    return;

                string target = hostname ?? currentHost;
                string cmd = user is not null ? $"{user}@{target}" : target;
                if (port is not null) cmd = $"-p {port} {cmd}";
                if (identityFile is not null) cmd = $"-i {identityFile} {cmd}";

                string sshExe = FindSshExe();
                sessions.Add(new SavedSession
                {
                    Name = $"[SSH] {currentHost}",
                    ShellPath = sshExe,
                    Arguments = cmd,
                    Description = $"SSH to {target}",
                    ColorTag = "#6be5ff",
                });
            }

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.StartsWith('#') || string.IsNullOrEmpty(line))
                    continue;

                int space = line.IndexOf(' ');
                if (space <= 0) continue;
                string key = line[..space].Trim();
                string val = line[(space + 1)..].Trim();

                if (key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    Flush();
                    currentHost = val;
                    hostname = null;
                    user = null;
                    port = null;
                    identityFile = null;
                }
                else if (key.Equals("HostName", StringComparison.OrdinalIgnoreCase))
                    hostname = val;
                else if (key.Equals("User", StringComparison.OrdinalIgnoreCase))
                    user = val;
                else if (key.Equals("Port", StringComparison.OrdinalIgnoreCase))
                    port = val;
                else if (key.Equals("IdentityFile", StringComparison.OrdinalIgnoreCase))
                    identityFile = val;
            }

            Flush();
        }
        catch { }

        return sessions;
    }

    private static string FindSshExe()
    {
        string system32Ssh = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "OpenSSH", "ssh.exe");
        if (File.Exists(system32Ssh)) return system32Ssh;

        string progFiles = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Git", "usr", "bin", "ssh.exe");
        if (File.Exists(progFiles)) return progFiles;

        return "ssh";
    }
}
