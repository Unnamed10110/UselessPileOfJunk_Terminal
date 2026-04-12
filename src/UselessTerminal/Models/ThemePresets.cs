namespace UselessTerminal.Models;

public static class ThemePresets
{
    public static readonly Dictionary<string, ThemePreset> All = new()
    {
        ["Default"] = new()
        {
            TerminalBackground = "#000000", TextDefault = "#ffffff", TextMuted = "#888888",
            ColorError = "#ff2b7b", ColorWarning = "#ffef5c", ColorCommand = "#b4fb00",
            ColorMessage = "#56ffef", ColorAccent = "#6be5ff", ColorHighlight = "#c47cff",
            CursorColor = "#ffffff", SelectionBackground = "#ffffff", SelectionForeground = "#000000"
        },
        ["Dracula"] = new()
        {
            TerminalBackground = "#282a36", TextDefault = "#f8f8f2", TextMuted = "#6272a4",
            ColorError = "#ff5555", ColorWarning = "#f1fa8c", ColorCommand = "#50fa7b",
            ColorMessage = "#8be9fd", ColorAccent = "#bd93f9", ColorHighlight = "#ff79c6",
            CursorColor = "#f8f8f2", SelectionBackground = "#44475a", SelectionForeground = "#f8f8f2"
        },
        ["Solarized Dark"] = new()
        {
            TerminalBackground = "#002b36", TextDefault = "#839496", TextMuted = "#586e75",
            ColorError = "#dc322f", ColorWarning = "#b58900", ColorCommand = "#859900",
            ColorMessage = "#2aa198", ColorAccent = "#268bd2", ColorHighlight = "#d33682",
            CursorColor = "#839496", SelectionBackground = "#073642", SelectionForeground = "#93a1a1"
        },
        ["Monokai"] = new()
        {
            TerminalBackground = "#272822", TextDefault = "#f8f8f2", TextMuted = "#75715e",
            ColorError = "#f92672", ColorWarning = "#e6db74", ColorCommand = "#a6e22e",
            ColorMessage = "#66d9ef", ColorAccent = "#ae81ff", ColorHighlight = "#fd971f",
            CursorColor = "#f8f8f0", SelectionBackground = "#49483e", SelectionForeground = "#f8f8f2"
        },
        ["Nord"] = new()
        {
            TerminalBackground = "#2e3440", TextDefault = "#d8dee9", TextMuted = "#4c566a",
            ColorError = "#bf616a", ColorWarning = "#ebcb8b", ColorCommand = "#a3be8c",
            ColorMessage = "#88c0d0", ColorAccent = "#81a1c1", ColorHighlight = "#b48ead",
            CursorColor = "#d8dee9", SelectionBackground = "#434c5e", SelectionForeground = "#eceff4"
        },
        ["Catppuccin Mocha"] = new()
        {
            TerminalBackground = "#1e1e2e", TextDefault = "#cdd6f4", TextMuted = "#585b70",
            ColorError = "#f38ba8", ColorWarning = "#f9e2af", ColorCommand = "#a6e3a1",
            ColorMessage = "#94e2d5", ColorAccent = "#89b4fa", ColorHighlight = "#cba6f7",
            CursorColor = "#f5e0dc", SelectionBackground = "#45475a", SelectionForeground = "#cdd6f4"
        },
        ["One Dark"] = new()
        {
            TerminalBackground = "#282c34", TextDefault = "#abb2bf", TextMuted = "#5c6370",
            ColorError = "#e06c75", ColorWarning = "#e5c07b", ColorCommand = "#98c379",
            ColorMessage = "#56b6c2", ColorAccent = "#61afef", ColorHighlight = "#c678dd",
            CursorColor = "#abb2bf", SelectionBackground = "#3e4451", SelectionForeground = "#abb2bf"
        },
        ["Gruvbox Dark"] = new()
        {
            TerminalBackground = "#282828", TextDefault = "#ebdbb2", TextMuted = "#928374",
            ColorError = "#fb4934", ColorWarning = "#fabd2f", ColorCommand = "#b8bb26",
            ColorMessage = "#8ec07c", ColorAccent = "#83a598", ColorHighlight = "#d3869b",
            CursorColor = "#ebdbb2", SelectionBackground = "#3c3836", SelectionForeground = "#ebdbb2"
        },
        ["Tokyo Night"] = new()
        {
            TerminalBackground = "#1a1b26", TextDefault = "#a9b1d6", TextMuted = "#565f89",
            ColorError = "#f7768e", ColorWarning = "#e0af68", ColorCommand = "#9ece6a",
            ColorMessage = "#7dcfff", ColorAccent = "#7aa2f7", ColorHighlight = "#bb9af7",
            CursorColor = "#c0caf5", SelectionBackground = "#33467c", SelectionForeground = "#c0caf5"
        },
    };

    public static void ApplyTo(AppSettings settings, ThemePreset preset)
    {
        settings.TerminalBackground = preset.TerminalBackground;
        settings.TextDefault = preset.TextDefault;
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
}

public sealed class ThemePreset
{
    public string TerminalBackground { get; init; } = "#000000";
    public string TextDefault { get; init; } = "#ffffff";
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
