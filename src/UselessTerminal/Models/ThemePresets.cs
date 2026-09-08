namespace UselessTerminal.Models;

public static class ThemePresets
{
    public static readonly Dictionary<string, ThemePreset> All = new()
    {
        ["Default"] = Dark("#000000", "#ffffff", "#ffffff", "#888888",
            "#ff2b7b", "#ffef5c", "#b4fb00", "#56ffef", "#6be5ff", "#c47cff",
            "#ffffff", "#ffffff", "#000000"),
        ["Dracula"] = Dark("#282a36", "#f8f8f2", "#f8f8f2", "#6272a4",
            "#ff5555", "#f1fa8c", "#50fa7b", "#8be9fd", "#bd93f9", "#ff79c6",
            "#f8f8f2", "#44475a", "#f8f8f2"),
        ["Solarized Dark"] = Dark("#002b36", "#839496", "#93a1a1", "#586e75",
            "#dc322f", "#b58900", "#859900", "#2aa198", "#268bd2", "#d33682",
            "#839496", "#073642", "#93a1a1"),
        ["Monokai"] = Dark("#272822", "#f8f8f2", "#f8f8f2", "#75715e",
            "#f92672", "#e6db74", "#a6e22e", "#66d9ef", "#ae81ff", "#fd971f",
            "#f8f8f0", "#49483e", "#f8f8f2"),
        ["Nord"] = Dark("#2e3440", "#d8dee9", "#eceff4", "#4c566a",
            "#bf616a", "#ebcb8b", "#a3be8c", "#88c0d0", "#81a1c1", "#b48ead",
            "#d8dee9", "#434c5e", "#eceff4"),
        ["Catppuccin Mocha"] = Dark("#1e1e2e", "#cdd6f4", "#cdd6f4", "#585b70",
            "#f38ba8", "#f9e2af", "#a6e3a1", "#94e2d5", "#89b4fa", "#cba6f7",
            "#f5e0dc", "#45475a", "#cdd6f4"),
        ["One Dark"] = Dark("#282c34", "#abb2bf", "#abb2bf", "#5c6370",
            "#e06c75", "#e5c07b", "#98c379", "#56b6c2", "#61afef", "#c678dd",
            "#abb2bf", "#3e4451", "#abb2bf"),
        ["Gruvbox Dark"] = Dark("#282828", "#ebdbb2", "#ebdbb2", "#928374",
            "#fb4934", "#fabd2f", "#b8bb26", "#8ec07c", "#83a598", "#d3869b",
            "#ebdbb2", "#3c3836", "#ebdbb2"),
        ["Tokyo Night"] = Dark("#1a1b26", "#a9b1d6", "#c0caf5", "#565f89",
            "#f7768e", "#e0af68", "#9ece6a", "#7dcfff", "#7aa2f7", "#bb9af7",
            "#c0caf5", "#33467c", "#c0caf5"),
        ["Ayu Dark"] = Dark("#0A0E14", "#BFBDB6", "#FFB454", "#626A73",
            "#F07178", "#E6B450", "#AAD94C", "#95E6CB", "#FFB454", "#D2A6FF",
            "#FFB454", "#253340", "#BFBDB6"),
        ["Vesper"] = Dark("#101010", "#FFFFFF", "#FFFFFF", "#666666",
            "#D9827A", "#E8C989", "#A8C787", "#8FBCBB", "#FFC799", "#C9A0DC",
            "#FFFFFF", "#2A2A2A", "#FFFFFF"),

        ["AMOLED Green"] = Amoled("#39ff14", "#00e676", "#c6ff00", "#ff1744", "#18ffff"),
        ["AMOLED Red"] = Amoled("#ff1744", "#ff5252", "#ffab00", "#ff1744", "#ff80ab"),
        ["AMOLED Purple Neon"] = Amoled("#d500f9", "#ea80fc", "#f50057", "#ff1744", "#00e5ff"),
        ["AMOLED Cyan"] = Amoled("#00e5ff", "#18ffff", "#00e676", "#ff1744", "#ea80fc"),
        ["AMOLED Orange"] = Amoled("#ff6d00", "#ff9100", "#ffea00", "#ff1744", "#ff80ab"),
        ["AMOLED Pink"] = Amoled("#ff4081", "#ff80ab", "#f50057", "#ff1744", "#e040fb"),
        ["AMOLED Blue"] = Amoled("#2979ff", "#448aff", "#00e5ff", "#ff1744", "#7c4dff"),
        ["AMOLED Gold"] = Amoled("#ffd600", "#ffea00", "#ffab00", "#ff1744", "#ff6d00"),
        ["AMOLED Matrix"] = Amoled("#00ff41", "#33ff77", "#aaff00", "#ff003c", "#00e5ff"),
        ["AMOLED Ice"] = Amoled("#b3ffff", "#80d8ff", "#18ffff", "#ff5252", "#ea80fc"),

        ["Light"] = Light("#1565c0", "#00838f", "#2e7d32", "#c62828", "#6a1b9a"),
        ["Light Green"] = Light("#1b5e20", "#2e7d32", "#558b2f", "#c62828", "#00695c"),
        ["Light Red"] = Light("#b71c1c", "#c62828", "#e65100", "#b71c1c", "#ad1457"),
        ["Light Purple Neon"] = Light("#6a1b9a", "#8e24aa", "#c2185b", "#c62828", "#0277bd"),
        ["Light Cyan"] = Light("#006064", "#00838f", "#00695c", "#c62828", "#4527a0"),
        ["Light Orange"] = Light("#e65100", "#ef6c00", "#f9a825", "#c62828", "#ad1457"),
        ["Light Pink"] = Light("#ad1457", "#c2185b", "#d81b60", "#c62828", "#6a1b9a"),
        ["Light Blue"] = Light("#0d47a1", "#1565c0", "#0277bd", "#c62828", "#4527a0"),
        ["Light Gold"] = Light("#f9a825", "#f57f17", "#ef6c00", "#c62828", "#6a1b9a"),
        ["Light Ice"] = Light("#0277bd", "#0288d1", "#00838f", "#c62828", "#6a1b9a"),
    };

