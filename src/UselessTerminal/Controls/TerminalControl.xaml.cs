using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using UselessTerminal.Services;

namespace UselessTerminal.Controls;

public sealed partial class TerminalControl : UserControl, IDisposable
{
    private const int MaxBackgroundImageBytes = 15 * 1024 * 1024;

    private static readonly JsonSerializerOptions TerminalJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private ConPtySession? _session;
    private bool _webViewReady;
    private readonly Queue<byte[]> _pendingOutput = new();
    private readonly Queue<string> _webWriteQueue = new();
    private bool _webWritePumpRunning;
    private bool _disposed;
    private (string Command, string? WorkingDirectory, short Cols, short Rows)? _pendingSessionStart;
    private short _lastKnownCols = 120;
    private short _lastKnownRows = 30;
    private bool _hasReceivedResize;

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
        Loaded += TerminalControl_Loaded;
        GotFocus += (_, _) => PaneFocused?.Invoke(this);
        IsKeyboardFocusWithinChanged += (_, e) =>
        {
            if (e.NewValue is true)
                PaneFocused?.Invoke(this);
        };
    }

    /// <summary>
    /// WebView2 must not run <see cref="EnsureCoreWebView2Async"/> synchronously inside the first Loaded callback —
    /// that pattern commonly yields a blank/hung surface on some machines. Wait until application idle.
    /// </summary>
    private void TerminalControl_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= TerminalControl_Loaded;
        Dispatcher.BeginInvoke(new Action(() => _ = InitializeWebViewDeferredAsync()), DispatcherPriority.ApplicationIdle);
    }

    private async Task InitializeWebViewDeferredAsync()
    {
        try
        {
            await InitializeWebView();
        }
        catch (Exception ex)
        {
            try
            {
                System.Windows.MessageBox.Show(
                    $"Terminal WebView failed to start.\n\n{ex.Message}",
                    "Useless Terminal",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            catch { /* ignore */ }
        }
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
        WebView.DefaultBackgroundColor = System.Drawing.Color.Black;

        var settings = WebView.CoreWebView2.Settings;
        settings.AreDefaultContextMenusEnabled = false;
#if DEBUG
        settings.AreDevToolsEnabled = true;
#else
        settings.AreDevToolsEnabled = false;
#endif
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;

        WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        string assetsPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets");
        if (!Directory.Exists(assetsPath) || !File.Exists(Path.Combine(assetsPath, "terminal.html")))
        {
            throw new DirectoryNotFoundException(
                $"Missing Assets next to the executable (expected terminal.html):\n{assetsPath}");
        }

        WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "terminal.local", assetsPath, CoreWebView2HostResourceAccessKind.Allow);

        WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;

        WebView.CoreWebView2.Navigate("https://terminal.local/terminal.html");
    }

    private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess || _disposed) return;
        _ = RefitTerminalMetricsAfterNavigationAsync();
    }

    private async Task RefitTerminalMetricsAfterNavigationAsync()
    {
        await Task.Delay(60);
        RequestTerminalReflow();
        await Task.Delay(140);
        RequestTerminalReflow();
    }

    /// <summary>Re-runs FitAddon + notifies host — safe before <c>ready</c> (e.g. NavigationCompleted).</summary>
    public void RequestTerminalReflow()
    {
        if (_disposed) return;
        _ = Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                if (_disposed || WebView.CoreWebView2 is null) return;
                await WebView.ExecuteScriptAsync("try{if(window.termReflowFit)window.termReflowFit();}catch(e){}");
            }
            catch { /* torn down */ }
        }, DispatcherPriority.Background);
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            string json = args.TryGetWebMessageAsString();
            var msg = JsonSerializer.Deserialize<TerminalMessage>(json, TerminalJsonOptions);
            if (msg is null) return;

            switch (msg.type)
            {
                case "ready":
                    _webViewReady = true;
                    ApplySettings(SettingsStore.Instance.Current);
                    TryStartPendingSession();
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
                    {
                        _lastKnownCols = (short)msg.cols;
                        _lastKnownRows = (short)msg.rows;
                        _hasReceivedResize = true;
                        if (_session is not null)
                        {
                            _session.Resize(_lastKnownCols, _lastKnownRows);
                        }
                        else
                        {
                            TryStartPendingSession();
                        }
                    }
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

    private string? _shellCommand;

    public void StartSession(string command, string? workingDirectory = null, short cols = 120, short rows = 30)
    {
        _session?.Dispose();
        _session = null;

        // First-tab race fix:
        // if ConPTY starts before WebView has reported its real size, the shell paints with
        // a temporary geometry then gets resized moments later, which can leave garbled rows
        // in some prompt UIs (PSReadLine list/history, clear). Start only once ready+resized.
        if (!_webViewReady || !_hasReceivedResize)
        {
            _pendingSessionStart = (command, workingDirectory, cols, rows);
            _shellCommand = command;
            CurrentWorkingDirectory = workingDirectory;
            return;
        }

        StartSessionInternal(command, workingDirectory, ResolveInitialCols(cols), ResolveInitialRows(rows));
    }

    private void TryStartPendingSession()
    {
        if (!_webViewReady || !_hasReceivedResize) return;
        if (_pendingSessionStart is null) return;
        var pending = _pendingSessionStart.Value;
        _pendingSessionStart = null;
        StartSessionInternal(
            pending.Command,
            pending.WorkingDirectory,
            ResolveInitialCols(pending.Cols),
            ResolveInitialRows(pending.Rows));
    }

    private short ResolveInitialCols(short fallback) => _hasReceivedResize && _lastKnownCols > 0 ? _lastKnownCols : fallback;
    private short ResolveInitialRows(short fallback) => _hasReceivedResize && _lastKnownRows > 0 ? _lastKnownRows : fallback;

    private void StartSessionInternal(string command, string? workingDirectory, short cols, short rows)
    {
        // For PowerShell we inject our shell-integration script via -EncodedCommand at
        // launch time. This is far more reliable than writing to stdin afterwards: it
        // can't race with PSReadLine, the user's profile, or oh-my-posh.
        string launchCommand = MaybeInjectPowerShellArgs(command);

        _session = new ConPtySession();
        _session.OutputReceived += OnOutputReceived;
        _session.ProcessExited += OnProcessExited;
        _session.Start(launchCommand, workingDirectory, cols, rows, ExtraEnvironment);
        _shellCommand = command;
        CurrentWorkingDirectory = workingDirectory;
        InjectShellIntegrationAsync();
    }

    /// <summary>
    /// Most shells don't emit OSC 7 (cwd) by default. For cmd and bash we send a one-line
    /// init through stdin. PowerShell is handled at launch time via
    /// <see cref="MaybeInjectPowerShellArgs"/> which appends -NoExit -EncodedCommand
    /// — far more reliable than racing PSReadLine over stdin.
    /// </summary>
    private void InjectShellIntegrationAsync()
    {
        if (string.IsNullOrWhiteSpace(_shellCommand)) return;
        string lower = _shellCommand.ToLowerInvariant();

        // PowerShell init is baked into the launch command, nothing to do via stdin.
        if (lower.Contains("pwsh") || lower.Contains("powershell")) return;

        string? init = null;

        if (lower.EndsWith("cmd.exe") || lower.EndsWith("cmd.exe\"") || lower.EndsWith("\\cmd") || lower == "cmd" || lower.Contains("\\cmd.exe"))
        {
            // Use cmd's $e for ESC; emit OSC 7 then the standard prompt; clear screen.
            init = "prompt $e]7;file:///$P$e\\$P$G & cls\r\n";
        }
        else if (lower.Contains("bash") || lower.Contains("\\sh.exe") || lower.EndsWith("/sh") || lower.EndsWith("/zsh") || lower.Contains("zsh"))
        {
            // Convert backslashes to forward slashes for file:// URL.
            init = "PROMPT_COMMAND='printf \"\\033]7;file://%s\\007\" \"${PWD//\\\\//}\"'\nclear\n";
        }

        if (init is null) return;

        Task.Delay(700).ContinueWith(_ =>
        {
            try
            {
                if (!_disposed && _session is not null)
                    _session.WriteInput(init);
            }
            catch { }
        }, TaskScheduler.Default);
    }

    /// <summary>
    /// If the launch command is a bare PowerShell (pwsh / powershell, no -File / -Command /
    /// -EncodedCommand args), append <c>-NoExit -EncodedCommand &lt;base64&gt;</c> so our
    /// shell-integration script runs synchronously at startup right after the user's
    /// profile loads. This is much more reliable than writing to stdin: there's no race
    /// with PSReadLine, no echo-back of injected text, and oh-my-posh / starship can't
    /// clobber the prompt because we wrap it AFTER they install theirs.
    /// </summary>
    private static string MaybeInjectPowerShellArgs(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return command;
        string trimmed = command.Trim();

        string exe;
        string args;
        if (trimmed.StartsWith('"'))
        {
            int end = trimmed.IndexOf('"', 1);
            if (end <= 0) return command;
            exe = trimmed[1..end];
            args = trimmed[(end + 1)..].Trim();
        }
        else
        {
            int sp = trimmed.IndexOf(' ');
            if (sp < 0) { exe = trimmed; args = ""; }
            else { exe = trimmed[..sp]; args = trimmed[(sp + 1)..].Trim(); }
        }

        string exeLower = exe.ToLowerInvariant();
        bool isPwsh =
            exeLower.EndsWith("pwsh.exe") ||
            exeLower.EndsWith("powershell.exe") ||
            exeLower.EndsWith("\\pwsh") ||
            exeLower.EndsWith("\\powershell") ||
            exeLower == "pwsh" ||
            exeLower == "powershell";
        if (!isPwsh) return command;

        // Don't override the user's explicit script / command flags.
        string argsLower = args.ToLowerInvariant();
        if (argsLower.Contains("-file") ||
            argsLower.Contains("-command") ||
            argsLower.Contains("-encodedcommand") ||
            argsLower.Contains(" -c ") ||
            argsLower.StartsWith("-c "))
        {
            return command;
        }

        // PowerShell expects -EncodedCommand to be UTF-16LE base64.
        string b64 = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(BuildPowerShellInitScript()));

        string injected = string.IsNullOrEmpty(args)
            ? $"-NoExit -EncodedCommand {b64}"
            : $"{args} -NoExit -EncodedCommand {b64}";

        return $"\"{exe}\" {injected}";
    }

    private static string BuildPowerShellInitScript()
    {
        // Compatible with both Windows PowerShell 5.1 and PowerShell 7+ (uses [char]27/7
        // instead of `e). The OSC 7 escape is *prepended to the prompt return value* so
        // the host writes one combined string — no race with [Console]::Write timing,
        // no reliance on Write-Host. We also re-detect on every prompt so a profile
        // that re-assigns $function:prompt later is wrapped automatically next time.
        return @"
$ErrorActionPreference = 'SilentlyContinue'
$global:__utE = [string][char]27
$global:__utB = [string][char]7

function global:__utWrap {
    $cur = (Get-Item Function:prompt -ErrorAction SilentlyContinue).ScriptBlock
    if ($cur -and $cur.ToString() -match '__utEmitCwdMarker') { return }
    if ($cur) { $global:__utOrigPrompt = $cur }
    function global:prompt {
        # __utEmitCwdMarker
        $p = $PWD.Path -replace '\\','/'
        $osc = $global:__utE + ']7;file:///' + $p + $global:__utB
        $orig = ''
        if ($global:__utOrigPrompt) {
            try { $orig = & $global:__utOrigPrompt } catch { $orig = 'PS ' + $PWD.Path + '> ' }
        } else {
            $orig = 'PS ' + $PWD.Path + '> '
        }
        $osc + ([string]$orig)
    }
}

__utWrap

# Backup: re-apply the wrap on every idle in case a user profile or module reassigns
# $function:prompt after we ran (oh-my-posh / starship / Import-Module posh-git etc.).
if (-not $global:__utOnIdleRegistered) {
    try {
        $null = Register-EngineEvent -SourceIdentifier PowerShell.OnIdle -Action { __utWrap }
        $global:__utOnIdleRegistered = $true
    } catch {}
}

# Emit the initial cwd so the status bar populates without waiting for a prompt fire.
[Console]::Write($global:__utE + ']7;file:///' + ($PWD.Path -replace '\\','/') + $global:__utB)
";
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
        string arg = JsonSerializer.Serialize(Convert.ToBase64String(data));
        lock (_webWriteQueue)
            _webWriteQueue.Enqueue(arg);
        PumpWebWriteQueue();
    }

    private void PumpWebWriteQueue()
    {
        Dispatcher.InvokeAsync(async () =>
        {
            if (_disposed) return;

            lock (_webWriteQueue)
            {
                if (_webWritePumpRunning) return;
                _webWritePumpRunning = true;
            }

            try
            {
                while (!_disposed)
                {
                    string? next = null;
                    lock (_webWriteQueue)
                    {
                        if (_webWriteQueue.Count > 0)
                            next = _webWriteQueue.Dequeue();
                    }

                    if (next is null) break;

                    try
                    {
                        await WebView.ExecuteScriptAsync($"window.termWrite({next})");
                    }
                    catch { /* WebView disposed */ }
                }
            }
            finally
            {
                lock (_webWriteQueue)
                    _webWritePumpRunning = false;

                lock (_webWriteQueue)
                {
                    if (_webWriteQueue.Count > 0 && !_disposed)
                        PumpWebWriteQueue();
                }
            }
        }, DispatcherPriority.Normal);
    }

    private void OnProcessExited()
    {
        Dispatcher.InvokeAsync(() =>
        {
            PumpWebWriteQueue();
            TitleChanged?.Invoke("[Process Exited]");
        });
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
        int fontWeight = Math.Clamp(settings.FontWeight, 100, 900);
        int fontWeightBold = Math.Min(900, fontWeight + 250);

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
            ["fontWeight"] = fontWeight,
            ["fontWeightBold"] = fontWeightBold,
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
