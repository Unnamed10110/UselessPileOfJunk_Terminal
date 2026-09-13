namespace UselessTerminal.Models;

/// <summary>UI chrome presets and terminal/prompt color presets (independent).</summary>
public static class ThemePresets
{
    public static readonly Dictionary<string, UiThemePreset> UiThemes = new()
    {
        ["Default"] = UiDark("#000000", "#ffffff", "#888888", "#6be5ff", "#c47cff", "#b4fb00", "#ffef5c", "#ff2b7b"),
        ["Dracula"] = UiDark("#282a36", "#f8f8f2", "#6272a4", "#bd93f9", "#ff79c6", "#50fa7b", "#f1fa8c", "#ff5555"),
        ["Solarized Dark"] = UiDark("#002b36", "#839496", "#586e75", "#268bd2", "#d33682", "#859900", "#b58900", "#dc322f"),
        ["Monokai"] = UiDark("#272822", "#f8f8f2", "#75715e", "#ae81ff", "#fd971f", "#a6e22e", "#e6db74", "#f92672"),
        ["Nord"] = UiDark("#2e3440", "#d8dee9", "#4c566a", "#81a1c1", "#b48ead", "#a3be8c", "#ebcb8b", "#bf616a"),
        ["Catppuccin Mocha"] = UiDark("#1e1e2e", "#cdd6f4", "#585b70", "#89b4fa", "#cba6f7", "#a6e3a1", "#f9e2af", "#f38ba8"),
        ["One Dark"] = UiDark("#282c34", "#abb2bf", "#5c6370", "#61afef", "#c678dd", "#98c379", "#e5c07b", "#e06c75"),
        ["Gruvbox Dark"] = UiDark("#282828", "#ebdbb2", "#928374", "#83a598", "#d3869b", "#b8bb26", "#fabd2f", "#fb4934"),
        ["Tokyo Night"] = UiDark("#1a1b26", "#c0caf5", "#565f89", "#7aa2f7", "#bb9af7", "#9ece6a", "#e0af68", "#f7768e"),
        ["Ayu Dark"] = UiDark("#0A0E14", "#BFBDB6", "#626A73", "#FFB454", "#D2A6FF", "#AAD94C", "#E6B450", "#F07178"),
        ["Vesper"] = UiDark("#101010", "#FFFFFF", "#666666", "#FFC799", "#C9A0DC", "#A8C787", "#E8C989", "#D9827A"),

        ["AMOLED Green"] = UiAmoled("#39ff14", "#00e676", "#c6ff00", "#ff1744", "#18ffff"),
        ["AMOLED Red"] = UiAmoled("#ff1744", "#ff5252", "#ffab00", "#ff1744", "#ff80ab"),
        ["AMOLED Purple Neon"] = UiAmoled("#d500f9", "#ea80fc", "#f50057", "#ff1744", "#00e5ff"),
        ["AMOLED Cyan"] = UiAmoled("#00e5ff", "#18ffff", "#00e676", "#ff1744", "#ea80fc"),
        ["AMOLED Orange"] = UiAmoled("#ff6d00", "#ff9100", "#ffea00", "#ff1744", "#ff80ab"),
        ["AMOLED Pink"] = UiAmoled("#ff4081", "#ff80ab", "#f50057", "#ff1744", "#e040fb"),
        ["AMOLED Blue"] = UiAmoled("#2979ff", "#448aff", "#00e5ff", "#ff1744", "#7c4dff"),
        ["AMOLED Gold"] = UiAmoled("#ffd600", "#ffea00", "#ffab00", "#ff1744", "#ff6d00"),
        ["AMOLED Matrix"] = UiAmoled("#00ff41", "#33ff77", "#aaff00", "#ff003c", "#00e5ff"),
        ["AMOLED Ice"] = UiAmoled("#b3ffff", "#80d8ff", "#18ffff", "#ff5252", "#ea80fc"),

        ["Light"] = UiLight("#1565c0", "#6a1b9a", "#2e7d32", "#f9a825", "#c62828"),
        ["Light Green"] = UiLight("#2e7d32", "#00695c", "#558b2f", "#f9a825", "#c62828"),
        ["Light Red"] = UiLight("#c62828", "#ad1457", "#e65100", "#f9a825", "#b71c1c"),
        ["Light Purple Neon"] = UiLight("#8e24aa", "#0277bd", "#c2185b", "#f9a825", "#c62828"),
        ["Light Cyan"] = UiLight("#00838f", "#4527a0", "#00695c", "#f9a825", "#c62828"),
        ["Light Orange"] = UiLight("#ef6c00", "#ad1457", "#f9a825", "#ffea00", "#c62828"),
        ["Light Pink"] = UiLight("#c2185b", "#6a1b9a", "#d81b60", "#f9a825", "#c62828"),
        ["Light Blue"] = UiLight("#1565c0", "#4527a0", "#0277bd", "#f9a825", "#c62828"),
        ["Light Gold"] = UiLight("#f57f17", "#6a1b9a", "#ef6c00", "#ffea00", "#c62828"),
        ["Light Ice"] = UiLight("#0288d1", "#6a1b9a", "#00838f", "#f9a825", "#c62828"),
    };

