namespace UselessTerminal.Models;

public sealed class WorkspaceProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Workspace";
    public List<WorkspaceTab> Tabs { get; set; } = new();
}

public sealed class WorkspaceTab
{
    /// <summary>Session Id from SessionStore; empty = default shell.</summary>
    public string SessionId { get; set; } = "";
    /// <summary>Fallback title if no session reference.</summary>
    public string Title { get; set; } = "";
    /// <summary>Fallback command if no session reference.</summary>
    public string Command { get; set; } = "";
    public string? WorkingDirectory { get; set; }
    public string? StartingCommand { get; set; }
}
