using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using UselessTerminal.Models;
using UselessTerminal.Services;
using MenuItem = System.Windows.Controls.MenuItem;

namespace UselessTerminal.Controls;

public sealed partial class SessionPanel : UserControl
{
    private const double DragThreshold = 6;

    private readonly SessionStore _store = new();
    private SessionTreeNode? _dragSource;
    private Point _mouseDown;

    /// <summary>Second argument: true = elevated external console (UAC), false = embedded terminal tab.</summary>
    public event Action<SavedSession, bool>? SessionLaunched;

    public SessionPanel()
    {
        InitializeComponent();
        _store.Load();
        _store.SeedDefaults();
        RefreshList();
    }

    public void TriggerAddSession() => AddSession_Click(this, new RoutedEventArgs());

    public void Reload()
    {
        _store.Load();
        RefreshList(SearchBox.Text);
    }

    public void ImportWindowsTerminalProfiles()
    {
        var imported = WtProfileImporter.Import();
        if (imported.Count == 0)
        {
            MessageBox.Show("No Windows Terminal profiles found.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        foreach (var session in imported)
        {
            if (!_store.Sessions.Any(s => s.Name == session.Name))
                _store.Add(session);
        }

        RefreshList(SearchBox.Text);
        MessageBox.Show($"Imported {imported.Count} profile(s) from Windows Terminal.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RefreshList(string? filter = null)
    {
        SessionTree.ItemsSource = string.IsNullOrWhiteSpace(filter)
            ? BuildFullTree()
            : BuildFilteredFlat(filter.Trim());

        if (string.IsNullOrWhiteSpace(filter))
            Dispatcher.BeginInvoke(ExpandFolderNodesWithChildren, DispatcherPriority.Loaded);
    }

    private void ExpandFolderNodesWithChildren()
    {
        foreach (var tvi in EnumerateTreeViewItems(SessionTree))
        {
            if (tvi.DataContext is SessionTreeNode { Kind: SessionTreeNodeKind.Folder } n && n.Children.Count > 0)
                tvi.IsExpanded = true;
        }
    }

    private ObservableCollection<SessionTreeNode> BuildFullTree()
    {
        var root = new ObservableCollection<SessionTreeNode>();
        foreach (var f in _store.Folders.Where(x => x.ParentId == null).OrderBy(x => x.SortOrder))
            root.Add(BuildFolderNode(f));
        foreach (var s in _store.Sessions.Where(x => x.FolderId == null).OrderBy(x => x.SortOrder))
            root.Add(new SessionTreeNode { Kind = SessionTreeNodeKind.Session, Session = s });
        return root;
    }

    private SessionTreeNode BuildFolderNode(SessionFolder folder)
    {
        var node = new SessionTreeNode { Kind = SessionTreeNodeKind.Folder, Folder = folder };
        foreach (var s in _store.Sessions.Where(x => x.FolderId == folder.Id).OrderBy(x => x.SortOrder))
            node.Children.Add(new SessionTreeNode { Kind = SessionTreeNodeKind.Session, Session = s });
        return node;
    }

    private ObservableCollection<SessionTreeNode> BuildFilteredFlat(string filter)
    {
        var list = new ObservableCollection<SessionTreeNode>();
        if (string.IsNullOrEmpty(filter)) return list;

        foreach (var s in SessionsMatching(filter))
            list.Add(new SessionTreeNode { Kind = SessionTreeNodeKind.Session, Session = s });
        return list;
    }

    private IEnumerable<SavedSession> SessionsMatching(string filter)
    {
        return _store.Sessions
            .Where(s =>
                s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                s.ShellPath.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.SortOrder);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshList(SearchBox.Text);
    }

    private void SessionTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindParent<Button>(e.OriginalSource as DependencyObject) is not null)
            return;
        if (FindParent<TreeViewItem>(e.OriginalSource as DependencyObject) is not { DataContext: SessionTreeNode node })
            return;
        if (node.Kind != SessionTreeNodeKind.Session || node.Session is null)
            return;

        bool runAsAdmin = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        SessionLaunched?.Invoke(node.Session, runAsAdmin);
        e.Handled = true;
    }

    private void SessionTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragSource = null;
        if (FindParent<Button>(e.OriginalSource as DependencyObject) is not null)
            return;
        if (FindParent<TreeViewItem>(e.OriginalSource as DependencyObject) is not { DataContext: SessionTreeNode node })
            return;
        _dragSource = node;
        _mouseDown = e.GetPosition(null);
    }

    private void SessionTree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragSource == null) return;
        var pos = e.GetPosition(null);
        if ((pos - _mouseDown).Length < DragThreshold) return;
        var data = _dragSource;
        _dragSource = null;
        DragDrop.DoDragDrop(SessionTree, new DataObject(typeof(SessionTreeNode), data), DragDropEffects.Move);
    }

    private void SessionTree_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragSource = null;
    }

    private void SessionTree_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(SessionTreeNode)))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
    }

    private void SessionTree_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(SessionTreeNode))) return;
        if (e.Data.GetData(typeof(SessionTreeNode)) is not SessionTreeNode src) return;

        var pt = e.GetPosition(SessionTree);
        var hit = VisualTreeHelper.HitTest(SessionTree, pt);
        TreeViewItem? item = null;
        if (hit.VisualHit is not null)
        {
            DependencyObject? d = hit.VisualHit;
            while (d is not null)
            {
                if (d is TreeViewItem tvi)
                {
                    item = tvi;
                    break;
                }

                d = VisualTreeHelper.GetParent(d);
            }
        }

        if (item?.DataContext is SessionTreeNode target)
        {
            bool topHalf = e.GetPosition(item).Y < item.ActualHeight * 0.5;
            ApplyDrop(src, target, topHalf);
        }
        else
            ApplyDropRoot(src);

        RefreshList(SearchBox.Text);
    }

    private void ApplyDropRoot(SessionTreeNode src)
    {
        if (src.Kind == SessionTreeNodeKind.Session && src.Session is not null)
            _store.MoveSessionToFolder(src.Session, null);
        else if (src.Kind == SessionTreeNodeKind.Folder && src.Folder is not null)
            _store.MoveFolderToRoot(src.Folder);
    }

    private void ApplyDrop(SessionTreeNode src, SessionTreeNode target, bool topHalf)
    {
        if (src.Kind == SessionTreeNodeKind.Session && src.Session is not null)
        {
            if (target.Kind == SessionTreeNodeKind.Folder && target.Folder is not null)
                _store.MoveSessionToFolder(src.Session, target.Folder.Id);
            else if (target.Kind == SessionTreeNodeKind.Session && target.Session is not null)
            {
                if (src.Session.Id == target.Session.Id) return;
                if (topHalf)
                    _store.MoveSessionBeforeSibling(src.Session, target.Session);
                else
                    _store.MoveSessionAfterSibling(src.Session, target.Session);
            }

            return;
        }

        if (src.Kind == SessionTreeNodeKind.Folder && src.Folder is not null)
        {
            if (target.Kind == SessionTreeNodeKind.Folder && target.Folder is not null)
            {
                if (src.Folder.Id == target.Folder.Id) return;
                if (topHalf)
                    _store.MoveFolderBeforeSibling(src.Folder, target.Folder);
                else
                    _store.MoveFolderAfterSibling(src.Folder, target.Folder);
            }
            else if (target.Kind == SessionTreeNodeKind.Session && target.Session is not null)
            {
                if (target.Session.FolderId == src.Folder.Id) return;
                _store.MoveFolderToRoot(src.Folder);
            }
        }
    }

    private void AddFolderRoot_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new RenameDialog("New Folder") { Title = "New folder", Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;
        _store.AddFolder(new SessionFolder { Name = dlg.ResultName });
        RefreshList(SearchBox.Text);
    }

    private void AddSessionInFolder_Click(object sender, RoutedEventArgs e)
    {
        if (GetTagId(sender) is not string folderId) return;
        AddSessionInFolder(folderId);
    }

    private void AddSessionInFolder(string folderId)
    {
        var session = new SavedSession { FolderId = folderId };
        if (ShowEditDialog(session, "New Session"))
        {
            _store.Add(session);
            RefreshList(SearchBox.Text);
        }
    }

    private void RenameFolder_Click(object sender, RoutedEventArgs e)
    {
        if (GetTagId(sender) is not string id) return;
        RenameFolderById(id);
    }

    private void RenameFolderById(string id)
    {
        var folder = _store.FindFolderById(id);
        if (folder is null) return;

        var dlg = new RenameDialog(folder.Name) { Title = "Rename folder", Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;
        folder.Name = dlg.ResultName;
        _store.UpdateFolder(folder);
        RefreshList(SearchBox.Text);
    }

    private void DeleteFolder_Click(object sender, RoutedEventArgs e)
    {
        if (GetTagId(sender) is not string id) return;
        DeleteFolderById(id);
    }

    private void DeleteFolderById(string id)
    {
        var folder = _store.FindFolderById(id);
        if (folder is null) return;

        var result = MessageBox.Show(
            $"Delete folder \"{folder.Name}\"? Sessions in this folder will be moved to the root list.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _store.RemoveFolder(folder);
            RefreshList(SearchBox.Text);
        }
    }

    private void AddSession_Click(object sender, RoutedEventArgs e)
    {
        var session = new SavedSession();
        if (ShowEditDialog(session, "New Session"))
        {
            _store.Add(session);
            RefreshList(SearchBox.Text);
        }
    }

    private void EditSession_Click(object sender, RoutedEventArgs e)
    {
        if (GetTagId(sender) is not string id) return;
        EditSessionById(id);
    }

    private void EditSessionById(string id)
    {
        var session = _store.FindById(id);
        if (session is null) return;

        if (ShowEditDialog(session, "Edit Session"))
        {
            _store.Update(session);
            RefreshList(SearchBox.Text);
        }
    }

    private void DeleteSession_Click(object sender, RoutedEventArgs e)
    {
        if (GetTagId(sender) is not string id) return;
        DeleteSessionById(id);
    }

    private void DeleteSessionById(string id)
    {
        var session = _store.FindById(id);
        if (session is null) return;

        var result = MessageBox.Show(
            $"Delete session \"{session.Name}\"?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _store.Remove(session);
            RefreshList(SearchBox.Text);
        }
    }

    private void ContextFolder_AddSession_Click(object sender, RoutedEventArgs e)
    {
        if (GetTagId(sender) is not string id) return;
        AddSessionInFolder(id);
    }

    private void ContextFolder_Rename_Click(object sender, RoutedEventArgs e)
    {
        if (GetTagId(sender) is not string id) return;
        RenameFolderById(id);
    }

    private void ContextFolder_Delete_Click(object sender, RoutedEventArgs e)
    {
        if (GetTagId(sender) is not string id) return;
        DeleteFolderById(id);
    }

    private void ContextFolder_Expand_Click(object sender, RoutedEventArgs e)
    {
        if (GetTagId(sender) is not string id) return;
        SetFolderExpanded(id, true);
    }

    private void ContextFolder_Collapse_Click(object sender, RoutedEventArgs e)
    {
        if (GetTagId(sender) is not string id) return;
        SetFolderExpanded(id, false);
    }

    private void SetFolderExpanded(string folderId, bool expanded)
    {
        foreach (var tvi in EnumerateTreeViewItems(SessionTree))
        {
            if (tvi.DataContext is SessionTreeNode n
                && n.Kind == SessionTreeNodeKind.Folder
                && n.Folder?.Id == folderId)
            {
                tvi.IsExpanded = expanded;
                return;
            }
        }
    }

    private static IEnumerable<TreeViewItem> EnumerateTreeViewItems(ItemsControl parent)
    {
        for (int i = 0; i < parent.Items.Count; i++)
        {
            if (parent.ItemContainerGenerator.ContainerFromIndex(i) is not TreeViewItem tvi)
                continue;
            yield return tvi;
            foreach (var nested in EnumerateTreeViewItems(tvi))
                yield return nested;
        }
    }

    private void ContextSession_Open_Click(object sender, RoutedEventArgs e)
    {
        if (GetTagId(sender) is not string id) return;
        var session = _store.FindById(id);
        if (session is not null)
            SessionLaunched?.Invoke(session, false);
    }

    private void ContextSession_RunAsAdmin_Click(object sender, RoutedEventArgs e)
    {
        if (GetTagId(sender) is not string id) return;
        var session = _store.FindById(id);
        if (session is not null)
            SessionLaunched?.Invoke(session, true);
    }

    private void ContextSession_Edit_Click(object sender, RoutedEventArgs e)
    {
        if (GetTagId(sender) is not string id) return;
        EditSessionById(id);
    }

    private void ContextSession_Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (GetTagId(sender) is not string id) return;
        var session = _store.FindById(id);
        if (session is null) return;
        var clone = session.Clone();
        _store.Add(clone);
        RefreshList(SearchBox.Text);
    }

    private void ContextSession_Delete_Click(object sender, RoutedEventArgs e)
    {
        if (GetTagId(sender) is not string id) return;
        DeleteSessionById(id);
    }

    private void ContextTree_NewSession_Click(object sender, RoutedEventArgs e) => AddSession_Click(sender, e);

    private void ContextTree_NewFolder_Click(object sender, RoutedEventArgs e) => AddFolderRoot_Click(sender, e);

    private void ContextTree_ImportWt_Click(object sender, RoutedEventArgs e) => ImportWt_Click(sender, e);

    private static string? GetTagId(object sender)
    {
        return sender switch
        {
            Button b => b.Tag as string,
            MenuItem m => m.Tag as string,
            _ => null
        };
    }

    private void ImportWt_Click(object sender, RoutedEventArgs e) => ImportWindowsTerminalProfiles();

    private bool ShowEditDialog(SavedSession session, string title)
    {
        var dialog = new SessionEditDialog(session) { Title = title };
        dialog.Owner = Window.GetWindow(this);
        return dialog.ShowDialog() == true;
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T t) return t;
            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }
}
