namespace UselessTerminal.Models;

public sealed class AppSettings
{
    public string FontFamily { get; set; } = "'Cascadia Code', 'Cascadia Mono', Consolas, 'Courier New', monospace";
    public int FontSize { get; set; } = 14;
    public bool CursorBlink { get; set; } = true;
    public string CursorStyle { get; set; } = "bar";
    public int Scrollback { get; set; } = 10000;

    public string Background { get; set; } = "#000000";
    public string Foreground { get; set; } = "#ffffff";
    public string Cursor { get; set; } = "#ffffff";
    public string SelectionBackground { get; set; } = "#ffffff";
    public string SelectionForeground { get; set; } = "#000000";

    public string Black { get; set; } = "#000000";
    public string Red { get; set; } = "#ff2b7b";
    public string Green { get; set; } = "#b4fb00";
    public string Yellow { get; set; } = "#ffef5c";
    public string Blue { get; set; } = "#6be5ff";
    public string Magenta { get; set; } = "#c47cff";
    public string Cyan { get; set; } = "#56ffef";
    public string White { get; set; } = "#ffffff";

    public string BrightBlack { get; set; } = "#888888";
    public string BrightRed { get; set; } = "#ff5c9a";
    public string BrightGreen { get; set; } = "#ccff33";
    public string BrightYellow { get; set; } = "#fffa80";
    public string BrightBlue { get; set; } = "#8eedff";
    public string BrightMagenta { get; set; } = "#d9a3ff";
    public string BrightCyan { get; set; } = "#80fff5";
    public string BrightWhite { get; set; } = "#ffffff";

    /// <summary>Full path to an image file shown behind the terminal (empty = none).</summary>
    public string ShellBackgroundImagePath { get; set; } = "";

    /// <summary>Opacity of the background image layer (0–1).</summary>
    public double ShellBackgroundImageOpacity { get; set; } = 0.4;

    public string ToThemeJson()
    {
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            background = Background,
            foreground = Foreground,
            cursor = Cursor,
            cursorAccent = Background,
            selectionBackground = SelectionBackground,
            selectionForeground = SelectionForeground,
            black = Black,
            red = Red,
            green = Green,
            yellow = Yellow,
            blue = Blue,
            magenta = Magenta,
            cyan = Cyan,
            white = White,
            brightBlack = BrightBlack,
            brightRed = BrightRed,
            brightGreen = BrightGreen,
            brightYellow = BrightYellow,
            brightBlue = BrightBlue,
            brightMagenta = BrightMagenta,
            brightCyan = BrightCyan,
            brightWhite = BrightWhite
        });
    }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
