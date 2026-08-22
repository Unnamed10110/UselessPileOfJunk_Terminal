namespace UselessTerminal.Models;

public sealed class AppSettings
{
    public string FontFamily { get; set; } = "'Cascadia Code', 'Cascadia Mono', Consolas, 'Courier New', monospace";
    public int FontSize { get; set; } = 14;

    /// <summary>CSS font-weight for normal text (100–900). Bold ANSI uses a heavier derived weight.</summary>
    public int FontWeight { get; set; } = 400;
    public bool CursorBlink { get; set; } = true;
    public string CursorStyle { get; set; } = "bar";
    public int Scrollback { get; set; } = 10000;

    /// <summary>Terminal canvas behind the shell (xterm background).</summary>
    public string TerminalBackground { get; set; } = "#000000";

    /// <summary>Default text, prompt, and normal output.</summary>
    public string TextDefault { get; set; } = "#ffffff";

    /// <summary>Color of typed input (PSReadLine command/default in PowerShell).</summary>
    public string ColorInput { get; set; } = "#ffffff";

    /// <summary>WPF chrome scale (tabs, session panel, status bar). 0.75–2.0.</summary>
    public double UiScale { get; set; } = 1.0;

    /// <summary>Font family for tabs, session cards, folder cards, and status bar.</summary>
    public string UiFontFamily { get; set; } = "Segoe UI";

    /// <summary>Base UI font size (session card titles). 10–22.</summary>
    public int UiFontSize { get; set; } = 13;

    /// <summary>UI font weight 300–700.</summary>
    public int UiFontWeight { get; set; } = 400;

    /// <summary>Glyph hinting: Sharp, Balanced, or Smooth.</summary>
    public string UiSharpness { get; set; } = "Sharp";

    public string UiForeground { get; set; } = "#ffffff";
    public string UiForegroundMuted { get; set; } = "#888888";
    public string UiAccent { get; set; } = "#6be5ff";
    public string UiHighlight { get; set; } = "#c47cff";
    public string UiSuccess { get; set; } = "#b4fb00";
    public string UiWarning { get; set; } = "#ffef5c";
    public string UiError { get; set; } = "#ff2b7b";
    public string UiChromeBackground { get; set; } = "#000000";
    public string UiCardBackground { get; set; } = "#0d1b1f";
    public string UiCardBorder { get; set; } = "#1e4055";
    public string UiFolderSelectedBackground { get; set; } = "#18323a";
    public string UiTabForeground { get; set; } = "#ffffff";
    public string UiTabSelectedBackground { get; set; } = "#6be5ff";
    public string UiTabSelectedForeground { get; set; } = "#111111";
    public string UiStatusBackground { get; set; } = "#11252a";
    public string UiIcon { get; set; } = "#6be5ff";
    public string UiInputBackground { get; set; } = "#121212";
    public string UiInputForeground { get; set; } = "#ffffff";
    public string UiHoverBackground { get; set; } = "#13292e";
    public string UiSplitter { get; set; } = "#2e6773";

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