    public static void ApplyTo(AppSettings settings, ThemePreset preset)
    {
        settings.TerminalBackground = preset.TerminalBackground;
        settings.TextDefault = preset.TextDefault;
        // Typed-input color is a personal setting, independent of the theme's prompt/output
        // colors - applying a preset must not touch it (see SettingsWindow's "Typed input" entry).
        settings.TextMuted = preset.TextMuted;
        settings.ColorError = preset.ColorError;
        settings.ColorWarning = preset.ColorWarning;
        settings.ColorCommand = preset.ColorCommand;
        settings.ColorMessage = preset.ColorMessage;
        settings.ColorAccent = preset.ColorAccent;
        settings.ColorHighlight = preset.ColorHighlight;
        settings.CursorColor = preset.CursorColor;
        settings.SelectionBackground = preset.SelectionBackground;
        settings.SelectionForeground = preset.SelectionForeground;

        settings.UiForeground = preset.UiForeground;
        settings.UiForegroundMuted = preset.UiForegroundMuted;
        settings.UiAccent = preset.UiAccent;
        settings.UiHighlight = preset.UiHighlight;
        settings.UiSuccess = preset.UiSuccess;
        settings.UiWarning = preset.UiWarning;
        settings.UiError = preset.UiError;
        settings.UiChromeBackground = preset.UiChromeBackground;
        settings.UiCardBackground = preset.UiCardBackground;
        settings.UiCardBorder = preset.UiCardBorder;
        settings.UiFolderSelectedBackground = preset.UiFolderSelectedBackground;
        settings.UiTabForeground = preset.UiTabForeground;
        settings.UiTabSelectedBackground = preset.UiTabSelectedBackground;
        settings.UiTabSelectedForeground = preset.UiTabSelectedForeground;
        settings.UiStatusBackground = preset.UiStatusBackground;
        settings.UiIcon = preset.UiIcon;
        settings.UiInputBackground = preset.UiInputBackground;
        settings.UiInputForeground = preset.UiInputForeground;
        settings.UiHoverBackground = preset.UiHoverBackground;
        settings.UiSplitter = preset.UiSplitter;
    }

