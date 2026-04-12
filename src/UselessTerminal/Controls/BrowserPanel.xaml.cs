using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace UselessTerminal.Controls;

public partial class BrowserPanel : UserControl
{
    private bool _initialized;

    public BrowserPanel()
    {
        InitializeComponent();
        Loaded += async (_, _) => await EnsureInitialized();
    }

    private async Task EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            string userDataFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UselessTerminal", "WebView2Browser");

            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await BrowserWebView.EnsureCoreWebView2Async(env);

            BrowserWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            BrowserWebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            BrowserWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            BrowserWebView.CoreWebView2.Settings.IsGeneralAutofillEnabled = true;
            BrowserWebView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = true;

            BrowserWebView.CoreWebView2.SourceChanged += (_, _) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    AddressBar.Text = BrowserWebView.CoreWebView2.Source;
                });
            };

            BrowserWebView.CoreWebView2.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                BrowserWebView.CoreWebView2.Navigate(args.Uri);
            };

            Navigate("https://chatgpt.com");
        }
        catch { }
    }

    public void Navigate(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        if (!url.Contains("://"))
        {
            if (url.Contains('.') && !url.Contains(' '))
                url = "https://" + url;
            else
                url = "https://www.google.com/search?q=" + Uri.EscapeDataString(url);
        }

        AddressBar.Text = url;

        if (BrowserWebView.CoreWebView2 is not null)
            BrowserWebView.CoreWebView2.Navigate(url);
    }

    private void BackBtn_Click(object sender, RoutedEventArgs e)
    {
        if (BrowserWebView.CoreWebView2?.CanGoBack == true)
            BrowserWebView.CoreWebView2.GoBack();
    }

    private void ForwardBtn_Click(object sender, RoutedEventArgs e)
    {
        if (BrowserWebView.CoreWebView2?.CanGoForward == true)
            BrowserWebView.CoreWebView2.GoForward();
    }

    private void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        BrowserWebView.CoreWebView2?.Reload();
    }

    private void AddressBar_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Navigate(AddressBar.Text.Trim());
            e.Handled = true;
        }
    }

    private void QuickLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string url)
            Navigate(url);
    }
}
