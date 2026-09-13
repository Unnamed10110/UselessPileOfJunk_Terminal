using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using UselessTerminal.Models;

namespace UselessTerminal.Services;

/// <summary>Pushes interface (chrome) settings into application resources and window text options.</summary>
public static class UiChrome
{
    public static void Apply(AppSettings s)
    {
        var app = Application.Current;
        if (app is null) return;

        bool light = ThemePresets.IsLight(s.UiChromeBackground);
        var wpfTheme = light ? ApplicationTheme.Light : ApplicationTheme.Dark;
        try
        {
            ApplicationThemeManager.Apply(wpfTheme);
            ApplicationAccentColorManager.Apply(ParseColor(s.UiAccent, "#6be5ff"), wpfTheme);
        }
        catch
        {
            // WPF-UI theme switch is best-effort; our Ui.* resources still apply below.
        }

        double scale = SettingsStore.SnapUiScale(s.UiScale);
        int size = Math.Clamp(s.UiFontSize, 10, 22);
        int weight = Math.Clamp(s.UiFontWeight, 300, 700);
        string family = string.IsNullOrWhiteSpace(s.UiFontFamily) ? "Segoe UI" : s.UiFontFamily.Trim();

        double body = Math.Max(8, Math.Round(size * scale));
        double small = Math.Max(8, Math.Round((size - 2) * scale));
        double title = Math.Max(10, Math.Round((size + 2) * scale));
        double tab = Math.Max(9, Math.Round((size - 3) * scale));
        double icon = Math.Max(10, Math.Round(14 * scale));

        var r = app.Resources;
        r["Ui.FontFamily"] = new FontFamily(family);
        r["Ui.FontSize"] = body;
        r["Ui.FontSizeSmall"] = small;
        r["Ui.FontSizeTitle"] = title;
        r["Ui.FontSizeTab"] = tab;
        r["Ui.FontSizeIcon"] = icon;
        r["Ui.FontWeight"] = CssWeightToFontWeight(weight);
        r["Ui.Foreground"] = Brush(s.UiForeground, "#ffffff");
        r["Ui.ForegroundMuted"] = Brush(s.UiForegroundMuted, "#888888");
        r["Ui.Accent"] = Brush(s.UiAccent, "#6be5ff");
        r["Ui.Highlight"] = Brush(s.UiHighlight, "#c47cff");
        r["Ui.Success"] = Brush(s.UiSuccess, "#b4fb00");
        r["Ui.Warning"] = Brush(s.UiWarning, "#ffef5c");
        r["Ui.Error"] = Brush(s.UiError, "#ff2b7b");
        r["Ui.ChromeBackground"] = Brush(s.UiChromeBackground, "#000000");
        r["Ui.CardBackground"] = Brush(s.UiCardBackground, "#0d1b1f");
        r["Ui.CardBorder"] = Brush(s.UiCardBorder, "#1e4055");
        r["Ui.FolderSelectedBackground"] = Brush(s.UiFolderSelectedBackground, "#18323a");
        r["Ui.TabForeground"] = Brush(s.UiTabForeground, "#ffffff");
        r["Ui.TabSelectedBackground"] = Brush(s.UiTabSelectedBackground, "#6be5ff");
        r["Ui.TabSelectedForeground"] = Brush(s.UiTabSelectedForeground, "#111111");
        r["Ui.StatusBackground"] = Brush(s.UiStatusBackground, "#11252a");
        r["Ui.Icon"] = Brush(s.UiIcon, "#6be5ff");
        r["Ui.InputBackground"] = Brush(s.UiInputBackground, "#121212");
        r["Ui.InputForeground"] = Brush(s.UiInputForeground, "#ffffff");
        r["Ui.HoverBackground"] = Brush(s.UiHoverBackground, "#13292e");
        r["Ui.Splitter"] = Brush(s.UiSplitter, "#2e6773");
        r["Ui.TerminalBackground"] = Brush(s.TerminalBackground, "#000000");

        ApplySharpness(s.UiSharpness);
    }

    private static void ApplySharpness(string? sharpness)
    {
        TextFormattingMode mode;
        TextHintingMode hint;
        TextRenderingMode render;
        switch ((sharpness ?? "Sharp").Trim())
        {
            case "Smooth":
                mode = TextFormattingMode.Ideal;
                hint = TextHintingMode.Animated;
                render = TextRenderingMode.Auto;
                break;
            case "Balanced":
                mode = TextFormattingMode.Display;
                hint = TextHintingMode.Animated;
                render = TextRenderingMode.ClearType;
                break;
            default:
                mode = TextFormattingMode.Display;
                hint = TextHintingMode.Fixed;
                render = TextRenderingMode.ClearType;
                break;
        }

        var app = Application.Current;
        if (app is null) return;
        foreach (Window w in app.Windows)
            ApplyTextOptions(w, mode, hint, render);
    }

    private static void ApplyTextOptions(
        DependencyObject d,
        TextFormattingMode mode,
        TextHintingMode hint,
        TextRenderingMode render)
    {
        TextOptions.SetTextFormattingMode(d, mode);
        TextOptions.SetTextHintingMode(d, hint);
        TextOptions.SetTextRenderingMode(d, render);
    }

    private static SolidColorBrush Brush(string? hex, string fallback)
    {
        var brush = new SolidColorBrush(ParseColor(hex, fallback));
        brush.Freeze();
        return brush;
    }

    private static Color ParseColor(string? hex, string fallback)
    {
        try
        {
            string h = (hex ?? fallback).Trim().TrimStart('#');
            if (h.Length == 3)
                h = $"{h[0]}{h[0]}{h[1]}{h[1]}{h[2]}{h[2]}";
            if (h.Length != 6) h = fallback.TrimStart('#');
            byte r = Convert.ToByte(h[..2], 16);
            byte g = Convert.ToByte(h[2..4], 16);
            byte b = Convert.ToByte(h[4..6], 16);
            return Color.FromRgb(r, g, b);
        }
        catch
        {
            return Colors.White;
        }
    }

    private static FontWeight CssWeightToFontWeight(int css)
    {
        return css switch
        {
            <= 300 => FontWeights.Light,
            <= 400 => FontWeights.Normal,
            <= 500 => FontWeights.Medium,
            <= 600 => FontWeights.SemiBold,
            _ => FontWeights.Bold
        };
    }
}