    private static ThemePreset Amoled(string neon, string accent, string warn, string err, string msg) =>
        Dark("#000000", "#f2f2f2", neon, "#6b6b6b",
            err, warn, neon, msg, accent, neon,
            neon, neon, "#000000", neonUi: true);

    private static ThemePreset Light(string command, string accent, string warn, string err, string highlight)
    {
        string chrome = Mix("#f8fafc", accent, 0.10);
        string border = Mix("#cbd5e1", accent, 0.40);
        string folderSel = Mix("#ffffff", accent, 0.16);
        string status = Mix("#eef2f7", accent, 0.14);
        string hover = Mix("#ffffff", accent, 0.12);
        string splitter = Mix("#94a3b8", accent, 0.45);
        return new()
        {
            TerminalBackground = "#f7f7f8",
            TextDefault = "#1a1a1a",
            ColorInput = command,
            TextMuted = "#6b7280",
            ColorError = err,
            ColorWarning = warn,
            ColorCommand = command,
            ColorMessage = accent,
            ColorAccent = accent,
            ColorHighlight = highlight,
            CursorColor = command,
            SelectionBackground = command,
            SelectionForeground = "#ffffff",
            UiForeground = "#111827",
            UiForegroundMuted = "#6b7280",
            UiAccent = accent,
            UiHighlight = highlight,
            UiSuccess = command,
            UiWarning = warn,
            UiError = err,
            UiChromeBackground = chrome,
            UiCardBackground = "#ffffff",
            UiCardBorder = border,
            UiFolderSelectedBackground = folderSel,
            UiTabForeground = "#111827",
            UiTabSelectedBackground = accent,
            UiTabSelectedForeground = ContrastFg(accent),
            UiStatusBackground = status,
            UiIcon = accent,
            UiInputBackground = "#ffffff",
            UiInputForeground = "#111827",
            UiHoverBackground = hover,
            UiSplitter = splitter,
        };
    }

    private static ThemePreset Dark(
        string bg, string fg, string input, string muted,
        string err, string warn, string cmd, string msg, string acc, string hi,
        string cursor, string selBg, string selFg,
        bool neonUi = false)
    {
        double borderT = neonUi ? 0.42 : 0.28;
        double cardT = neonUi ? 0.10 : 0.12;
        double folderT = neonUi ? 0.24 : 0.20;
        double statusT = neonUi ? 0.18 : 0.14;
        double hoverT = neonUi ? 0.22 : 0.16;
        double splitT = neonUi ? 0.55 : 0.40;
        return new()
        {
            TerminalBackground = bg,
            TextDefault = fg,
            ColorInput = input,
            TextMuted = muted,
            ColorError = err,
            ColorWarning = warn,
            ColorCommand = cmd,
            ColorMessage = msg,
            ColorAccent = acc,
            ColorHighlight = hi,
            CursorColor = cursor,
            SelectionBackground = selBg,
            SelectionForeground = selFg,
            UiForeground = fg,
            UiForegroundMuted = muted,
            UiAccent = acc,
            UiHighlight = hi,
            UiSuccess = cmd,
            UiWarning = warn,
            UiError = err,
            UiChromeBackground = bg,
            UiCardBackground = Mix(bg, acc, cardT),
            UiCardBorder = Mix(bg, acc, borderT),
            UiFolderSelectedBackground = Mix(bg, acc, folderT),
            UiTabForeground = fg,
            UiTabSelectedBackground = acc,
            UiTabSelectedForeground = ContrastFg(acc),
            UiStatusBackground = Mix(bg, acc, statusT),
            UiIcon = acc,
            UiInputBackground = Mix(bg, fg, 0.08),
            UiInputForeground = fg,
            UiHoverBackground = Mix(bg, acc, hoverT),
            UiSplitter = Mix(bg, acc, splitT),
        };
    }

