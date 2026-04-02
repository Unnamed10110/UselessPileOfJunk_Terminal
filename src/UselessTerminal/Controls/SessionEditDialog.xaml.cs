using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using UselessTerminal.Models;

namespace UselessTerminal.Controls;

public partial class SessionEditDialog : Window
{
    private readonly SavedSession _session;
    private string _selectedColor;

    public SessionEditDialog(SavedSession session)
    {
        _session = session;
        _selectedColor = session.ColorTag;
        InitializeComponent();

        NameBox.Text = session.Name;
        DescriptionBox.Text = session.Description;
        ShellPathBox.Text = session.ShellPath;
        ArgumentsBox.Text = session.Arguments;
        WorkDirBox.Text = session.WorkingDirectory;
        StartingCommandBox.Text = session.StartingCommand;
    }

    private void BrowseShell_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*",
            Title = "Select Shell Executable"
        };
        if (dlg.ShowDialog() == true)
            ShellPathBox.Text = dlg.FileName;
    }

    private void BrowseDir_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Select Working Directory" };
        if (dlg.ShowDialog() == true)
            WorkDirBox.Text = dlg.FolderName;
    }

    private void Color_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string color)
            _selectedColor = color;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text) || string.IsNullOrWhiteSpace(ShellPathBox.Text))
        {
            MessageBox.Show("Name and Shell Path are required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _session.Name = NameBox.Text.Trim();
        _session.Description = DescriptionBox.Text.Trim();
        _session.ShellPath = ShellPathBox.Text.Trim();
        _session.Arguments = ArgumentsBox.Text.Trim();
        _session.WorkingDirectory = WorkDirBox.Text.Trim();
        _session.StartingCommand = StartingCommandBox.Text.Trim();
        _session.ColorTag = _selectedColor;

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
