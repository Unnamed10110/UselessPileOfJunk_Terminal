namespace UselessTerminal.Models;

public sealed class SessionFolder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Folder";
    public string? ParentId { get; set; }
    public int SortOrder { get; set; }
}
