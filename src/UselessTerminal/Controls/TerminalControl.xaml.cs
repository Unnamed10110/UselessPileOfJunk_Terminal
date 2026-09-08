using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

    private const int OutputHighWaterBytes = 8 * 1024 * 1024;
    private const int OutputLowWaterBytes = 2 * 1024 * 1024;
    private const int MaxBatchBytes = 64 * 1024;
    private const int PendingOutputCapBytes = 4 * 1024 * 1024;

    private ConPtySession? _session;
    private bool _webViewReady;
    private readonly Queue<byte[]> _pendingOutput = new();
    private readonly Queue<byte[]> _webWriteQueue = new();
    private bool _webWritePumpRunning;
    private long _webQueuedBytes;
    private readonly System.Threading.ManualResetEventSlim _drainGate = new(initialState: true);
    private long _pendingOutputBytes;
    private bool _disposed;
    private (string Command, string? WorkingDirectory, short Cols, short Rows, string? StartingCommand)? _pendingSessionStart;
    private string? _startingCommand;
    private string? _sshMuxControlPath;
    private string? _cwdHost;
    private bool _cwdFromOsc7;
    private DispatcherTimer? _dropStatusTimer;
    private bool _dropCopyInProgress;
    private short _lastKnownCols = 120;
    private short _lastKnownRows = 30;
    private bool _hasReceivedResize;
    private short _appliedPtyCols;
    private short _appliedPtyRows;

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
        WebView.AllowExternalDrop = true;
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
            ? TryFindResource("Ui.Accent") as System.Windows.Media.Brush
              ?? new System.Windows.Media.SolidColorBrush(
                  System.Windows.Media.Color.FromArgb(180, 107, 229, 255))
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
        WebView.AllowExternalDrop = true;
        WebView.DefaultBackgroundColor = HexToDrawing(SettingsStore.Instance.Current.TerminalBackground);
        WebView.ZoomFactor = 1.0;

        var settings = WebView.CoreWebView2.Settings;
        settings.AreDefaultContextMenusEnabled = false;
#if DEBUG
        settings.AreDevToolsEnabled = true;
#else
        settings.AreDevToolsEnabled = false;
