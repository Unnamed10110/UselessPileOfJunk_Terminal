using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using FormsDialogResult = System.Windows.Forms.DialogResult;
using WinFormsColorDialog = System.Windows.Forms.ColorDialog;
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

        ShellBgPathBox.Text = _settings.ShellBackgroundImagePath;
        ShellBgOpacitySlider.Value = Math.Clamp(_settings.ShellBackgroundImageOpacity * 100, 0, 100);
        ShellBgOpacityLabel.Text = $"{(int)ShellBgOpacitySlider.Value}%";

        BuildColorGrid();
    }

    private void ShellBgOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ShellBgOpacityLabel is not null)
            ShellBgOpacityLabel.Text = $"{(int)e.NewValue}%";
    }

    private void BrowseShellBg_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp|All files|*.*",
        };
        if (dlg.ShowDialog() == true)
            ShellBgPathBox.Text = dlg.FileName;
    }

    private void ClearShellBg_Click(object sender, RoutedEventArgs e)
    {
        ShellBgPathBox.Text = "";
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
                Tag = propName,
                ToolTip = "Click to open color palette"
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
            swatch.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                OpenColorPalette(propName, swatch, textBox);
            };

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

    private void OpenColorPalette(string propName, Border swatch, System.Windows.Controls.TextBox hexBox)
    {
        var info = typeof(AppSettings).GetProperty(propName);
        if (info is null) return;

        string current = (string)info.GetValue(_settings)!;
        System.Windows.Media.Color wpfColor;
        try { wpfColor = ParseColor(current); }
        catch { wpfColor = Colors.White; }

        var dlg = new WinFormsColorDialog
        {
            Color = System.Drawing.Color.FromArgb(wpfColor.R, wpfColor.G, wpfColor.B),
            FullOpen = true,
            SolidColorOnly = false,
        };

        var owner = new WpfWin32Window(this);
        if (dlg.ShowDialog(owner) != FormsDialogResult.OK)
            return;

        System.Drawing.Color d = dlg.Color;
        string hex = $"#{d.R:X2}{d.G:X2}{d.B:X2}";
        info.SetValue(_settings, hex);
        swatch.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(d.R, d.G, d.B));
        hexBox.Text = hex;
    }

    private sealed class WpfWin32Window : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle { get; }
        public WpfWin32Window(Window window) =>
            Handle = new WindowInteropHelper(window).EnsureHandle();
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

        _settings.ShellBackgroundImagePath = ShellBgPathBox.Text.Trim();
        _settings.ShellBackgroundImageOpacity = Math.Clamp(ShellBgOpacitySlider.Value / 100.0, 0, 1);

        SettingsStore.Instance.Apply(_settings);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static System.Windows.Media.Color ParseColor(string hex)
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
