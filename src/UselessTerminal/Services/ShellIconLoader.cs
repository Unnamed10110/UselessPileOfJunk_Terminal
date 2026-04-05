using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UselessTerminal.Services;

/// <summary>
/// Loads the embedded icon from each shell executable via <see cref="System.Drawing.Icon.ExtractAssociatedIcon"/>.
/// </summary>
public static class ShellIconLoader
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static ImageSource? _fallback;

    public static ImageSource FallbackIcon
    {
        get
        {
            if (_fallback is not null) return _fallback;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute);
            bmp.DecodePixelWidth = 32;
            bmp.EndInit();
            bmp.Freeze();
            _fallback = bmp;
            return _fallback;
        }
    }

    public static ImageSource GetIconForCommand(string command)
    {
        string token = ShellGlyphResolver.ParseExecutable(command);
        if (string.IsNullOrEmpty(token))
            return FallbackIcon;
        return GetIconForPath(token);
    }

    public static ImageSource GetIconForPath(string shellPath)
    {
        if (string.IsNullOrWhiteSpace(shellPath))
            return FallbackIcon;

        string trimmed = shellPath.Trim().Trim('"');
        string? resolved = ResolveExecutableToPath(trimmed);
        if (resolved is null)
            return FallbackIcon;

        lock (Sync)
        {
            if (Cache.TryGetValue(resolved, out var hit))
                return hit;
        }

        ImageSource? created = TryExtractIcon(resolved);
        ImageSource use = created ?? FallbackIcon;
        lock (Sync)
        {
            if (!Cache.ContainsKey(resolved))
                Cache[resolved] = use;
        }

        return use;
    }

    private static ImageSource? TryExtractIcon(string resolvedPath)
    {
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(resolvedPath);
            if (icon is null)
                return null;
            var src = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Resolves a shell path or bare filename to an existing .exe (PATH, System32, etc.).</summary>
    public static string? ResolveExecutableToPath(string shellPath)
    {
        if (string.IsNullOrWhiteSpace(shellPath))
            return null;

        shellPath = shellPath.Trim().Trim('"');
        if (File.Exists(shellPath))
            return Path.GetFullPath(shellPath);

        string file = Path.GetFileName(shellPath);
        if (string.IsNullOrEmpty(file))
            return null;

        string inCwd = Path.Combine(Directory.GetCurrentDirectory(), file);
        if (File.Exists(inCwd))
            return Path.GetFullPath(inCwd);

        string sys = Path.Combine(Environment.SystemDirectory, file);
        if (File.Exists(sys))
            return sys;

        if (file.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase))
        {
            string winPs = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(winPs))
                return winPs;
        }

        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv is null)
            return null;

        foreach (string dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(dir.Trim(), file);
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }
            catch
            {
                // invalid path segment
            }
        }

        return null;
    }
}
