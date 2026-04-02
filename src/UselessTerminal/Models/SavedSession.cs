using System.Text.Json.Serialization;

namespace UselessTerminal.Models;

public sealed class SavedSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Session";
    public string Description { get; set; } = "";

    public string ShellPath { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public string StartingCommand { get; set; } = "";
    public string ColorTag { get; set; } = "#00ff44";
    public string IconGlyph { get; set; } = "\uE756";
    public int SortOrder { get; set; }

    [JsonIgnore]
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    [JsonIgnore]
    public string DisplayCommand => string.IsNullOrEmpty(Arguments)
        ? ShellPath
        : $"{ShellPath} {Arguments}";

    public string GetFullCommand()
    {
        return string.IsNullOrWhiteSpace(Arguments)
            ? ShellPath
            : $"\"{ShellPath}\" {Arguments}";
    }

    public SavedSession Clone() => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = $"{Name} (Copy)",
        Description = Description,
        ShellPath = ShellPath,
        Arguments = Arguments,
        WorkingDirectory = WorkingDirectory,
        StartingCommand = StartingCommand,
        ColorTag = ColorTag,
        IconGlyph = IconGlyph,
        SortOrder = SortOrder + 1
    };
}
