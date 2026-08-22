using System.Text.Json;

namespace UselessTerminal.Models;

/// <summary>
/// Maps legacy settings.json (raw ANSI color names) onto semantic <see cref="AppSettings"/> fields
/// when the new property keys are absent.
/// </summary>
public static class AppSettingsLegacyMigration
{
    public static void ApplyIfNeeded(JsonElement root, AppSettings s)
    {
        bool HasNew(string name) =>
            root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String;

        void SetIfMissing(string newKey, string legacyPrimary, string? legacyFallback = null)
        {
            if (HasNew(newKey)) return;
            if (TryStr(root, legacyPrimary, out var v)) { Apply(newKey, s, v); return; }
            if (legacyFallback is not null && TryStr(root, legacyFallback, out v))
                Apply(newKey, s, v);
        }

        SetIfMissing(nameof(AppSettings.TerminalBackground), "Background");
        SetIfMissing(nameof(AppSettings.TextDefault), "Foreground", "White");
        SetIfMissing(nameof(AppSettings.ColorInput), "Foreground", "White");
        SetIfMissing(nameof(AppSettings.CursorColor), "Cursor");
        SetIfMissing(nameof(AppSettings.TextMuted), "BrightBlack");

        SetIfMissing(nameof(AppSettings.ColorError), "Red", "BrightRed");
        SetIfMissing(nameof(AppSettings.ColorWarning), "Yellow", "BrightYellow");
        SetIfMissing(nameof(AppSettings.ColorCommand), "Green", "BrightGreen");
        SetIfMissing(nameof(AppSettings.ColorMessage), "Cyan", "BrightCyan");
        SetIfMissing(nameof(AppSettings.ColorAccent), "Blue", "BrightBlue");
        SetIfMissing(nameof(AppSettings.ColorHighlight), "Magenta", "BrightMagenta");

        if (!HasNew(nameof(AppSettings.UiHighlight)))
            s.UiHighlight = s.ColorHighlight;
        if (!HasNew(nameof(AppSettings.UiSuccess)))
            s.UiSuccess = s.ColorCommand;
        if (!HasNew(nameof(AppSettings.UiWarning)))
            s.UiWarning = s.ColorWarning;
        if (!HasNew(nameof(AppSettings.UiError)))
            s.UiError = s.ColorError;
        if (!HasNew(nameof(AppSettings.UiIcon)))
            s.UiIcon = string.IsNullOrWhiteSpace(s.UiAccent) ? s.ColorAccent : s.UiAccent;
        if (!HasNew(nameof(AppSettings.UiTabSelectedBackground)))
            s.UiTabSelectedBackground = string.IsNullOrWhiteSpace(s.UiAccent) ? s.ColorAccent : s.UiAccent;
        if (!HasNew(nameof(AppSettings.UiTabSelectedForeground)))
            s.UiTabSelectedForeground = ThemePresets.ContrastFg(s.UiTabSelectedBackground);
        if (!HasNew(nameof(AppSettings.UiInputBackground)))
            s.UiInputBackground = s.UiCardBackground;
        if (!HasNew(nameof(AppSettings.UiInputForeground)))
            s.UiInputForeground = s.UiForeground;
        if (!HasNew(nameof(AppSettings.UiHoverBackground)))
            s.UiHoverBackground = s.UiFolderSelectedBackground;
        if (!HasNew(nameof(AppSettings.UiSplitter)))
            s.UiSplitter = string.IsNullOrWhiteSpace(s.UiAccent) ? s.ColorAccent : s.UiAccent;
    }

    private static void Apply(string newKey, AppSettings s, string v)
    {
        switch (newKey)
        {
            case nameof(AppSettings.TerminalBackground): s.TerminalBackground = v; break;
            case nameof(AppSettings.TextDefault): s.TextDefault = v; break;
            case nameof(AppSettings.ColorInput): s.ColorInput = v; break;
            case nameof(AppSettings.CursorColor): s.CursorColor = v; break;
            case nameof(AppSettings.TextMuted): s.TextMuted = v; break;
            case nameof(AppSettings.ColorError): s.ColorError = v; break;
            case nameof(AppSettings.ColorWarning): s.ColorWarning = v; break;
            case nameof(AppSettings.ColorCommand): s.ColorCommand = v; break;
            case nameof(AppSettings.ColorMessage): s.ColorMessage = v; break;
            case nameof(AppSettings.ColorAccent): s.ColorAccent = v; break;
            case nameof(AppSettings.ColorHighlight): s.ColorHighlight = v; break;
        }
    }

    private static bool TryStr(JsonElement root, string name, out string value)
    {
        value = "";
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
            return false;
        string? v = el.GetString();
        if (string.IsNullOrWhiteSpace(v)) return false;
        value = v;
        return true;
    }
}
