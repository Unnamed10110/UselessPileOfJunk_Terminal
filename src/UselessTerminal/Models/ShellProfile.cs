namespace UselessTerminal.Models;

public sealed class ShellProfile
{
    public required string Name { get; init; }
    public required string Command { get; init; }
    public string Arguments { get; init; } = "";
    public string IconGlyph { get; init; } = "\uE756";
    public string Color { get; init; } = "#888888";
    public bool IsDefault { get; init; }
}
