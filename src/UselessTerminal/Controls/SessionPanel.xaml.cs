using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UselessTerminal.Models;
using UselessTerminal.Services;

namespace UselessTerminal.Controls;

public sealed partial class SessionPanel : UserControl
{
    private readonly SessionStore _store = new();

    public event Action<SavedSession>? SessionLaunched;

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
        var sessions = _store.Sessions.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(filter))
            sessions = sessions.Where(s =>
                s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                s.ShellPath.Contains(filter, StringComparison.OrdinalIgnoreCase));

        SessionList.ItemsSource = sessions.OrderBy(s => s.SortOrder).ToList();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshList(SearchBox.Text);
    }

    private void SessionList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SessionList.SelectedItem is SavedSession session)
            SessionLaunched?.Invoke(session);
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
        if (sender is Button btn && btn.Tag is string id)
        {
            var session = _store.FindById(id);
            if (session is null) return;

            if (ShowEditDialog(session, "Edit Session"))
            {
                _store.Update(session);
                RefreshList(SearchBox.Text);
            }
        }
    }

    private void DeleteSession_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
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
    }

    private void ImportWt_Click(object sender, RoutedEventArgs e) => ImportWindowsTerminalProfiles();

    private bool ShowEditDialog(SavedSession session, string title)
    {
        var dialog = new SessionEditDialog(session) { Title = title };
        dialog.Owner = Window.GetWindow(this);
        return dialog.ShowDialog() == true;
    }
}
