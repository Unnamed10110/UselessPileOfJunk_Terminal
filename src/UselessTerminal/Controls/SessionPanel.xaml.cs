using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using UselessTerminal.Models;
using UselessTerminal.Services;
using MenuItem = System.Windows.Controls.MenuItem;

namespace UselessTerminal.Controls;

public sealed partial class SessionPanel : UserControl
{
    private const double DragThreshold = 6;
    /// <summary>Drag/drop payload: ordered session ids (multi-select).</summary>
    private const string SessionDragIdsFormat = "UselessTerminal.SessionIds";

    private readonly SessionStore _store = new();
    private readonly HashSet<string> _selectedSessionIds = new(StringComparer.OrdinalIgnoreCase);
    private SessionTreeNode? _dragSource;
    private Point _mouseDown;
    /// <summary>While true, ignore SelectedItemChanged so drag-hover does not collapse multi-select.</summary>
    private bool _isDragInProgress;
    /// <summary>Session ids being dragged; valid until DoDragDrop returns (Drop runs while this is set).</summary>
    private List<string>? _dragSessionIdsSnapshot;

    /// <summary>Second argument: true = elevated external console (UAC), false = embedded terminal tab.</summary>
    public event Action<SavedSession, bool>? SessionLaunched;

    public SessionPanel()
    {
        InitializeComponent();
        _store.Load();
        _store.SeedDefaults();
        RefreshList();
    }

