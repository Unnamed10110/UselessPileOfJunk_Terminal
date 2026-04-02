using System.Windows;
using System.Windows.Input;

namespace UselessTerminal.Controls;

public partial class RenameDialog : Window
{
    public string ResultName { get; private set; } = "";

    public RenameDialog(string currentName)
    {
        InitializeComponent();
        NameBox.Text = currentName;
        Loaded += (_, _) => { NameBox.SelectAll(); NameBox.Focus(); };
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Accept();
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    private void NameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Accept();
        else if (e.Key == Key.Escape) { DialogResult = false; Close(); }
    }

    private void Accept()
    {
        ResultName = NameBox.Text.Trim();
        if (string.IsNullOrEmpty(ResultName)) return;
        DialogResult = true;
        Close();
    }
}
