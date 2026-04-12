using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace UselessTerminal.Controls;

public partial class CommandPaletteDialog : Window
{
    private readonly List<PaletteEntry> _allEntries;
    private bool _closing;

    public string? SelectedActionId { get; private set; }

    public CommandPaletteDialog(IEnumerable<PaletteEntry> entries)
    {
        _allEntries = entries.ToList();
        InitializeComponent();
        Loaded += (_, _) =>
        {
            SearchBox.Focus();
            Filter("");
        };
        Deactivated += (_, _) =>
        {
            if (!_closing)
            {
                _closing = true;
                DialogResult = false;
            }
        };
    }

    private void Filter(string query)
    {
        var q = query.Trim().ToLowerInvariant();
        var filtered = string.IsNullOrEmpty(q)
            ? _allEntries
            : _allEntries.Where(e => e.Label.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        ResultsList.Items.Clear();
        foreach (var e in filtered)
            ResultsList.Items.Add(e);

        if (ResultsList.Items.Count > 0)
            ResultsList.SelectedIndex = 0;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        => Filter(SearchBox.Text);

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (!_closing)
            {
                _closing = true;
                DialogResult = false;
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            AcceptSelection();
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            if (ResultsList.Items.Count > 0)
            {
                int i = ResultsList.SelectedIndex + 1;
                if (i >= ResultsList.Items.Count) i = 0;
                ResultsList.SelectedIndex = i;
                ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (ResultsList.Items.Count > 0)
            {
                int i = ResultsList.SelectedIndex - 1;
                if (i < 0) i = ResultsList.Items.Count - 1;
                ResultsList.SelectedIndex = i;
                ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            }
            e.Handled = true;
        }
    }

    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        => AcceptSelection();

    private void AcceptSelection()
    {
        if (_closing) return;
        if (ResultsList.SelectedItem is PaletteEntry entry)
        {
            _closing = true;
            SelectedActionId = entry.Id;
            DialogResult = true;
        }
    }
}

public sealed class PaletteEntry
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Shortcut { get; init; } = "";
}