    public static readonly Dictionary<string, PromptThemePreset> PromptThemes = new()
    {
        // Classic
        ["Default"] = Prompt("#000000", "#ffffff", "#ffffff", "#888888",
            "#ff2b7b", "#ffef5c", "#b4fb00", "#56ffef", "#6be5ff", "#c47cff",
            "#ffffff", "#ffffff", "#000000"),
        ["Dracula"] = Prompt("#282a36", "#f8f8f2", "#f8f8f2", "#6272a4",
            "#ff5555", "#f1fa8c", "#50fa7b", "#8be9fd", "#bd93f9", "#ff79c6",
            "#f8f8f2", "#44475a", "#f8f8f2"),
        ["Solarized Dark"] = Prompt("#002b36", "#839496", "#93a1a1", "#586e75",
            "#dc322f", "#b58900", "#859900", "#2aa198", "#268bd2", "#d33682",
            "#839496", "#073642", "#93a1a1"),
        ["Monokai"] = Prompt("#272822", "#f8f8f2", "#f8f8f2", "#75715e",
            "#f92672", "#e6db74", "#a6e22e", "#66d9ef", "#ae81ff", "#fd971f",
            "#f8f8f0", "#49483e", "#f8f8f2"),
        ["Nord"] = Prompt("#2e3440", "#d8dee9", "#eceff4", "#4c566a",
            "#bf616a", "#ebcb8b", "#a3be8c", "#88c0d0", "#81a1c1", "#b48ead",
            "#d8dee9", "#434c5e", "#eceff4"),
        ["Catppuccin Mocha"] = Prompt("#1e1e2e", "#cdd6f4", "#cdd6f4", "#585b70",
            "#f38ba8", "#f9e2af", "#a6e3a1", "#94e2d5", "#89b4fa", "#cba6f7",
            "#f5e0dc", "#45475a", "#cdd6f4"),
        ["One Dark"] = Prompt("#282c34", "#abb2bf", "#abb2bf", "#5c6370",
            "#e06c75", "#e5c07b", "#98c379", "#56b6c2", "#61afef", "#c678dd",
            "#abb2bf", "#3e4451", "#abb2bf"),
        ["Gruvbox Dark"] = Prompt("#282828", "#ebdbb2", "#ebdbb2", "#928374",
            "#fb4934", "#fabd2f", "#b8bb26", "#8ec07c", "#83a598", "#d3869b",
            "#ebdbb2", "#3c3836", "#ebdbb2"),
        ["Tokyo Night"] = Prompt("#1a1b26", "#a9b1d6", "#c0caf5", "#565f89",
            "#f7768e", "#e0af68", "#9ece6a", "#7dcfff", "#7aa2f7", "#bb9af7",
            "#c0caf5", "#33467c", "#c0caf5"),
        ["Ayu Dark"] = Prompt("#0A0E14", "#BFBDB6", "#FFB454", "#626A73",
            "#F07178", "#E6B450", "#AAD94C", "#95E6CB", "#FFB454", "#D2A6FF",
            "#FFB454", "#253340", "#BFBDB6"),
        ["Vesper"] = Prompt("#101010", "#FFFFFF", "#FFFFFF", "#666666",
            "#D9827A", "#E8C989", "#A8C787", "#8FBCBB", "#FFC799", "#C9A0DC",
            "#FFFFFF", "#2A2A2A", "#FFFFFF"),
        ["Light"] = Prompt("#f7f7f8", "#1a1a1a", "#1565c0", "#6b7280",
            "#c62828", "#f9a825", "#1565c0", "#00838f", "#1565c0", "#6a1b9a",
            "#1565c0", "#1565c0", "#ffffff"),

        // Neon — pure black canvas, electric accents
        ["Neon Acid"] = Neon("#39ff14", "#00e676", "#c6ff00", "#ff1744", "#18ffff", "#d500f9"),
        ["Neon Magenta"] = Neon("#ff00ff", "#ff66ff", "#ffea00", "#ff0055", "#00ffff", "#bf00ff"),
        ["Neon Cyan"] = Neon("#00ffff", "#66ffff", "#39ff14", "#ff1744", "#00e5ff", "#ff00aa"),
        ["Neon Laser"] = Neon("#ff0055", "#ff3377", "#ffea00", "#ff0033", "#00e5ff", "#ff00cc"),
        ["Neon Plasma"] = Neon("#bf00ff", "#e040fb", "#00ffcc", "#ff1744", "#00e5ff", "#ff00aa"),
        ["Neon Toxic"] = Neon("#ccff00", "#aaff00", "#39ff14", "#ff003c", "#00ffcc", "#ff00ff"),
        ["Neon Hot Pink"] = Neon("#ff1493", "#ff69b4", "#ffea00", "#ff0055", "#00ffff", "#ff00ff"),
        ["Neon Ice"] = Neon("#b3ffff", "#80d8ff", "#18ffff", "#ff5252", "#00e5ff", "#ea80fc"),
        ["Neon Ember"] = Neon("#ff6d00", "#ff9100", "#ffea00", "#ff1744", "#ff4081", "#ff00aa"),
        ["Neon Voltage"] = Neon("#ffe600", "#ffea00", "#39ff14", "#ff003c", "#00e5ff", "#ff00ff"),
        ["Neon Hyper"] = Neon("#00ff9f", "#00ffa3", "#ffea00", "#ff0055", "#00e5ff", "#ff00ff"),
        ["Neon Synthwave"] = Neon("#ff71ce", "#01cdfe", "#fffb96", "#ff003c", "#05ffa1", "#b967ff"),
        ["Neon Matrix"] = Neon("#00ff41", "#33ff77", "#aaff00", "#ff003c", "#00e5ff", "#39ff14"),
        ["Neon Arcade"] = Neon("#ff9f1c", "#2ec4b6", "#ffea00", "#e71d36", "#00e5ff", "#ff71ce"),
        ["Neon Aurora"] = Neon("#7cffc4", "#00e5ff", "#b967ff", "#ff2e63", "#05ffa1", "#ff71ce"),
        ["Neon Void"] = Neon("#c77dff", "#7b2cbf", "#ff006e", "#ff0055", "#00f5d4", "#f72585"),
        ["Neon Cobalt"] = Neon("#4cc9f0", "#4361ee", "#f72585", "#ff0055", "#7209b7", "#b5179e"),
        ["Neon Limewire"] = Neon("#d4ff00", "#a8ff3e", "#00ffcc", "#ff003c", "#ff00aa", "#39ff14"),
        ["Neon Blood"] = Neon("#ff0040", "#ff3366", "#ffea00", "#ff0022", "#00e5ff", "#ff00aa"),
        ["Neon Ultraviolet"] = Neon("#9d4edd", "#c77dff", "#ff006e", "#ff1744", "#00f5d4", "#e0aaff"),
        ["Neon Aquaflare"] = Neon("#00f5d4", "#00bbf9", "#fee440", "#f15bb5", "#9b5de5", "#00f5d4"),
        ["Neon Retrowave"] = Neon("#ff2a6d", "#05d9e8", "#d1f7ff", "#ff003c", "#7700ff", "#ff71ce"),
        ["Neon Ghost"] = Neon("#e0ffff", "#afeeee", "#7fffd4", "#ff69b4", "#dda0dd", "#f0ffff"),
        ["Neon Radar"] = Neon("#00ff9c", "#00c853", "#76ff03", "#ff1744", "#00e5ff", "#64ffda"),
    };

