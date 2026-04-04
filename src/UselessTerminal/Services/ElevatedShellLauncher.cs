using System.Diagnostics;
using UselessTerminal.Models;

namespace UselessTerminal.Services;

/// <summary>
/// Starts the saved shell elevated in a separate OS console (UAC).
/// Embedded ConPTY cannot host an elevated child when this process is not elevated.
/// </summary>
public static class ElevatedShellLauncher
{
    public static void Launch(SavedSession session)
    {
        var psi = new ProcessStartInfo
        {
            FileName = session.ShellPath,
            Arguments = session.Arguments,
            UseShellExecute = true,
            Verb = "runas",
        };

        if (!string.IsNullOrWhiteSpace(session.WorkingDirectory))
            psi.WorkingDirectory = session.WorkingDirectory;

        Process.Start(psi);
    }
}