    internal static string ContrastFg(string bg)
    {
        var (r, g, b) = Rgb(bg);
        double y = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0;
        return y > 0.55 ? "#111111" : "#ffffff";
    }

    internal static bool IsLight(string hex)
    {
        var (r, g, b) = Rgb(hex);
        return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0 > 0.55;
    }

    private static string Mix(string a, string b, double t)
    {
        var (ar, ag, ab) = Rgb(a);
        var (br, bg, bb) = Rgb(b);
        byte r = (byte)Math.Clamp((int)Math.Round(ar + (br - ar) * t), 0, 255);
        byte g = (byte)Math.Clamp((int)Math.Round(ag + (bg - ag) * t), 0, 255);
        byte bl = (byte)Math.Clamp((int)Math.Round(ab + (bb - ab) * t), 0, 255);
        return $"#{r:X2}{g:X2}{bl:X2}";
    }

    private static (int r, int g, int b) Rgb(string hex)
    {
        string h = hex.Trim().TrimStart('#');
        if (h.Length == 3)
            h = $"{h[0]}{h[0]}{h[1]}{h[1]}{h[2]}{h[2]}";
        if (h.Length != 6) return (0, 0, 0);
        return (Convert.ToInt32(h[..2], 16), Convert.ToInt32(h[2..4], 16), Convert.ToInt32(h[4..6], 16));
    }
}

public sealed class ThemePreset
{
    public string TerminalBackground { get; init; } = "#000000";
    public string TextDefault { get; init; } = "#ffffff";
    public string ColorInput { get; init; } = "#ffffff";
    public string TextMuted { get; init; } = "#888888";
    public string ColorError { get; init; } = "#ff2b7b";
    public string ColorWarning { get; init; } = "#ffef5c";
    public string ColorCommand { get; init; } = "#b4fb00";
    public string ColorMessage { get; init; } = "#56ffef";
    public string ColorAccent { get; init; } = "#6be5ff";
    public string ColorHighlight { get; init; } = "#c47cff";
    public string CursorColor { get; init; } = "#ffffff";
    public string SelectionBackground { get; init; } = "#ffffff";
    public string SelectionForeground { get; init; } = "#000000";

    public string UiForeground { get; init; } = "#f4f4f5";
    public string UiForegroundMuted { get; init; } = "#9ca3af";
    public string UiAccent { get; init; } = "#6be5ff";
    public string UiHighlight { get; init; } = "#c47cff";
    public string UiSuccess { get; init; } = "#b4fb00";
    public string UiWarning { get; init; } = "#ffef5c";
    public string UiError { get; init; } = "#ff2b7b";
    public string UiChromeBackground { get; init; } = "#000000";
    public string UiCardBackground { get; init; } = "#0d1b1f";
    public string UiCardBorder { get; init; } = "#1e4055";
    public string UiFolderSelectedBackground { get; init; } = "#18323a";
    public string UiTabForeground { get; init; } = "#ffffff";
    public string UiTabSelectedBackground { get; init; } = "#6be5ff";
    public string UiTabSelectedForeground { get; init; } = "#111111";
    public string UiStatusBackground { get; init; } = "#11252a";
    public string UiIcon { get; init; } = "#6be5ff";
    public string UiInputBackground { get; init; } = "#121212";
    public string UiInputForeground { get; init; } = "#ffffff";
    public string UiHoverBackground { get; init; } = "#13292e";
    public string UiSplitter { get; init; } = "#2e6773";
}
