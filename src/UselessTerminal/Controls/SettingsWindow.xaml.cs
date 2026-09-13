using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        ("Terminal background", nameof(AppSettings.TerminalBackground)),
        ("Default text & prompt", nameof(AppSettings.TextDefault)),
        ("Typed input", nameof(AppSettings.ColorInput)),
        ("Muted / secondary", nameof(AppSettings.TextMuted)),
        ("Errors", nameof(AppSettings.ColorError)),
        ("Warnings", nameof(AppSettings.ColorWarning)),
        ("Commands & success", nameof(AppSettings.ColorCommand)),
        ("Info / messages", nameof(AppSettings.ColorMessage)),
        ("Paths & links", nameof(AppSettings.ColorAccent)),
        ("Highlights", nameof(AppSettings.ColorHighlight)),
        ("Cursor", nameof(AppSettings.CursorColor)),
        ("Selection BG", nameof(AppSettings.SelectionBackground)),
        ("Selection FG", nameof(AppSettings.SelectionForeground)),
    ];

    private static readonly (string Label, string Property)[] UiColorEntries =
    [
        ("UI text", nameof(AppSettings.UiForeground)),
        ("UI muted text", nameof(AppSettings.UiForegroundMuted)),
        ("UI accent", nameof(AppSettings.UiAccent)),
        ("UI highlight", nameof(AppSettings.UiHighlight)),
        ("UI success", nameof(AppSettings.UiSuccess)),
        ("UI warning", nameof(AppSettings.UiWarning)),
        ("UI error", nameof(AppSettings.UiError)),
        ("Chrome background", nameof(AppSettings.UiChromeBackground)),
        ("Folder / card fill", nameof(AppSettings.UiCardBackground)),
        ("Card border", nameof(AppSettings.UiCardBorder)),
        ("Folder selected", nameof(AppSettings.UiFolderSelectedBackground)),
        ("Tab text", nameof(AppSettings.UiTabForeground)),
        ("Selected tab fill", nameof(AppSettings.UiTabSelectedBackground)),
        ("Selected tab text", nameof(AppSettings.UiTabSelectedForeground)),
        ("Status bar", nameof(AppSettings.UiStatusBackground)),
        ("Icons", nameof(AppSettings.UiIcon)),
        ("Input fill", nameof(AppSettings.UiInputBackground)),
        ("Input text", nameof(AppSettings.UiInputForeground)),
        ("Hover fill", nameof(AppSettings.UiHoverBackground)),
        ("Splitters", nameof(AppSettings.UiSplitter)),
    ];

    public SettingsWindow(AppSettings settings)
    {
        _settings = settings.Clone();
        InitializeComponent();
        PopulateFields();
    }

    private void PopulateFields()
    {
        FontFamilyBox.Items.Clear();
        foreach (var ff in Fonts.SystemFontFamilies.OrderBy(f => f.Source))
            FontFamilyBox.Items.Add(ff.Source);
        FontFamilyBox.Text = _settings.FontFamily;

        UiFontFamilyBox.Items.Clear();
        foreach (var ff in Fonts.SystemFontFamilies.OrderBy(f => f.Source))
            UiFontFamilyBox.Items.Add(ff.Source);
        UiFontFamilyBox.Text = _settings.UiFontFamily;

        FontSizeSlider.Value = _settings.FontSize;
        FontSizeLabel.Text = _settings.FontSize.ToString();

        int fw = Math.Clamp(_settings.FontWeight, 300, 700);
        FontWeightSlider.Value = fw;
        FontWeightLabel.Text = fw.ToString();

        double ui = SettingsStore.SnapUiScale(_settings.UiScale);
        UiScaleSlider.Value = ui * 100;
        UiScaleLabel.Text = $"{(int)Math.Round(ui * 100)}%";

        int ufs = Math.Clamp(_settings.UiFontSize, 10, 22);
        UiFontSizeSlider.Value = ufs;
        UiFontSizeLabel.Text = ufs.ToString();

        int ufw = Math.Clamp(_settings.UiFontWeight, 300, 700);
        UiFontWeightSlider.Value = ufw;
        UiFontWeightLabel.Text = ufw.ToString();

        foreach (ComboBoxItem item in UiSharpnessBox.Items)
        {
            if ((string)item.Content == _settings.UiSharpness)
            { UiSharpnessBox.SelectedItem = item; break; }
        }
        if (UiSharpnessBox.SelectedItem is null)
            UiSharpnessBox.SelectedIndex = 0;

        foreach (ComboBoxItem item in CursorStyleBox.Items)
        {
            if ((string)item.Content == _settings.CursorStyle)
            { CursorStyleBox.SelectedItem = item; break; }
        }

        CursorBlinkBox.IsChecked = _settings.CursorBlink;
        ScrollbackBox.Text = _settings.Scrollback.ToString();

        foreach (ComboBoxItem item in WindowBackdropBox.Items)
        {
            if ((string)item.Content == _settings.WindowBackdrop)
            { WindowBackdropBox.SelectedItem = item; break; }
        }

        ShellBgPathBox.Text = _settings.ShellBackgroundImagePath;
        ShellBgOpacitySlider.Value = Math.Clamp(_settings.ShellBackgroundImageOpacity * 100, 0, 100);
        ShellBgOpacityLabel.Text = $"{(int)ShellBgOpacitySlider.Value}%";

        ThemePresetBox.Items.Clear();
        ThemePresetBox.Items.Add("Custom");
        foreach (var name in ThemePresets.All.Keys)
            ThemePresetBox.Items.Add(name);
        ThemePresetBox.SelectedIndex = 0;

        BuildColorGrid(ColorGrid, ColorEntries);
        BuildColorGrid(UiColorGrid, UiColorEntries);
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

    private void BuildColorGrid(UniformGrid grid, (string Label, string Property)[] entries)
    {
        grid.Children.Clear();
        var prop = typeof(AppSettings);

        foreach (var (label, propName) in entries)
        {
            var info = prop.GetProperty(propName)!;
            string colorVal = (string)info.GetValue(_settings)!;

            var panel = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var fg = TryFindResource("Ui.Foreground") as Brush ?? new SolidColorBrush(Colors.White);
            var inputBg = TryFindResource("Ui.InputBackground") as Brush ?? new SolidColorBrush(Colors.Black);
            var inputFg = TryFindResource("Ui.InputForeground") as Brush ?? new SolidColorBrush(Colors.White);
            var border = TryFindResource("Ui.CardBorder") as Brush ?? new SolidColorBrush(ParseColor("#555555"));

            var swatch = new Border
            {
                Width = 24, Height = 24,
                CornerRadius = new CornerRadius(4),
                BorderBrush = border,
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
                Background = inputBg,
                Foreground = inputFg,
                BorderBrush = border,
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
                Foreground = fg,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                Width = 100
            };

            panel.Children.Add(lbl);
            panel.Children.Add(swatch);
            panel.Children.Add(textBox);

            grid.Children.Add(panel);
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

    private void ThemePresetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemePresetBox.SelectedItem is not string name) return;
        if (name == "Custom" || !ThemePresets.All.TryGetValue(name, out var preset)) return;
        ThemePresets.ApplyTo(_settings, preset);
        BuildColorGrid(ColorGrid, ColorEntries);
        BuildColorGrid(UiColorGrid, UiColorEntries);
    }

    private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FontSizeLabel is not null)
            FontSizeLabel.Text = ((int)e.NewValue).ToString();
    }

    private void FontWeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FontWeightLabel is not null)
            FontWeightLabel.Text = ((int)e.NewValue).ToString();
    }

    private void UiScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (UiScaleLabel is not null)
            UiScaleLabel.Text = $"{(int)e.NewValue}%";
    }

    private void UiFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (UiFontSizeLabel is not null)
            UiFontSizeLabel.Text = ((int)e.NewValue).ToString();
    }

    private void UiFontWeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (UiFontWeightLabel is not null)
            UiFontWeightLabel.Text = ((int)e.NewValue).ToString();
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
        _settings.FontWeight = Math.Clamp((int)FontWeightSlider.Value, 300, 700);
        _settings.UiScale = SettingsStore.SnapUiScale(UiScaleSlider.Value / 100.0);
        _settings.UiFontFamily = string.IsNullOrWhiteSpace(UiFontFamilyBox.Text) ? "Segoe UI" : UiFontFamilyBox.Text.Trim();
        _settings.UiFontSize = Math.Clamp((int)UiFontSizeSlider.Value, 10, 22);
        _settings.UiFontWeight = Math.Clamp((int)UiFontWeightSlider.Value, 300, 700);
        _settings.UiSharpness = (UiSharpnessBox.SelectedItem as ComboBoxItem)?.Content as string ?? "Sharp";
        _settings.CursorBlink = CursorBlinkBox.IsChecked == true;
        _settings.CursorStyle = (CursorStyleBox.SelectedItem as ComboBoxItem)?.Content as string ?? "bar";
        if (int.TryParse(ScrollbackBox.Text, out int sb) && sb > 0)
            _settings.Scrollback = sb;

        _settings.ShellBackgroundImagePath = ShellBgPathBox.Text.Trim();
        _settings.ShellBackgroundImageOpacity = Math.Clamp(ShellBgOpacitySlider.Value / 100.0, 0, 1);
        _settings.WindowBackdrop = (WindowBackdropBox.SelectedItem as ComboBoxItem)?.Content as string ?? "None";

        SyncColorsFromGridIntoSettings();

        SettingsStore.Instance.Apply(_settings);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// Ensures every visible hex field is written to <see cref="_settings"/> before save
    /// (covers edge cases where TextChanged did not update the model).
    /// </summary>
    private void SyncColorsFromGridIntoSettings()
    {
        var type = typeof(AppSettings);
        SyncGrid(ColorGrid);
        SyncGrid(UiColorGrid);

        void SyncGrid(UniformGrid grid)
        {
            foreach (object? row in grid.Children)
            {
                if (row is not DockPanel panel) continue;
                foreach (object? child in panel.Children)
                {
                    if (child is not TextBox tb || tb.Tag is not string propName) continue;
                    string val = tb.Text.Trim();
                    if (!val.StartsWith('#') || (val.Length != 7 && val.Length != 4)) continue;
                    try
                    {
                        _ = ParseColor(val);
                        type.GetProperty(propName)?.SetValue(_settings, val);
                    }
                    catch { /* skip invalid */ }
                }
            }
        }
    }

    private static System.Windows.Media.Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 3)
            hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
        byte r = Convert.ToByte(hex[..2], 16);
        byte g = Convert.ToByte(hex[2..4], 16);
        byte b = Convert.ToByte(hex[4..6], 16);
        return System.Windows.Media.Color.FromRgb(r, g, b);
    }
}
