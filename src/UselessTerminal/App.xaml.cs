using System.Diagnostics;
using System.Windows;
using UselessTerminal.Services;

namespace UselessTerminal;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (!ElevationHelper.IsProcessElevated())
        {
            try
            {
                string? path = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(path))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = path,
                        Arguments = ElevationHelper.JoinCommandLineArgs(e.Args),
                        UseShellExecute = true,
                        Verb = "runas",
                    };

                    if (Process.Start(psi) is not null)
                    {
                        Shutdown();
                        return;
                    }
                }
            }
            catch
            {
                // UAC declined or elevation unavailable — continue without admin.
            }
        }

        base.OnStartup(e);
    }
}
