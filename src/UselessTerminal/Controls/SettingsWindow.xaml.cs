using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using UselessTerminal.Models;
using UselessTerminal.Services;

namespace UselessTerminal.Controls;

public partial class SettingsWindow : Window
{
    private AppSettings _settings;

    private static readonly (string Label, string Property)[] ColorEntries =
    [
        ("Background", nameof(AppSettings.Background)),
        ("Foreground", nameof(AppSettings.Foreground)),
        ("Cursor", nameof(AppSettings.Cursor)),
        ("Selection BG", nameof(AppSettings.SelectionBackground)),
        ("Selection FG", nameof(AppSettings.SelectionForeground)),
        ("Black", nameof(AppSettings.Black)),
        ("Red", nameof(AppSettings.Red)),
        ("Green", nameof(AppSettings.Green)),
        ("Yellow", nameof(AppSettings.Yellow)),
        ("Blue", nameof(AppSettings.Blue)),
        ("Magenta", nameof(AppSettings.Magenta)),
        ("Cyan", nameof(AppSettings.Cyan)),
        ("White", nameof(AppSettings.White)),
        ("Bright Black", nameof(AppSettings.BrightBlack)),
        ("Bright Red", nameof(AppSettings.BrightRed)),
        ("Bright Green", nameof(AppSettings.BrightGreen)),
        ("Bright Yellow", nameof(AppSettings.BrightYellow)),
        ("Bright Blue", nameof(AppSettings.BrightBlue)),
        ("Bright Magenta", nameof(AppSettings.BrightMagenta)),
        ("Bright Cyan", nameof(AppSettings.BrightCyan)),
        ("Bright White", nameof(AppSettings.BrightWhite)),
    ];

    private static readonly string[] CommonFonts =
    [
        "'Cascadia Code', 'Cascadia Mono', Consolas, 'Courier New', monospace",
        "'JetBrains Mono', Consolas, monospace",
        "'Fira Code', Consolas, monospace",
        "'Source Code Pro', Consolas, monospace",
        "Consolas, monospace",
        "'Courier New', monospace",
        "'Hack', Consolas, monospace",
        "'IBM Plex Mono', Consolas, monospace",
        "'Iosevka', Consolas, monospace",
    ];

    public SettingsWindow(AppSettings settings)
    {
        _settings = settings.Clone();
        InitializeComponent();
        PopulateFields();
    }

    private void PopulateFields()
    {
        foreach (string font in CommonFonts)
            FontFamilyBox.Items.Add(font);
        FontFamilyBox.Text = _settings.FontFamily;

        FontSizeSlider.Value = _settings.FontSize;
        FontSizeLabel.Text = _settings.FontSize.ToString();

        foreach (ComboBoxItem item in CursorStyleBox.Items)
        {
            if ((string)item.Content == _settings.CursorStyle)
            { CursorStyleBox.SelectedItem = item; break; }
        }

        CursorBlinkBox.IsChecked = _settings.CursorBlink;
        ScrollbackBox.Text = _settings.Scrollback.ToString();

        BuildColorGrid();
    }

    private void BuildColorGrid()
    {
        ColorGrid.Children.Clear();
        var prop = typeof(AppSettings);

        foreach (var (label, propName) in ColorEntries)
        {
            var info = prop.GetProperty(propName)!;
            string colorVal = (string)info.GetValue(_settings)!;

            var panel = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };

            var swatch = new Border
            {
                Width = 24, Height = 24,
                CornerRadius = new CornerRadius(4),
                BorderBrush = new SolidColorBrush(ParseColor("#555555")),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(ParseColor(colorVal)),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = propName
            };

            var textBox = new TextBox
            {
                Text = colorVal,
                Width = 80,
                Padding = new Thickness(6, 4, 6, 4),
                Background = new SolidColorBrush(Colors.Black),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(ParseColor("#555555")),
                BorderThickness = new Thickness(1),
                FontSize = 12,
                Tag = propName,
                VerticalAlignment = VerticalAlignment.Center
            };

            textBox.TextChanged += ColorTextBox_Changed;

            var lbl = new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                Width = 100
            };

            panel.Children.Add(lbl);
            panel.Children.Add(swatch);
            panel.Children.Add(textBox);

            ColorGrid.Children.Add(panel);
        }
    }

    private void ColorTextBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox tb || tb.Tag is not string propName) return;
        string val = tb.Text.Trim();
        if (!val.StartsWith('#') || (val.Length != 7 && val.Length != 4)) return;

        try
        {
            var color = ParseColor(val);
            var info = typeof(AppSettings).GetProperty(propName);
            info?.SetValue(_settings, val);

            var parent = (DockPanel)tb.Parent;
            foreach (var child in parent.Children)
            {
                if (child is Border b && b.Tag is string t && t == propName)
                {
                    b.Background = new SolidColorBrush(color);
                    break;
                }
            }
        }
        catch { }
    }

    private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FontSizeLabel is not null)
            FontSizeLabel.Text = ((int)e.NewValue).ToString();
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        _settings = new AppSettings();
        PopulateFields();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.FontFamily = FontFamilyBox.Text;
        _settings.FontSize = (int)FontSizeSlider.Value;
        _settings.CursorBlink = CursorBlinkBox.IsChecked == true;
        _settings.CursorStyle = (CursorStyleBox.SelectedItem as ComboBoxItem)?.Content as string ?? "bar";
        if (int.TryParse(ScrollbackBox.Text, out int sb) && sb > 0)
            _settings.Scrollback = sb;

        SettingsStore.Instance.Apply(_settings);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 3)
            hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
        byte r = Convert.ToByte(hex[..2], 16);
        byte g = Convert.ToByte(hex[2..4], 16);
        byte b = Convert.ToByte(hex[4..6], 16);
        return Color.FromRgb(r, g, b);
    }
}
