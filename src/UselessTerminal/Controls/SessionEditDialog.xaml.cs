using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using UselessTerminal.Models;
using UselessTerminal.Services;

namespace UselessTerminal.Controls;

public partial class SessionEditDialog : Window
{
    private readonly SavedSession _session;
    private string _selectedColor;
    private bool _presetSync;

    public SessionEditDialog(SavedSession session)
    {
        _session = session;
        _selectedColor = session.ColorTag;
        InitializeComponent();

        PopulatePresetCombo();

        NameBox.Text = session.Name;
        DescriptionBox.Text = session.Description;
        ShellPathBox.Text = session.ShellPath;
        ArgumentsBox.Text = session.Arguments;
        WorkDirBox.Text = session.WorkingDirectory;
        StartingCommandBox.Text = session.StartingCommand;

        ThemeBgBox.Text = session.ThemeBackground;
        ThemeFontSizeBox.Text = session.ThemeFontSize > 0 ? session.ThemeFontSize.ToString() : "";
        EnvVarsBox.Text = session.EnvironmentVariables;

        ApplyColorToUi(session.ColorTag);

        if (string.IsNullOrWhiteSpace(session.ShellPath))
        {
            var profiles = ShellDetector.DetectShells();
            var def = profiles.FirstOrDefault(p => p.IsDefault) ?? profiles.FirstOrDefault();
            if (def is not null)
            {
                ApplyShellProfile(def);
                _presetSync = true;
                try
                {
                    PresetShellCombo.SelectedIndex = GetPresetIndex(def);
                }
                finally
                {
                    _presetSync = false;
                }
            }
            else
                SyncPresetSelection();
        }
        else
            SyncPresetSelection();
    }

    private void PopulatePresetCombo()
    {
        PresetShellCombo.Items.Add(new ComboBoxItem { Content = "— Custom —", Tag = null });
        foreach (var p in ShellDetector.DetectShells())
            PresetShellCombo.Items.Add(new ComboBoxItem { Content = p.Name, Tag = p });
    }

    private int GetPresetIndex(ShellProfile target)
    {
        for (int i = 1; i < PresetShellCombo.Items.Count; i++)
        {
            if (PresetShellCombo.Items[i] is ComboBoxItem { Tag: ShellProfile p }
                && p.Command == target.Command
                && string.Equals(p.Arguments ?? "", target.Arguments ?? "", StringComparison.Ordinal))
                return i;
        }

        return 0;
    }

    private void SyncPresetSelection()
    {
        _presetSync = true;
        try
        {
            string path = ShellPathBox.Text.Trim();
            string args = ArgumentsBox.Text.Trim();
            int matchIndex = 0;
            if (!string.IsNullOrEmpty(path))
            {
                for (int i = 1; i < PresetShellCombo.Items.Count; i++)
                {
                    if (PresetShellCombo.Items[i] is not ComboBoxItem { Tag: ShellProfile p })
                        continue;
                    if (PathsEqual(p.Command, path) && string.Equals(p.Arguments ?? "", args, StringComparison.Ordinal))
                    {
                        matchIndex = i;
                        break;
                    }
                }
            }

            PresetShellCombo.SelectedIndex = matchIndex;
        }
        finally
        {
            _presetSync = false;
        }
    }

    private static bool PathsEqual(string a, string b)
    {
        if (string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;
        try
        {
            var fa = Path.GetFullPath(a);
            var fb = Path.GetFullPath(b);
            return string.Equals(fa, fb, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void ApplyShellProfile(ShellProfile p)
    {
        ShellPathBox.Text = p.Command;
        ArgumentsBox.Text = p.Arguments ?? "";
        ApplyColorToUi(p.Color);
    }

    private void ApplyColorToUi(string hex)
    {
        var normalized = (hex ?? "").Trim();
        if (normalized.Length > 0 && !normalized.StartsWith('#'))
            normalized = "#" + normalized;
        _selectedColor = string.IsNullOrEmpty(normalized) ? "#00ff44" : normalized;

        if (ColorBlue.Parent is Panel panel)
        {
            foreach (var child in panel.Children.OfType<RadioButton>())
            {
                if (child.Tag is not string tag) continue;
                var t = tag.Trim();
                if (string.Equals(t, _selectedColor, StringComparison.OrdinalIgnoreCase))
                {
                    child.IsChecked = true;
                    return;
                }
            }
        }

        ColorBlue.IsChecked = true;
        _selectedColor = ColorBlue.Tag as string ?? "#00ff44";
    }

    private void PresetShellCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_presetSync) return;
        if (PresetShellCombo.SelectedItem is not ComboBoxItem item || item.Tag is not ShellProfile p)
            return;
        ApplyShellProfile(p);
    }

    private void BrowseShell_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*",
            Title = "Select Shell Executable"
        };
        if (dlg.ShowDialog() == true)
        {
            ShellPathBox.Text = dlg.FileName;
            SyncPresetSelection();
        }
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
        _session.ThemeBackground = ThemeBgBox.Text.Trim();
        if (int.TryParse(ThemeFontSizeBox.Text.Trim(), out int fs) && fs >= 8 && fs <= 32)
            _session.ThemeFontSize = fs;
        else
            _session.ThemeFontSize = 0;
        _session.EnvironmentVariables = EnvVarsBox.Text.Trim();

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
