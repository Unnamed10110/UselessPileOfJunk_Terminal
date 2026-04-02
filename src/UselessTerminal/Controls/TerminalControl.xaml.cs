using System.Text;
using System.Text.Json;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using UselessTerminal.Services;

namespace UselessTerminal.Controls;

public sealed partial class TerminalControl : UserControl, IDisposable
{
    private ConPtySession? _session;
    private bool _webViewReady;
    private readonly Queue<byte[]> _pendingOutput = new();
    private bool _disposed;

    public event Action<string>? TitleChanged;
    public event Action<TerminalControl>? PaneFocused;

    public TerminalControl()
    {
        InitializeComponent();
        Loaded += async (_, _) => await InitializeWebView();
        GotFocus += (_, _) => PaneFocused?.Invoke(this);
        IsKeyboardFocusWithinChanged += (_, e) =>
        {
            if (e.NewValue is true)
                PaneFocused?.Invoke(this);
        };
    }

    public void SetFocusIndicator(bool focused)
    {
        FocusBorder.BorderBrush = focused
            ? new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(180, 255, 43, 123))
            : System.Windows.Media.Brushes.Transparent;
        DimOverlay.Visibility = focused
            ? System.Windows.Visibility.Collapsed
            : System.Windows.Visibility.Visible;
    }

    private async Task InitializeWebView()
    {
        string userDataFolder = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UselessTerminal", "WebView2");

        var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
        await WebView.EnsureCoreWebView2Async(env);

        var settings = WebView.CoreWebView2.Settings;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDevToolsEnabled = true;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;

        WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        string assetsPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets");
        WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "terminal.local", assetsPath, CoreWebView2HostResourceAccessKind.Allow);

        WebView.CoreWebView2.Navigate("https://terminal.local/terminal.html");
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            string json = args.TryGetWebMessageAsString();
            var msg = JsonSerializer.Deserialize<TerminalMessage>(json);
            if (msg is null) return;

            switch (msg.type)
            {
                case "ready":
                    _webViewReady = true;
                    ApplySettings(SettingsStore.Instance.Current);
                    FlushPendingOutput();
                    break;

                case "input":
                    if (msg.data is not null)
                        _session?.WriteInput(msg.data);
                    break;

                case "binary":
                    if (msg.data is not null)
                    {
                        var bytes = Encoding.Latin1.GetBytes(msg.data);
                        _session?.WriteInput(bytes);
                    }
                    break;

                case "resize":
                    if (msg.cols > 0 && msg.rows > 0)
                        _session?.Resize((short)msg.cols, (short)msg.rows);
                    break;

                case "title":
                    TitleChanged?.Invoke(msg.data ?? "Terminal");
                    break;
            }
        }
        catch { }
    }

    public void StartSession(string command, string? workingDirectory = null, short cols = 120, short rows = 30)
    {
        _session?.Dispose();
        _session = new ConPtySession();
        _session.OutputReceived += OnOutputReceived;
        _session.ProcessExited += OnProcessExited;
        _session.Start(command, workingDirectory, cols, rows);
    }

    private void OnOutputReceived(byte[] data)
    {
        if (_webViewReady)
        {
            WriteToTerminal(data);
        }
        else
        {
            lock (_pendingOutput)
                _pendingOutput.Enqueue(data);
        }
    }

    private void FlushPendingOutput()
    {
        lock (_pendingOutput)
        {
            while (_pendingOutput.Count > 0)
                WriteToTerminal(_pendingOutput.Dequeue());
        }
    }

    private void WriteToTerminal(byte[] data)
    {
        string base64 = Convert.ToBase64String(data);
        Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                if (!_disposed)
                    await WebView.ExecuteScriptAsync($"window.termWrite('{base64}')");
            }
            catch { }
        });
    }

    private void OnProcessExited()
    {
        Dispatcher.InvokeAsync(() => TitleChanged?.Invoke("[Process Exited]"));
    }

    public void ApplySettings(Models.AppSettings settings)
    {
        if (!_webViewReady || _disposed) return;
        string themeJson = settings.ToThemeJson();
        string settingsJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            fontFamily = settings.FontFamily,
            fontSize = settings.FontSize,
            cursorBlink = settings.CursorBlink,
            cursorStyle = settings.CursorStyle,
            scrollback = settings.Scrollback,
            theme = System.Text.Json.JsonSerializer.Deserialize<object>(themeJson)
        });
        string escaped = settingsJson.Replace("'", "\\'");
        Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                if (!_disposed)
                    await WebView.ExecuteScriptAsync($"window.termApplySettings('{escaped}')");
            }
            catch { }
        });
    }

    public void SendCommand(string command)
    {
        if (_session is null || _disposed) return;
        _session.WriteInput(command + "\r");
    }

    public void FocusTerminal()
    {
        Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                WebView.Focus();
                if (_webViewReady && !_disposed)
                    await WebView.ExecuteScriptAsync("window.termFocus()");
            }
            catch { }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session?.Dispose();
        _session = null;
    }

    private record TerminalMessage(string type, string? data, int cols, int rows);
}