    public static void ApplyUi(AppSettings settings, UiThemePreset preset)
    {
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

    public static void ApplyPrompt(AppSettings settings, PromptThemePreset preset)
    {
        settings.TerminalBackground = preset.TerminalBackground;
        settings.TextDefault = preset.TextDefault;
        settings.ColorInput = preset.ColorInput;
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
    }

    private static UiThemePreset UiAmoled(string neon, string accent, string warn, string err, string msg) =>
        UiDark("#000000", "#f2f2f2", "#6b6b6b", accent, msg, neon, warn, err, neonUi: true, icon: neon);

    private static UiThemePreset UiLight(string accent, string highlight, string success, string warn, string err)
    {
        string chrome = Mix("#f8fafc", accent, 0.10);
        string border = Mix("#cbd5e1", accent, 0.40);
        string folderSel = Mix("#ffffff", accent, 0.16);
        string status = Mix("#eef2f7", accent, 0.14);
        string hover = Mix("#ffffff", accent, 0.12);
        string splitter = Mix("#94a3b8", accent, 0.45);
        return new()
        {
            UiForeground = "#111827",
            UiForegroundMuted = "#6b7280",
            UiAccent = accent,
            UiHighlight = highlight,
            UiSuccess = success,
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

    private static UiThemePreset UiDark(
        string bg, string fg, string muted, string acc, string hi, string success, string warn, string err,
        bool neonUi = false, string? icon = null)
    {
        double borderT = neonUi ? 0.42 : 0.28;
        double cardT = neonUi ? 0.10 : 0.12;
        double folderT = neonUi ? 0.24 : 0.20;
        double statusT = neonUi ? 0.18 : 0.14;
        double hoverT = neonUi ? 0.22 : 0.16;
        double splitT = neonUi ? 0.55 : 0.40;
        return new()
        {
            UiForeground = fg,
            UiForegroundMuted = muted,
            UiAccent = acc,
            UiHighlight = hi,
            UiSuccess = success,
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
            UiIcon = icon ?? acc,
            UiInputBackground = Mix(bg, fg, 0.08),
            UiInputForeground = fg,
            UiHoverBackground = Mix(bg, acc, hoverT),
            UiSplitter = Mix(bg, acc, splitT),
        };
    }

    private static PromptThemePreset Neon(
        string cmd, string cursor, string warn, string err, string msg, string hi)
    {
        // Default output (and typed input) use the neon accent so the whole buffer reads themed,
        // not only ANSI green/red slots when a shell happens to colorize.
        string muted = Mix(cmd, "#000000", 0.58);
        return Prompt("#000000", cmd, cmd, muted,
            err, warn, cmd, msg, cursor, hi,
            cursor, cursor, "#000000");
    }

    private static PromptThemePreset Prompt(
        string bg, string fg, string input, string muted,
        string err, string warn, string cmd, string msg, string acc, string hi,
        string cursor, string selBg, string selFg) => new()
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
    };

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

public sealed class UiThemePreset
{
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

public sealed class PromptThemePreset
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
}
