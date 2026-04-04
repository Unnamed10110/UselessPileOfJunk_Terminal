using System.Security.Principal;
using System.Text;

namespace UselessTerminal.Services;

public static class ElevationHelper
{
    public static bool IsProcessElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>Builds a command-line string suitable for <see cref="System.Diagnostics.ProcessStartInfo.Arguments"/>.</summary>
    public static string JoinCommandLineArgs(string[] args)
    {
        if (args.Length == 0) return "";

        var sb = new StringBuilder();
        foreach (var arg in args)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(EscapeArg(arg));
        }

        return sb.ToString();
    }

    private static string EscapeArg(string arg)
    {
        if (string.IsNullOrEmpty(arg)) return "\"\"";
        if (arg.AsSpan().IndexOfAny(" \t\"") < 0) return arg;
        return "\"" + arg.Replace("\"", "\\\"") + "\"";
    }
}
