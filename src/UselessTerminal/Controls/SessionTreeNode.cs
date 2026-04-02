using System.Collections.ObjectModel;
using UselessTerminal.Models;

namespace UselessTerminal.Controls;

public enum SessionTreeNodeKind
{
    Folder,
    Session
}

public sealed class SessionTreeNode
{
    public SessionTreeNodeKind Kind { get; init; }
    public SavedSession? Session { get; init; }
    public SessionFolder? Folder { get; init; }
    public ObservableCollection<SessionTreeNode> Children { get; } = new();
}