    private void SessionTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object?> e)
    {
        if (_isDragInProgress)
            return;

        if (SessionTree.SelectedItem is SessionTreeNode { Kind: SessionTreeNodeKind.Session, Session: { } s })
        {
            _selectedSessionIds.Clear();
            _selectedSessionIds.Add(s.Id);
            UpdateMultiSelectUi();
        }
        else if (SessionTree.SelectedItem is SessionTreeNode { Kind: SessionTreeNodeKind.Folder })
        {
            _selectedSessionIds.Clear();
            UpdateMultiSelectUi();
        }
    }

    private void UpdateMultiSelectUi()
    {
        foreach (var node in EnumerateSessionNodes(SessionTree.Items))
        {
            if (node.Kind == SessionTreeNodeKind.Session && node.Session is not null)
                node.IsMultiSelected = _selectedSessionIds.Contains(node.Session.Id);
        }
    }

    private static IEnumerable<SessionTreeNode> EnumerateSessionNodes(System.Collections.IEnumerable items)
    {
        foreach (object? o in items)
        {
            if (o is not SessionTreeNode n) continue;
            yield return n;
            foreach (var c in EnumerateSessionNodes(n.Children))
                yield return c;
        }
    }

    private IReadOnlyList<SavedSession> GetDragSessions(SessionTreeNode node)
    {
        if (node.Kind != SessionTreeNodeKind.Session || node.Session is null)
            return Array.Empty<SavedSession>();

        if (_selectedSessionIds.Count > 0 && _selectedSessionIds.Contains(node.Session.Id))
        {
            return _selectedSessionIds
                .Select(id => _store.FindById(id))
                .Where(s => s is not null)
                .Cast<SavedSession>()
                .OrderBy(s => s.FolderId ?? "")
                .ThenBy(s => s.SortOrder)
                .ToList();
        }

        return new[] { node.Session };
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
        Dispatcher.BeginInvoke(UpdateMultiSelectUi, DispatcherPriority.Loaded);
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

        if (node.Kind == SessionTreeNodeKind.Session && node.Session is not null)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                if (_selectedSessionIds.Contains(node.Session.Id))
                    _selectedSessionIds.Remove(node.Session.Id);
                else
                    _selectedSessionIds.Add(node.Session.Id);
                UpdateMultiSelectUi();
                e.Handled = true;
            }
            else
            {
                _selectedSessionIds.Clear();
                _selectedSessionIds.Add(node.Session.Id);
                UpdateMultiSelectUi();
            }
        }
        else if (node.Kind == SessionTreeNodeKind.Folder)
        {
            _selectedSessionIds.Clear();
            UpdateMultiSelectUi();
        }

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

        _dragSessionIdsSnapshot = null;
        if (data?.Kind == SessionTreeNodeKind.Session && data.Session is not null)
        {
            var sessions = GetDragSessions(data);
            if (sessions.Count > 0)
                _dragSessionIdsSnapshot = sessions.Select(s => s.Id).ToList();
        }

        var payload = new DataObject();
        payload.SetData(typeof(SessionTreeNode), data);
        if (_dragSessionIdsSnapshot is { Count: > 0 })
        {
            // Pipe-separated string round-trips reliably in WPF IDataObject (string[] often does not).
            payload.SetData(SessionDragIdsFormat, string.Join('|', _dragSessionIdsSnapshot));
        }

        _isDragInProgress = true;
        try
        {
            DragDrop.DoDragDrop(SessionTree, payload, DragDropEffects.Move);
        }
        finally
        {
            _isDragInProgress = false;
            _dragSessionIdsSnapshot = null;
        }
    }

    private void SessionTree_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragSource = null;
    }

    private void SessionTree_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(SessionTreeNode)) || e.Data.GetDataPresent(SessionDragIdsFormat))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
    }

    private void SessionTree_Drop(object sender, DragEventArgs e)
    {
        IReadOnlyList<SavedSession>? movingSessions = null;
        SessionTreeNode? srcNode = e.Data.GetData(typeof(SessionTreeNode)) as SessionTreeNode;

        // Snapshot is taken before DoDragDrop and cleared in finally after Drop — use it first (multi-select).
        if (_dragSessionIdsSnapshot is { Count: > 0 })
        {
            movingSessions = _dragSessionIdsSnapshot
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(id => _store.FindById(id))
                .Where(s => s is not null)
                .Cast<SavedSession>()
                .ToList();
        }
        else if (e.Data.GetData(SessionDragIdsFormat) is string pipe && pipe.Length > 0)
        {
            movingSessions = pipe.Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(id => _store.FindById(id))
                .Where(s => s is not null)
                .Cast<SavedSession>()
                .ToList();
        }
        else if (e.Data.GetData(SessionDragIdsFormat) is string[] ids && ids.Length > 0)
        {
            movingSessions = ids.Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(id => _store.FindById(id))
                .Where(s => s is not null)
                .Cast<SavedSession>()
                .ToList();
        }

        if (movingSessions is not { Count: > 0 } && srcNode?.Kind == SessionTreeNodeKind.Session && srcNode.Session is not null)
            movingSessions = GetDragSessions(srcNode);

        bool folderDrag = srcNode?.Kind == SessionTreeNodeKind.Folder;
        if (movingSessions is not { Count: > 0 } && !folderDrag)
            return;

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
            if (movingSessions is { Count: > 0 })
                ApplyDropSessions(movingSessions, target, topHalf);
            else if (srcNode?.Kind == SessionTreeNodeKind.Folder)
                ApplyDropFolder(srcNode, target, topHalf);
        }
        else
        {
            if (movingSessions is { Count: > 0 })
                ApplyDropSessionsRoot(movingSessions);
            else if (srcNode?.Kind == SessionTreeNodeKind.Folder)
                ApplyDropRootFolder(srcNode);
        }

        RefreshList(SearchBox.Text);
    }

    private void ApplyDropSessionsRoot(IReadOnlyList<SavedSession> moving)
    {
        if (moving.Count == 0) return;
        _store.MoveSessionsToFolder(moving, null);
    }

    private void ApplyDropRootFolder(SessionTreeNode src)
    {
        if (src.Kind == SessionTreeNodeKind.Folder && src.Folder is not null)
            _store.MoveFolderToRoot(src.Folder);
    }

    private void ApplyDropSessions(IReadOnlyList<SavedSession> moving, SessionTreeNode target, bool topHalf)
    {
        if (moving.Count == 0) return;

        if (target.Kind == SessionTreeNodeKind.Folder && target.Folder is not null)
        {
            _store.MoveSessionsToFolder(moving, target.Folder.Id);
            return;
        }

        if (target.Kind != SessionTreeNodeKind.Session || target.Session is null) return;
        if (moving.Any(m => m.Id == target.Session.Id)) return;

        if (topHalf)
            _store.MoveSessionsBeforeAnchor(moving, target.Session);
        else
            _store.MoveSessionsAfterAnchor(moving, target.Session);
    }

    private void ApplyDropFolder(SessionTreeNode src, SessionTreeNode target, bool topHalf)
    {
        if (src.Kind != SessionTreeNodeKind.Folder || src.Folder is null) return;

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

    private void ContextFolder_MoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (GetTagId(sender) is not string id) return;
        var folder = _store.FindFolderById(id);
        if (folder is null) return;
        _store.MoveFolderUp(folder);
        RefreshList(SearchBox.Text);
    }

    private void ContextFolder_MoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (GetTagId(sender) is not string id) return;
        var folder = _store.FindFolderById(id);
        if (folder is null) return;
        _store.MoveFolderDown(folder);
        RefreshList(SearchBox.Text);
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

    private void ExportSessions_Click(object sender, RoutedEventArgs e) => ExportSessions();

    private void ImportSessions_Click(object sender, RoutedEventArgs e) => ImportSessions();

    private void ContextTree_ExportSessions_Click(object sender, RoutedEventArgs e) => ExportSessions();

    private void ContextTree_ImportSessions_Click(object sender, RoutedEventArgs e) => ImportSessions();

    private void ExportSessions()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json",
            FileName = "useless-terminal-sessions.json",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            _store.ExportToFile(dlg.FileName);
            System.Windows.MessageBox.Show(
                "Sessions exported successfully.",
                "Export",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Export failed.\n\n{ex.Message}",
                "Export",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ImportSessions()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "JSON (*.json)|*.json|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog() != true) return;

        var confirm = System.Windows.MessageBox.Show(
            "Replace all sessions and folders with the contents of this file?\n\nThis cannot be undone.",
            "Import sessions",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            _store.ImportFromFile(dlg.FileName);
            RefreshList(SearchBox.Text);
            System.Windows.MessageBox.Show(
                "Sessions imported successfully.",
                "Import",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Import failed.\n\n{ex.Message}",
                "Import",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

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
