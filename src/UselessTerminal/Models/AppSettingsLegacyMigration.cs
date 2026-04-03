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
        SetIfMissing(nameof(AppSettings.CursorColor), "Cursor");
        SetIfMissing(nameof(AppSettings.TextMuted), "BrightBlack");

        SetIfMissing(nameof(AppSettings.ColorError), "Red", "BrightRed");
        SetIfMissing(nameof(AppSettings.ColorWarning), "Yellow", "BrightYellow");
        SetIfMissing(nameof(AppSettings.ColorCommand), "Green", "BrightGreen");
        SetIfMissing(nameof(AppSettings.ColorMessage), "Cyan", "BrightCyan");
        SetIfMissing(nameof(AppSettings.ColorAccent), "Blue", "BrightBlue");
        SetIfMissing(nameof(AppSettings.ColorHighlight), "Magenta", "BrightMagenta");
    }

    private static void Apply(string newKey, AppSettings s, string v)
    {
        switch (newKey)
        {
            case nameof(AppSettings.TerminalBackground): s.TerminalBackground = v; break;
            case nameof(AppSettings.TextDefault): s.TextDefault = v; break;
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