#endif
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsPinchZoomEnabled = false;
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
                            if (_lastKnownCols != _appliedPtyCols || _lastKnownRows != _appliedPtyRows)
                            {
                                _session.Resize(_lastKnownCols, _lastKnownRows);
                                _appliedPtyCols = _lastKnownCols;
                                _appliedPtyRows = _lastKnownRows;
                            }
                        }
                        else
                        {
                            TryStartPendingSession();
                        }
                    }
                    break;

                case "title":
                    TitleChanged?.Invoke(msg.data ?? "Terminal");
                    MaybeUpdateCwdFromSshTitle(msg.data);
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
                        _cwdHost = msg.host;
                        _cwdFromOsc7 = true;
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

                case "fileDragOver":
                    ShowFileDropOverlay(true, ResolveDropDestination()?.Display);
                    break;

                case "fileDragLeave":
                    if (!_dropCopyInProgress)
                        HideDropStatus();
                    break;

                case "fileDrop":
                    string[] dropped = ExtractDroppedFilePaths(args);
                    if (dropped.Length == 0)
                    {
                        if (!_dropCopyInProgress)
                            HideDropStatus();
                        break;
                    }
                    CopyDroppedFiles(dropped);
                    break;
            }
        }
        catch { }
    }

    private static string[] ExtractDroppedFilePaths(CoreWebView2WebMessageReceivedEventArgs args)
    {
        var list = new List<string>();
        try
        {
            if (args.AdditionalObjects is null) return [];
            foreach (object? item in args.AdditionalObjects)
            {
                if (item is CoreWebView2File file && !string.IsNullOrWhiteSpace(file.Path))
                    list.Add(file.Path);
            }
        }
        catch { }

        return list.ToArray();
    }

    public Dictionary<string, string>? ExtraEnvironment { get; set; }

    private string? _shellCommand;

    public void StartSession(string command, string? workingDirectory = null, short cols = 120, short rows = 30, string? startingCommand = null)
    {
        _session?.Dispose();
        _session = null;
        _appliedPtyCols = 0;
        _appliedPtyRows = 0;
        _startingCommand = string.IsNullOrWhiteSpace(startingCommand) ? null : startingCommand.Trim();
        _sshMuxControlPath = null;
        _cwdHost = null;
        _cwdFromOsc7 = false;

        // First-tab race fix:
        // if ConPTY starts before WebView has reported its real size, the shell paints with
        // a temporary geometry then gets resized moments later, which can leave garbled rows
        // in some prompt UIs (PSReadLine list/history, clear). Start only once ready+resized.
        if (!_webViewReady || !_hasReceivedResize)
        {
            _pendingSessionStart = (command, workingDirectory, cols, rows, _startingCommand);
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
        _startingCommand = pending.StartingCommand;
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
        string launchCommand = MaybeInjectPowerShellArgs(command, _startingCommand, out bool startCmdBaked);
        if (startCmdBaked)
            _startingCommand = null;
        launchCommand = SshConnection.InjectControlMaster(launchCommand, out _sshMuxControlPath);

        _session = new ConPtySession();
        _session.OutputReceived += OnOutputReceived;
        _session.ProcessExited += OnProcessExited;
        _session.Start(launchCommand, workingDirectory, cols, rows, ExtraEnvironment);
        _appliedPtyCols = cols;
        _appliedPtyRows = rows;
        _shellCommand = command;
        CurrentWorkingDirectory = workingDirectory;
        InjectShellIntegrationAsync();
        ScheduleStartingCommandViaStdin();
    }

    private enum ShellKind { Unknown, PowerShell, Cmd, Posix, Wsl, Ssh }

    private static ShellKind DetectShellKind(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return ShellKind.Unknown;
        string lower = command.ToLowerInvariant();
        if (lower.Contains("pwsh") || lower.Contains("powershell")) return ShellKind.PowerShell;
        if (SshConnection.LooksLikeSsh(command)) return ShellKind.Ssh;
        if (lower.Contains("wsl.exe") || lower.Contains("\\wsl") || lower == "wsl") return ShellKind.Wsl;
        if (lower.EndsWith("cmd.exe") || lower.EndsWith("cmd.exe\"") || lower.EndsWith("\\cmd") || lower == "cmd" || lower.Contains("\\cmd.exe")) return ShellKind.Cmd;
        if (lower.Contains("bash") || lower.Contains("zsh") || lower.Contains("\\sh.exe") || lower.EndsWith("/sh")) return ShellKind.Posix;
        return ShellKind.Unknown;
    }

    /// <summary>
    /// Most shells don't emit OSC 133 (shell integration) or OSC 7 (cwd) by default. For cmd
    /// and POSIX shells (bash/zsh, including under WSL) we send a one-line init through stdin.
    /// PowerShell is handled at launch time via <see cref="MaybeInjectPowerShellArgs"/> which
    /// appends -NoExit -EncodedCommand — far more reliable than racing PSReadLine over stdin.
    /// SSH sessions are never touched here: we don't control the remote shell.
    /// </summary>
    private void InjectShellIntegrationAsync()
    {
        if (string.IsNullOrWhiteSpace(_shellCommand)) return;
        ShellKind kind = DetectShellKind(_shellCommand);

        string? init = kind switch
        {
            ShellKind.Cmd => BuildCmdInit(),
            ShellKind.Posix => BuildPosixInit(includeOsc7: true),
            ShellKind.Wsl => BuildPosixInit(includeOsc7: false),
            _ => null, // PowerShell (baked into the launch command), Ssh (never inject), Unknown.
        };

        if (init is null) return;

        if (!string.IsNullOrWhiteSpace(_startingCommand))
        {
            if (kind == ShellKind.Cmd)
            {
                init += ToConPtyInput(_startingCommand);
            }
            else
            {
                init += _startingCommand.Replace("\r\n", "\n").Replace('\r', '\n');
                if (!init.EndsWith('\n')) init += "\n";
            }
            _startingCommand = null;
        }

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

    // cmd.exe has no pre-exec hook and no way to expose ERRORLEVEL at prompt-draw time, so
    // only prompt-boundary markers (A/B) are achievable — no C (command-start) and no exit
    // code (D stays bare). $e = ESC, $e\ = ST (cmd has no BEL escape code), $P = cwd, $G = '>'.
    private static string BuildCmdInit() =>
        "set \"PROMPT=$e]133;D$e\\$e]133;A$e\\$e]7;file:///$P$e\\$P$G$s$e]133;B$e\\\" & cls\r\n";

    private static string BuildPosixInit(bool includeOsc7)
    {
        var sb = new StringBuilder();
        if (includeOsc7) sb.Append("__UT_OSC7=1; ");
        sb.Append("if [ -n \"$ZSH_VERSION\" ]; then ").Append(ZshIntegration)
          .Append(" elif [ -n \"$BASH_VERSION\" ]; then ").Append(BashIntegration)
          .Append(" fi; printf '\\033[?2004h'; clear\n");
        return sb.ToString();
    }

    private const string BashIntegration =
        "__ut_mark=0; __ut_seen=0; " +
        "__ut_pre() { case \"$BASH_COMMAND\" in __ut_precmd*) return;; esac; if [ \"$__ut_mark\" = 0 ]; then __ut_mark=1; printf '\\033]133;C\\007'; fi; }; " +
        "__ut_precmd() { local __ut_ec=$?; if [ \"$__ut_seen\" = 1 ]; then printf '\\033]133;D;%s\\007' \"$__ut_ec\"; fi; __ut_seen=1; if [ -n \"$__UT_OSC7\" ]; then printf '\\033]7;file://%s\\007' \"${PWD//\\\\//}\"; fi; __ut_mark=0; return $__ut_ec; }; " +
        "case \"$PROMPT_COMMAND\" in *__ut_precmd*) ;; *) PROMPT_COMMAND=\"__ut_precmd${PROMPT_COMMAND:+;$PROMPT_COMMAND}\";; esac; " +
        "case \"$PS1\" in *'133;A'*) ;; *) PS1='\\[\\e]133;A\\a\\]'\"$PS1\"'\\[\\e]133;B\\a\\]';; esac; " +
        "if [ -z \"$(trap -p DEBUG)\" ]; then trap '__ut_pre' DEBUG; fi;";

    private const string ZshIntegration =
        "__ut_seen=0; " +
        "__ut_precmd() { local ec=$?; [ \"$__ut_seen\" = 1 ] && printf '\\033]133;D;%s\\007' \"$ec\"; __ut_seen=1; [ -n \"$__UT_OSC7\" ] && printf '\\033]7;file://%s\\007' \"$PWD\"; return 0; }; " +
        "__ut_preexec() { printf '\\033]133;C\\007'; }; " +
        "autoload -Uz add-zsh-hook && add-zsh-hook precmd __ut_precmd && add-zsh-hook preexec __ut_preexec; " +
        "case \"$PS1\" in *'133;A'*) ;; *) PS1=$'%{\\e]133;A\\a%}'\"$PS1\"$'%{\\e]133;B\\a%}';; esac;";

    /// <summary>
    /// If the launch command is a bare PowerShell (pwsh / powershell, no -File / -Command /
    /// -EncodedCommand args), append <c>-NoExit -EncodedCommand &lt;base64&gt;</c> so our
    /// shell-integration script runs synchronously at startup right after the user's
    /// profile loads. This is much more reliable than writing to stdin: there's no race
    /// with PSReadLine, no echo-back of injected text, and oh-my-posh / starship can't
    /// clobber the prompt because we wrap it AFTER they install theirs.
    /// </summary>
    private static string MaybeInjectPowerShellArgs(string command, string? startingCommand, out bool startingCommandBaked)
    {
        startingCommandBaked = false;
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
        string b64 = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(BuildPowerShellInitScript(startingCommand)));
        startingCommandBaked = !string.IsNullOrWhiteSpace(startingCommand);

        string injected = string.IsNullOrEmpty(args)
            ? $"-NoExit -EncodedCommand {b64}"
            : $"{args} -NoExit -EncodedCommand {b64}";

        return $"\"{exe}\" {injected}";
    }

    private static string BuildPowerShellInitScript(string? startingCommand = null)
    {
        // Compatible with both Windows PowerShell 5.1 and PowerShell 7+ (uses [char]27/7
        // instead of `e). The OSC 7 escape is *prepended to the prompt return value* so
        // the host writes one combined string — no race with [Console]::Write timing,
        // no reliance on Write-Host. We also re-detect on every prompt so a profile
        // that re-assigns $function:prompt later is wrapped automatically next time.
        string baseScript = @"
$ErrorActionPreference = 'SilentlyContinue'
$global:__utE = [string][char]27
$global:__utB = [string][char]7
function global:__utApplyInputColor { }

function global:__utWrap {
    $cur = (Get-Item Function:prompt -ErrorAction SilentlyContinue).ScriptBlock
    if ($cur -and $cur.ToString() -match '__utEmitCwdMarker') { return }
    if ($cur) { $global:__utOrigPrompt = $cur }
    function global:prompt {
        # __utEmitCwdMarker
        $__utOk  = $?
        $__utLec = $global:LASTEXITCODE
        try { __utApplyInputColor } catch {}
        try { __utWrapReadLine } catch {}

        $e = $global:__utE; $b = $global:__utB
        $out = ''

        $__utHid = -1
        try { $__utH = Get-History -Count 1 -ErrorAction SilentlyContinue; if ($__utH) { $__utHid = $__utH.Id } } catch {}
        if ($global:__utPromptSeen) {
            if ($__utHid -ne -1 -and $__utHid -eq $global:__utLastHistoryId) {
                $out += $e + ']133;D' + $b
            } else {
                $__utCode = 0
                if (-not $__utOk) {
                    if ($null -ne $__utLec -and ""$__utLec"" -ne '') { $__utCode = $__utLec } else { $__utCode = 1 }
                }
                $out += $e + ']133;D;' + $__utCode + $b
            }
        }
        $global:__utPromptSeen = $true
        $global:__utLastHistoryId = $__utHid

        $out += $e + ']133;A' + $b
        $p = $PWD.Path -replace '\\','/'
        $out += $e + ']7;file:///' + $p + $b

        $orig = ''
        if ($global:__utOrigPrompt) {
            try { $orig = & $global:__utOrigPrompt } catch { $orig = 'PS ' + $PWD.Path + '> ' }
        } else {
            $orig = 'PS ' + $PWD.Path + '> '
        }
        $out += [string]$orig

        $out += $e + ']133;B' + $b
        $out
    }
}

function global:__utWrapReadLine {
    $rl = Get-Item Function:PSConsoleHostReadLine -ErrorAction SilentlyContinue
    if (-not $rl) { return }
    if ($rl.ScriptBlock.ToString() -match '__utReadLineMarker') { return }
    $global:__utOrigReadLine = $rl.ScriptBlock
    function global:PSConsoleHostReadLine {
        # __utReadLineMarker
        $line = & $global:__utOrigReadLine
        try { [Console]::Write($global:__utE + ']133;C' + $global:__utB) } catch {}
        $line
    }
}

__utWrap

# Backup: re-apply the wrap on every idle in case a user profile or module reassigns
# $function:prompt after we ran (oh-my-posh / starship / Import-Module posh-git etc.).
if (-not $global:__utOnIdleRegistered) {
    try {
        $null = Register-EngineEvent -SourceIdentifier PowerShell.OnIdle -Action {
            __utWrap
            try { __utApplyInputColor } catch {}
        }
        $global:__utOnIdleRegistered = $true
    } catch {}
}

# Emit the initial cwd so the status bar populates without waiting for a prompt fire.
[Console]::Write($global:__utE + ']7;file:///' + ($PWD.Path -replace '\\','/') + $global:__utB)

# Bracketed paste: multiline clipboard inserts at the prompt instead of running each line.
[Console]::Write($global:__utE + '[?2004h')
";
        return baseScript + BuildPsReadLineInputColorScript() + BuildPowerShellStartingCommandScript(startingCommand);
    }

    private static string BuildPowerShellStartingCommandScript(string? startingCommand)
    {
        if (string.IsNullOrWhiteSpace(startingCommand)) return "";
        string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(startingCommand.Trim()));
        return $@"
# Session starting command (after profile + shell integration).
try {{
  $__utStart = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{b64}'))
  if ($__utStart) {{ Invoke-Expression $__utStart }}
}} catch {{}}
";
    }

    private void ScheduleStartingCommandViaStdin()
    {
        if (string.IsNullOrWhiteSpace(_startingCommand)) return;
        string cmd = _startingCommand;
        _startingCommand = null;

        Task.Run(async () =>
        {
            try
            {
                for (int i = 0; i < 50; i++)
                {
                    await Task.Delay(200).ConfigureAwait(false);
                    if (_disposed) return;
                    if (_session is { IsProcessAlive: true })
                    {
                        await Task.Delay(600).ConfigureAwait(false);
                        if (_disposed || _session is null) return;
                        _session.WriteInput(ToConPtyInput(cmd));
                        return;
                    }
                }
            }
            catch { }
        });
    }

    private static string ToConPtyInput(string command)
    {
        string normalized = command.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        if (normalized.Length == 0) return "\r";
        return normalized.Replace('\n', '\r') + "\r";
    }

    private static string BuildPsReadLineInputColorScript()
    {
        var (r, g, b) = ParseRgb(SettingsStore.Instance.Current.ColorInput);
        // PSReadLine is not imported yet during -EncodedCommand; apply from prompt/OnIdle
        // with 24-bit VT (works on PSReadLine 2.x; "#RRGGBB" does not on older builds).
        return $@"
function global:__utApplyInputColor {{
  if ($global:__utInputColorApplied) {{ return }}
  try {{
    Import-Module PSReadLine -ErrorAction Stop
    $vt = $global:__utE + '[38;2;{r};{g};{b}m'
    Set-PSReadLineOption -ErrorAction Stop -Colors @{{
      Command = $vt
      Default = $vt
      Number = $vt
      Parameter = $vt
      Operator = $vt
      Member = $vt
      Variable = $vt
      Keyword = $vt
      Type = $vt
      String = $vt
    }}
    $global:__utInputColorApplied = $true
  }} catch {{}}
}}
";
    }

    private static (int r, int g, int b) ParseRgb(string? hex)
    {
        string h = SanitizeCssHex(hex).TrimStart('#');
        return (Convert.ToByte(h[..2], 16), Convert.ToByte(h[2..4], 16), Convert.ToByte(h[4..6], 16));
    }

    private static string SanitizeCssHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return "#ffffff";
        hex = hex.Trim();
        if (hex.Length == 4 && hex[0] == '#')
            hex = $"#{hex[1]}{hex[1]}{hex[2]}{hex[2]}{hex[3]}{hex[3]}";
        if (hex.Length == 7 && hex[0] == '#')
        {
            for (int i = 1; i < 7; i++)
            {
                if (!Uri.IsHexDigit(hex[i])) return "#ffffff";
            }
            return hex;
        }
        return "#ffffff";
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
            {
                if (_pendingOutputBytes < PendingOutputCapBytes)
                {
                    _pendingOutput.Enqueue(data);
                    _pendingOutputBytes += data.Length;
                }
            }
        }

        // Backpressure: only ever wait here on the reader thread, never the UI thread.
        // Blocking here stalls the PTY read loop, which is exactly the correct flow-control
        // behavior (ConPTY/conhost already block a slow-reading consumer).
        if (!Dispatcher.CheckAccess())
        {
            while (!_disposed && !_drainGate.Wait(200)) { }
        }
    }

    private void FlushPendingOutput()
    {
        lock (_pendingOutput)
        {
            while (_pendingOutput.Count > 0)
            {
                var data = _pendingOutput.Dequeue();
                _pendingOutputBytes -= data.Length;
                WriteToTerminal(data);
            }
        }
    }

    private void WriteToTerminal(byte[] data)
    {
        bool needPump;
        lock (_webWriteQueue)
        {
            _webWriteQueue.Enqueue(data);
            _webQueuedBytes += data.Length;
            if (_webQueuedBytes >= OutputHighWaterBytes) _drainGate.Reset();
            needPump = !_webWritePumpRunning;
            if (needPump) _webWritePumpRunning = true;
        }
        if (needPump) PumpWebWriteQueue();
    }

    private void PumpWebWriteQueue()
    {
        Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                while (!_disposed)
                {
                    byte[]? batch;
                    lock (_webWriteQueue)
                    {
                        if (_webWriteQueue.Count == 0)
                        {
                            _webWritePumpRunning = false;
                            return;
                        }

                        int take = 0;
                        var parts = new List<byte[]>();
                        while (_webWriteQueue.Count > 0 &&
                               (take == 0 || take + _webWriteQueue.Peek().Length <= MaxBatchBytes))
                        {
                            var c = _webWriteQueue.Dequeue();
                            parts.Add(c);
                            take += c.Length;
                        }
                        _webQueuedBytes -= take;
                        if (_webQueuedBytes <= OutputLowWaterBytes) _drainGate.Set();

                        if (parts.Count == 1)
                        {
                            batch = parts[0];
                        }
                        else
                        {
                            batch = new byte[take];
                            int offset = 0;
                            foreach (var part in parts)
                            {
                                Buffer.BlockCopy(part, 0, batch, offset, part.Length);
                                offset += part.Length;
                            }
                        }
                    }

                    try
                    {
                        // Base64 output is limited to A-Z a-z 0-9 + / = , all of which are safe
                        // to interpolate directly inside a JS double-quoted string literal.
                        string b64 = Convert.ToBase64String(batch);
                        await WebView.ExecuteScriptAsync($"window.termWrite(\"{b64}\")");
                    }
                    catch { /* WebView disposed */ }
                }
            }
            finally
            {
                bool requeue = false;
                lock (_webWriteQueue)
                {
                    if (_webWriteQueue.Count > 0 && !_disposed)
                    {
                        _webWritePumpRunning = true;
                        requeue = true;
                    }
                    else
                    {
                        _webWritePumpRunning = false;
                    }
                }
                if (requeue) PumpWebWriteQueue();
            }
        }, DispatcherPriority.Normal);
    }

    /// <summary>
    /// Starts the pump only if it isn't already running, claiming <see cref="_webWritePumpRunning"/>
    /// under the queue lock first. Unlike calling <see cref="PumpWebWriteQueue"/> directly (which no
    /// longer re-checks that flag internally — ownership is claimed by the caller so batches stay in
    /// strict FIFO order), this is safe to call from anywhere, including when a pump may already be
    /// draining the queue.
    /// </summary>
    private void EnsurePumpRunning()
    {
        bool needPump;
        lock (_webWriteQueue)
        {
            needPump = !_webWritePumpRunning;
            if (needPump) _webWritePumpRunning = true;
        }
        if (needPump) PumpWebWriteQueue();
    }

    private void OnProcessExited()
    {
        Dispatcher.InvokeAsync(() =>
        {
            EnsurePumpRunning();
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

        WebView.DefaultBackgroundColor = HexToDrawing(settings.TerminalBackground);
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
            ["colorInput"] = settings.ColorInput,
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

    private static System.Drawing.Color HexToDrawing(string? hex)
    {
        try
        {
            string h = (hex ?? "#000000").Trim().TrimStart('#');
            if (h.Length == 3)
                h = $"{h[0]}{h[0]}{h[1]}{h[1]}{h[2]}{h[2]}";
            if (h.Length != 6) return System.Drawing.Color.Black;
            return System.Drawing.Color.FromArgb(
                Convert.ToInt32(h[..2], 16),
                Convert.ToInt32(h[2..4], 16),
                Convert.ToInt32(h[4..6], 16));
        }
        catch
        {
            return System.Drawing.Color.Black;
        }
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
        // Release any reader thread parked on the gate BEFORE we wait on _session.Dispose()'s
        // internal read-task join — otherwise tab-close could hang up to that join's timeout
        // whenever the web-write queue was above the high-water mark.
        _drainGate.Set();
        _dropStatusTimer?.Stop();
        _dropCopyInProgress = false;
        _session?.Dispose();
        _session = null;
        _drainGate.Dispose();
    }

    private void Terminal_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
            return;
        }
        string? dest = ResolveDropDestination()?.Display;
        e.Effects = dest is null ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
        ShowFileDropOverlay(dest is not null, dest);
    }

    private void Terminal_PreviewDragLeave(object sender, DragEventArgs e)
    {
        if (_dropCopyInProgress) return;
        HideDropStatus();
    }

    private void Terminal_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
        {
            HideDropStatus();
            return;
        }
        CopyDroppedFiles(paths);
    }

    private void ShowFileDropOverlay(bool show, string? destDir)
    {
        if (_dropCopyInProgress) return;
        if (!show)
        {
            HideDropStatus();
            return;
        }
        ShowDropStatus(
            "Drop to copy",
            string.IsNullOrEmpty(destDir) ? "Release to copy into this folder" : destDir,
            "Ui.Accent");
    }

    private void HideDropStatus()
    {
        _dropStatusTimer?.Stop();
        if (FileDropOverlay is not null)
            FileDropOverlay.Visibility = Visibility.Collapsed;
    }

    private void ShowDropStatus(string title, string detail, string brushKey, int autoHideMs = 0)
    {
        if (FileDropOverlay is null) return;
        FileDropTitle.Text = title;
        FileDropLabel.Text = detail;
        var brush = TryFindResource(brushKey) as Brush ?? Brushes.White;
        FileDropTitle.Foreground = brush;
        FileDropCard.BorderBrush = brush;
        FileDropOverlay.Visibility = Visibility.Visible;

        _dropStatusTimer?.Stop();
        if (autoHideMs <= 0) return;
        _dropStatusTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(autoHideMs) };
        _dropStatusTimer.Interval = TimeSpan.FromMilliseconds(autoHideMs);
        _dropStatusTimer.Tick -= DropStatusTimer_Tick;
        _dropStatusTimer.Tick += DropStatusTimer_Tick;
        _dropStatusTimer.Start();
    }

    private void DropStatusTimer_Tick(object? sender, EventArgs e)
    {
        _dropStatusTimer?.Stop();
        if (!_dropCopyInProgress)
            HideDropStatus();
    }

    private void CopyDroppedFiles(string[] paths)
    {
        var dest = ResolveDropDestination();
        if (dest is null)
        {
            ShowDropStatus("Cannot copy", "The current shell directory is not available yet.", "Ui.Error", 4000);
            return;
        }

        _dropCopyInProgress = true;
        string firstName = Path.GetFileName(paths[0].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        ShowDropStatus(
            "Checking…",
            paths.Length == 1 ? $"{firstName}\n→ {dest.Display}" : $"{paths.Length} items → {dest.Display}",
            "Ui.Accent");

        _ = Task.Run(() =>
        {
            try
            {
            List<string> conflicts;
            try
            {
                conflicts = dest.Ssh is not null
                    ? SshFileDrop.FindNameConflicts(paths, dest.Ssh, dest.Path)
                    : ShellFileDrop.FindNameConflicts(paths, dest.Path);
            }
            catch
            {
                conflicts = [];
            }

            FileConflictChoice choice = FileConflictChoice.Rename;
            if (conflicts.Count > 0)
            {
                choice = Dispatcher.Invoke(() =>
                {
                    _dropCopyInProgress = false;
                    HideDropStatus();
                    var picked = FileConflictDialog.Ask(Window.GetWindow(this), conflicts, dest.Display);
                    if (picked != FileConflictChoice.Cancel)
                        _dropCopyInProgress = true;
                    return picked;
                });
                if (choice == FileConflictChoice.Cancel)
                    return;
            }

            Dispatcher.Invoke(() =>
            {
                if (_dropCopyInProgress)
                {
                    ShowDropStatus(
                        paths.Length == 1 ? "Copying…" : $"Copying {paths.Length} items…",
                        paths.Length == 1 ? $"{firstName}\n→ {dest.Display}" : $"into {dest.Display}",
                        "Ui.Accent");
                }
            });

            void Report(string status)
            {
                Dispatcher.Invoke(() =>
                {
                    if (_dropCopyInProgress)
                        ShowDropStatus("Copying…", $"{status}\n→ {dest.Display}", "Ui.Accent");
                });
            }

            ShellFileDrop.CopyResult result = dest.Ssh is not null
                ? SshFileDrop.CopyDroppedPaths(paths, dest.Ssh, dest.Path, choice, Report)
                : ShellFileDrop.CopyDroppedPaths(paths, dest.Path, choice, Report);

            Dispatcher.Invoke(() =>
            {
                _dropCopyInProgress = false;
                string names = result.CopiedNames.Count == 0
                    ? ""
                    : result.CopiedNames.Count <= 4
                        ? string.Join("\n", result.CopiedNames)
                        : string.Join("\n", result.CopiedNames.Take(3)) + $"\n… and {result.CopiedNames.Count - 3} more";

                if (result.HasCopied && !result.HasErrors)
                {
                    string title = result.Copied == 1 ? "Copied" : $"Copied {result.Copied} items";
                    ShowDropStatus(title, string.IsNullOrEmpty(names) ? dest.Display : $"{names}\n→ {dest.Display}", "Ui.Success", 3200);
                }
                else if (result.HasCopied && result.HasErrors)
                {
                    ShowDropStatus(
                        $"Copied {result.Copied}, {result.Errors.Count} failed",
                        string.Join("\n", result.Errors.Take(4)),
                        "Ui.Warning",
                        5000);
                }
                else
                {
                    ShowDropStatus(
                        "Copy failed",
                        result.Errors.Count > 0 ? string.Join("\n", result.Errors.Take(4)) : "Nothing was copied.",
                        "Ui.Error",
                        5000);
                }
            });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    _dropCopyInProgress = false;
                    ShowDropStatus("Copy failed", ex.Message, "Ui.Error", 5000);
                });
            }
        });
    }

    private sealed record DropDestination(string Path, string Display, SshConnection? Ssh);

    private DropDestination? ResolveDropDestination()
    {
        var ssh = SshConnection.Resolve(_shellCommand, SessionProcessId, _sshMuxControlPath, _cwdHost);
        if (ssh is not null)
        {
            string remoteDir = IsUsableRemoteCwd(CurrentWorkingDirectory, _cwdHost)
                ? CurrentWorkingDirectory!
                : "~";
            return new DropDestination(remoteDir, SshFileDrop.FormatDestination(ssh, remoteDir), ssh);
        }

        string? local = ShellFileDrop.ResolveDestinationDirectory(CurrentWorkingDirectory, null);
        return local is null ? null : new DropDestination(local, local, null);
    }

    private static bool IsUsableRemoteCwd(string? cwd, string? oscHost)
    {
        if (string.IsNullOrWhiteSpace(cwd)) return false;
        if (!SshConnection.IsLocalHostName(oscHost)) return true;
        if (cwd.StartsWith('~')) return true;
        if (cwd.StartsWith('/'))
            return ShellFileDrop.TryResolveExistingDirectory(cwd) is null;
        return false;
    }

    private void MaybeUpdateCwdFromSshTitle(string? title)
    {
        if (_cwdFromOsc7) return;
        if (!SshFileDrop.TryParseTitleCwd(title, out string path)) return;
        if (!SshConnection.LooksLikeSsh(_shellCommand)
            && SshConnection.Resolve(_shellCommand, SessionProcessId, _sshMuxControlPath, _cwdHost) is null)
            return;
        CurrentWorkingDirectory = path;
        CwdChanged?.Invoke(path);
    }

    private record TerminalMessage(string type, string? data, int cols, int rows, int? fontSize, string? host);
}
