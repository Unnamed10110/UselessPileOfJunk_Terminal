using System.IO;
using UselessTerminal.Models;
using File = System.IO.File;
using Path = System.IO.Path;

namespace UselessTerminal.Services;

public static class ShellDetector
{
    public static string GetDefaultShell()
    {
        var profiles = DetectShells();
        var defaultProfile = profiles.FirstOrDefault(p => p.IsDefault) ?? profiles.First();
        string quoted = defaultProfile.Command.Contains(' ') && !defaultProfile.Command.StartsWith('"')
            ? $"\"{defaultProfile.Command}\""
            : defaultProfile.Command;
        return string.IsNullOrEmpty(defaultProfile.Arguments)
            ? quoted
            : $"{quoted} {defaultProfile.Arguments}";
    }

    public static List<ShellProfile> DetectShells()
    {
        var shells = new List<ShellProfile>();

        string? pwsh = FindExecutable("pwsh.exe");
        if (pwsh is not null)
            shells.Add(new ShellProfile { Name = "PowerShell", Command = pwsh, IconGlyph = "", Color = "#00e5ff", IsDefault = true });

        string winPs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        if (File.Exists(winPs))
            shells.Add(new ShellProfile { Name = "Windows PowerShell", Command = winPs, IconGlyph = "", Color = "#00e5ff", IsDefault = pwsh is null });

        string cmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        if (File.Exists(cmd))
            shells.Add(new ShellProfile { Name = "Command Prompt", Command = cmd, IconGlyph = "", Color = "#ffff00" });

        string wsl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsl.exe");
        if (File.Exists(wsl))
        {
            shells.Add(new ShellProfile { Name = "WSL", Command = wsl, IconGlyph = "", Color = "#ff8800" });
            foreach (var distro in GetWslDistros())
                shells.Add(new ShellProfile { Name = $"WSL: {distro}", Command = wsl, Arguments = $"-d {distro}", IconGlyph = "", Color = "#ff8800" });
        }

        string? gitBash = FindGitBash();
        if (gitBash is not null)
            shells.Add(new ShellProfile { Name = "Git Bash", Command = gitBash, Arguments = "--login -i", IconGlyph = "", Color = "#ff003c" });

        if (shells.Count == 0)
            shells.Add(new ShellProfile { Name = "Command Prompt", Command = "cmd.exe", IconGlyph = "", Color = "#ffff00", IsDefault = true });

        return shells;
    }

    private static string? FindExecutable(string name)
    {
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv is null) return null;

        foreach (string dir in pathEnv.Split(Path.PathSeparator))
        {
            string fullPath = Path.Combine(dir.Trim(), name);
            if (File.Exists(fullPath)) return fullPath;
        }
        return null;
    }

    private static string? FindGitBash()
    {
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "bash.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "bin", "bash.exe"),
            @"C:\Program Files\Git\bin\bash.exe"
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    private static List<string> GetWslDistros()
    {
        var distros = new List<string>();
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("wsl.exe", "--list --quiet")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) return distros;

            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);

            foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string clean = line.Replace("\0", "").Trim();
                if (!string.IsNullOrEmpty(clean))
                    distros.Add(clean);
            }
        }
        catch { }
        return distros;
    }
}
