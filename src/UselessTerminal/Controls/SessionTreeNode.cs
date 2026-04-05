using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using UselessTerminal.Models;

namespace UselessTerminal.Controls;

public enum SessionTreeNodeKind
{
    Folder,
    Session
}

public sealed class SessionTreeNode : INotifyPropertyChanged
{
    public SessionTreeNodeKind Kind { get; init; }
    public SavedSession? Session { get; init; }
    public SessionFolder? Folder { get; init; }
    public ObservableCollection<SessionTreeNode> Children { get; } = new();

    private bool _isMultiSelected;

    /// <summary>Part of a Ctrl+multi-select; distinct from TreeViewItem.IsSelected (single keyboard focus).</summary>
    public bool IsMultiSelected
    {
        get => _isMultiSelected;
        set
        {
            if (_isMultiSelected == value) return;
            _isMultiSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
