using System.IO;
using System.Text.Json;
using UselessTerminal.Models;
using Path = System.IO.Path;
using File = System.IO.File;

namespace UselessTerminal.Services;

public sealed class SettingsStore
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "UselessTerminal", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static SettingsStore Instance { get; } = new();

    public AppSettings Current { get; private set; } = new();

    public event Action? SettingsChanged;

    private SettingsStore() { }

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                using var doc = JsonDocument.Parse(json);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new();
                AppSettingsLegacyMigration.ApplyIfNeeded(doc.RootElement, settings);
                NormalizeFontWeight(settings);
                NormalizeUiChrome(settings);
                NormalizeUiScale(settings);
                if (string.IsNullOrWhiteSpace(settings.ColorInput))
                    settings.ColorInput = settings.TextDefault;
                Current = settings;
            }
        }
        catch
        {
            Current = new();
        }
    }

    public void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(SettingsPath);
            if (dir is not null) Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(Current, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }

    public void Apply(AppSettings settings)
    {
        Current = settings;
        Save();
        SettingsChanged?.Invoke();
    }

    /// <summary>Updates shell font size from Ctrl+scroll (or similar); clamps 8–32 and persists.</summary>
    public void UpdateFontSize(int fontSize)
    {
        fontSize = Math.Clamp(fontSize, 8, 32);
        if (Current.FontSize == fontSize) return;
        Current.FontSize = fontSize;
        Save();
        SettingsChanged?.Invoke();
    }

    /// <summary>Updates WPF chrome scale from Ctrl+scroll; clamps 0.75–2.0 and persists.</summary>
    public void UpdateUiScale(double scale)
    {
        scale = SnapUiScale(scale);
        if (Math.Abs(Current.UiScale - scale) < 0.001) return;
        Current.UiScale = scale;
        Save();
        SettingsChanged?.Invoke();
    }

    /// <summary>Older settings.json files omit FontWeight (deserializes as 0).</summary>
    private static void NormalizeFontWeight(AppSettings s)
    {
        if (s.FontWeight < 100 || s.FontWeight > 900)
            s.FontWeight = 400;
    }

    private static void NormalizeUiChrome(AppSettings s)
    {
        if (string.IsNullOrWhiteSpace(s.UiFontFamily))
            s.UiFontFamily = "Segoe UI";
        if (s.UiFontSize < 10 || s.UiFontSize > 22)
            s.UiFontSize = 13;
        if (s.UiFontWeight < 300 || s.UiFontWeight > 700)
            s.UiFontWeight = 400;
        if (s.UiSharpness is not "Sharp" and not "Balanced" and not "Smooth")
            s.UiSharpness = "Sharp";
        static string HexOr(string? v, string d) =>
            string.IsNullOrWhiteSpace(v) ? d : v.Trim();
        s.UiForeground = HexOr(s.UiForeground, "#ffffff");
        s.UiForegroundMuted = HexOr(s.UiForegroundMuted, "#888888");
        s.UiAccent = HexOr(s.UiAccent, "#6be5ff");
        s.UiHighlight = HexOr(s.UiHighlight, "#c47cff");
        s.UiSuccess = HexOr(s.UiSuccess, "#b4fb00");
        s.UiWarning = HexOr(s.UiWarning, "#ffef5c");
        s.UiError = HexOr(s.UiError, "#ff2b7b");
        s.UiChromeBackground = HexOr(s.UiChromeBackground, "#000000");
        s.UiCardBackground = HexOr(s.UiCardBackground, "#0d1b1f");
        s.UiCardBorder = HexOr(s.UiCardBorder, "#1e4055");
        s.UiFolderSelectedBackground = HexOr(s.UiFolderSelectedBackground, "#18323a");
        s.UiTabForeground = HexOr(s.UiTabForeground, "#ffffff");
        s.UiTabSelectedBackground = HexOr(s.UiTabSelectedBackground, "#6be5ff");
        s.UiTabSelectedForeground = HexOr(s.UiTabSelectedForeground, "#111111");
        s.UiStatusBackground = HexOr(s.UiStatusBackground, "#11252a");
        s.UiIcon = HexOr(s.UiIcon, "#6be5ff");
        s.UiInputBackground = HexOr(s.UiInputBackground, "#121212");
        s.UiInputForeground = HexOr(s.UiInputForeground, "#ffffff");
        s.UiHoverBackground = HexOr(s.UiHoverBackground, "#13292e");
        s.UiSplitter = HexOr(s.UiSplitter, "#2e6773");
    }

    private static void NormalizeUiScale(AppSettings s)
    {
        if (s.UiScale < 0.75 || s.UiScale > 2.0 || double.IsNaN(s.UiScale))
            s.UiScale = 1.0;
        else
            s.UiScale = SnapUiScale(s.UiScale);
    }

    internal static double SnapUiScale(double scale)
    {
        scale = Math.Clamp(scale, 0.75, 2.0);
        return Math.Round(scale / 0.05) * 0.05;
    }
}
