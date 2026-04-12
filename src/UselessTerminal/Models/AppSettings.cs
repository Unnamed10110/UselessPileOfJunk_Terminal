namespace UselessTerminal.Models;

public sealed class AppSettings
{
    public string FontFamily { get; set; } = "'Cascadia Code', 'Cascadia Mono', Consolas, 'Courier New', monospace";
    public int FontSize { get; set; } = 14;
    public bool CursorBlink { get; set; } = true;
    public string CursorStyle { get; set; } = "bar";
    public int Scrollback { get; set; } = 10000;

    /// <summary>Terminal canvas behind the shell (xterm background).</summary>
    public string TerminalBackground { get; set; } = "#000000";

    /// <summary>Default text, prompt, and normal output.</summary>
    public string TextDefault { get; set; } = "#ffffff";

    /// <summary>Dimmed secondary text (comments, de-emphasized output).</summary>
    public string TextMuted { get; set; } = "#888888";

    /// <summary>Errors and stderr-style output.</summary>
    public string ColorError { get; set; } = "#ff2b7b";

    /// <summary>Warnings.</summary>
    public string ColorWarning { get; set; } = "#ffef5c";

    /// <summary>Commands, keywords, and success-style output.</summary>
    public string ColorCommand { get; set; } = "#b4fb00";

    /// <summary>Info and system messages.</summary>
    public string ColorMessage { get; set; } = "#56ffef";

    /// <summary>Paths, links, and primary accents (ANSI blue).</summary>
    public string ColorAccent { get; set; } = "#6be5ff";

    /// <summary>Highlights and secondary accents (ANSI magenta).</summary>
    public string ColorHighlight { get; set; } = "#c47cff";

    public string CursorColor { get; set; } = "#ffffff";
    public string SelectionBackground { get; set; } = "#ffffff";
    public string SelectionForeground { get; set; } = "#000000";

    /// <summary>Full path to an image file shown behind the terminal (empty = none).</summary>
    public string ShellBackgroundImagePath { get; set; } = "";

    /// <summary>Opacity of the background image layer (0–1).</summary>
    public double ShellBackgroundImageOpacity { get; set; } = 0.52;

    /// <summary>Window backdrop: "None", "Mica", "Acrylic". Only effective on Win 11+.</summary>
    public string WindowBackdrop { get; set; } = "None";

    /// <summary>
    /// Builds the xterm theme payload: semantic colors are expanded into the 16 ANSI slots so shells
    /// and prompts that use standard colors map predictably (errors → red, warnings → yellow, etc.).
    /// </summary>
    public string ToThemeJson()
    {
        string bg = TerminalBackground;
        string fg = TextDefault;
        string err = ColorError;
        string warn = ColorWarning;
        string cmd = ColorCommand;
        string msg = ColorMessage;
        string acc = ColorAccent;
        string hi = ColorHighlight;
        string muted = TextMuted;

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            background = bg,
            foreground = fg,
            cursor = CursorColor,
            cursorAccent = bg,
            selectionBackground = SelectionBackground,
            selectionForeground = SelectionForeground,
            black = "#000000",
            red = err,
            green = cmd,
            yellow = warn,
            blue = acc,
            magenta = hi,
            cyan = msg,
            white = fg,
            brightBlack = muted,
            brightRed = err,
            brightGreen = cmd,
            brightYellow = warn,
            brightBlue = acc,
            brightMagenta = hi,
            brightCyan = msg,
            brightWhite = fg
        });
    }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
