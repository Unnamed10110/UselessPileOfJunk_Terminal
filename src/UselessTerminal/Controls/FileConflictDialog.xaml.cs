using System.Windows;
using UselessTerminal.Services;

namespace UselessTerminal.Controls;

internal partial class FileConflictDialog : Window
{
    public FileConflictChoice Choice { get; private set; } = FileConflictChoice.Cancel;

    public FileConflictDialog(IReadOnlyList<string> names, string destination)
    {
        InitializeComponent();

        bool many = names.Count > 1;
        Headline.Text = many
            ? $"{names.Count} items already exist in the destination."
            : "A file with this name already exists.";
        DestinationLabel.Text = destination;
        NamesLabel.Text = FormatNames(names);

        string sample = names[0];
        string renamed = ShellFileDrop.SuggestRename(sample);
        RenameHint.Text = many
            ? $"Rename keeps both copies (for example “{renamed}”). Replace overwrites the existing items."
            : $"Rename keeps both copies as “{renamed}”. Replace overwrites the existing item.";
    }

    public static FileConflictChoice Ask(Window? owner, IReadOnlyList<string> names, string destination)
    {
        if (names.Count == 0) return FileConflictChoice.Rename;
        var dlg = new FileConflictDialog(names, destination);
        if (owner is not null && owner.IsLoaded)
            dlg.Owner = owner;
        return dlg.ShowDialog() == true ? dlg.Choice : FileConflictChoice.Cancel;
    }

    private static string FormatNames(IReadOnlyList<string> names)
    {
        if (names.Count <= 6) return string.Join("\n", names);
        return string.Join("\n", names.Take(5)) + $"\n… and {names.Count - 5} more";
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        Choice = FileConflictChoice.Rename;
        DialogResult = true;
        Close();
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        Choice = FileConflictChoice.Replace;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Choice = FileConflictChoice.Cancel;
        DialogResult = false;
        Close();
    }
}
