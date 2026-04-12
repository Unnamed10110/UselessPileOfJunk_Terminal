using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using UselessTerminal.Services;

namespace UselessTerminal.Controls;

public sealed partial class TerminalControl : UserControl, IDisposable
{
    private const int MaxBackgroundImageBytes = 15 * 1024 * 1024;

    private ConPtySession? _session;
    private bool _webViewReady;
    private readonly Queue<byte[]> _pendingOutput = new();
    private bool _disposed;

    public event Action<string>? TitleChanged;
    public event Action<TerminalControl>? PaneFocused;
    public event Action? BellRang;
    public event Action<string>? BufferExportRequested;
    public event Action? OutputProduced;
    public event Action<byte[]>? RawOutputReceived;
    public event Action<string>? InputBroadcast;
    public event Action<string>? CwdChanged;
    public event Action<string>? ShellIntegrationEvent;
    public event Action<string>? SearchAllTabsRequested;

    public string? CurrentWorkingDirectory { get; private set; }

    public string? SessionThemeBackground { get; set; }
    public int SessionFontSize { get; set; }
    public bool ReadOnly { get; set; }

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
                    if (msg.data is not null && !ReadOnly)
                    {
                        _session?.WriteInput(msg.data);
                        InputBroadcast?.Invoke(msg.data);
                    }
                    break;

                case "binary":
                    if (msg.data is not null && !ReadOnly)
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

                case "fontSize":
                    if (msg.fontSize is int sz && sz >= 8 && sz <= 32)
                        SettingsStore.Instance.UpdateFontSize(sz);
                    break;

                case "bell":
                    BellRang?.Invoke();
                    break;

                case "openLink":
                    if (!string.IsNullOrWhiteSpace(msg.data))
                    {
                        try { Process.Start(new ProcessStartInfo(msg.data) { UseShellExecute = true }); }
                        catch { }
                    }
                    break;

                case "exportBuffer":
                    if (msg.data is not null)
                        BufferExportRequested?.Invoke(msg.data);
                    break;

                case "cwd":
                    if (!string.IsNullOrWhiteSpace(msg.data))
                    {
                        CurrentWorkingDirectory = msg.data;
                        CwdChanged?.Invoke(msg.data);
                    }
                    break;

                case "osc133":
                    if (!string.IsNullOrWhiteSpace(msg.data))
                        ShellIntegrationEvent?.Invoke(msg.data);
                    break;

                case "openFile":
                    if (!string.IsNullOrWhiteSpace(msg.data))
                        OpenFileInEditor(msg.data);
                    break;

                case "searchAllTabs":
                    if (!string.IsNullOrWhiteSpace(msg.data))
                        SearchAllTabsRequested?.Invoke(msg.data);
                    break;
            }
        }
        catch { }
    }

    public Dictionary<string, string>? ExtraEnvironment { get; set; }

    public void StartSession(string command, string? workingDirectory = null, short cols = 120, short rows = 30)
    {
        _session?.Dispose();
        _session = new ConPtySession();
        _session.OutputReceived += OnOutputReceived;
        _session.ProcessExited += OnProcessExited;
        _session.Start(command, workingDirectory, cols, rows, ExtraEnvironment);
    }

    private void OnOutputReceived(byte[] data)
    {
        OutputProduced?.Invoke();
        RawOutputReceived?.Invoke(data);
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

        string? dataUrl = null;
        string path = settings.ShellBackgroundImagePath?.Trim() ?? "";
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length <= MaxBackgroundImageBytes)
                {
                    string mime = GuessImageMime(path);
                    dataUrl = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
                }
            }
            catch { }
        }

        string themeJson = settings.ToThemeJson();
        var themeEl = JsonSerializer.Deserialize<JsonElement>(themeJson);

        int fontSize = SessionFontSize > 0 ? SessionFontSize : settings.FontSize;

        JsonElement finalTheme = themeEl;
        if (!string.IsNullOrWhiteSpace(SessionThemeBackground))
        {
            var themeDict = JsonSerializer.Deserialize<Dictionary<string, object>>(themeJson) ?? new();
            themeDict["background"] = SessionThemeBackground!;
            var overridden = JsonSerializer.Serialize(themeDict);
            finalTheme = JsonSerializer.Deserialize<JsonElement>(overridden);
        }

        var payload = new Dictionary<string, object?>
        {
            ["fontFamily"] = settings.FontFamily,
            ["fontSize"] = fontSize,
            ["cursorBlink"] = settings.CursorBlink,
            ["cursorStyle"] = settings.CursorStyle,
            ["scrollback"] = settings.Scrollback,
            ["theme"] = finalTheme,
            ["backgroundImageDataUrl"] = dataUrl ?? "",
            ["backgroundImageOpacity"] = settings.ShellBackgroundImageOpacity,
            ["useBackgroundImage"] = !string.IsNullOrEmpty(dataUrl),
        };

        string innerJson = JsonSerializer.Serialize(payload);
        Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                if (!_disposed)
                {
                    string arg = JsonSerializer.Serialize(innerJson);
                    await WebView.ExecuteScriptAsync($"window.termApplySettingsRaw({arg})");
                }
            }
            catch { }
        });
    }

    private static string GuessImageMime(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "image/png",
        };
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

    public bool IsSessionAlive => _session?.IsProcessAlive == true;

    private static void OpenFileInEditor(string pathWithLine)
    {
        try
        {
            string filePath = pathWithLine;
            int lineNumber = 0;
            int lastColon = pathWithLine.LastIndexOf(':');
            if (lastColon > 2 && int.TryParse(pathWithLine[(lastColon + 1)..], out int ln))
            {
                filePath = pathWithLine[..lastColon];
                lineNumber = ln;
            }

            if (System.IO.Directory.Exists(filePath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{filePath}\"") { UseShellExecute = false });
                return;
            }

            if (!System.IO.File.Exists(filePath))
            {
                string? dir = System.IO.Path.GetDirectoryName(filePath);
                if (dir is not null && System.IO.Directory.Exists(dir))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = false });
                }
                return;
            }

            string? codeExe = FindInPath("code");
            if (codeExe is not null)
            {
                string arg = lineNumber > 0 ? $"--goto \"{filePath}\":{lineNumber}" : $"\"{filePath}\"";
                Process.Start(new ProcessStartInfo(codeExe, arg) { UseShellExecute = false });
                return;
            }

            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }
        catch { }
    }

    private static string? FindInPath(string exe)
    {
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv is null) return null;
        foreach (string dir in pathEnv.Split(';'))
        {
            string full = System.IO.Path.Combine(dir.Trim(), exe + ".cmd");
            if (System.IO.File.Exists(full)) return full;
            full = System.IO.Path.Combine(dir.Trim(), exe + ".exe");
            if (System.IO.File.Exists(full)) return full;
        }
        return null;
    }

    public void ExecuteScript(string script)
    {
        if (!_webViewReady || _disposed) return;
        Dispatcher.InvokeAsync(async () =>
        {
            try { if (!_disposed) await WebView.ExecuteScriptAsync(script); }
            catch { }
        });
    }
    public int SessionProcessId => _session?.ProcessId ?? 0;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session?.Dispose();
        _session = null;
    }

    private record TerminalMessage(string type, string? data, int cols, int rows, int? fontSize);
}
