using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using UselessTerminal.Controls;
using UselessTerminal.Models;
using UselessTerminal.Services;
using Wpf.Ui.Controls;
using MenuItem = System.Windows.Controls.MenuItem;
using Button = System.Windows.Controls.Button;

namespace UselessTerminal;

public partial class MainWindow : FluentWindow, INotifyPropertyChanged
{
    private bool _sessionPanelOpen;
    private double _sessionPanelWidth = 260;
    private readonly List<TerminalTabState> _tabs = new();
    private TerminalTabState? _activeTab;

    private TerminalTabState? _tabDragSource;
    private Point _tabDragMouseDown;
    private const double TabDragThreshold = 6;

    // Quake-mode global hotkey
    private const int HOTKEY_ID_QUAKE = 0x7001;
    private const int MOD_WIN = 0x0008;
    private HwndSource? _hwndSource;
    private bool _quakeRegistered;
    private readonly CommandNotifier _commandNotifier = new();
    private bool _crtMode;
    private bool _minimapMode;
    private bool _browserPanelOpen;
    private double _browserPanelWidth = 500;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private bool _reallyClosing;

    // Cached last-known-good window bounds. Reading Left/Top/ActualWidth/ActualHeight
    // on a hidden/minimised window can yield NaN or 0; those would overwrite a perfectly
    // good saved state, breaking session restore.
    private double _lastGoodLeft = double.NaN;
    private double _lastGoodTop = double.NaN;
    private double _lastGoodWidth = double.NaN;
    private double _lastGoodHeight = double.NaN;

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(nint hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint hWnd, int id);
    [DllImport("user32.dll")] private static extern bool FlashWindow(nint hWnd, bool invert);

    private static readonly string[] TabColors =
    [
        "", "#00ff44", "#ff003c", "#ffff00", "#00e5ff", "#ff00ff", "#ff8800", "#ffffff", "#888888"
    ];

    public ICommand TogglePanelCommand { get; }
    public ICommand NewTabCommand { get; }
    public ICommand CloseTabCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        TogglePanelCommand = new RelayCommand(ToggleSessionPanel);
        NewTabCommand = new RelayCommand(AddDefaultTab);
        CloseTabCommand = new RelayCommand(CloseActivePane);

        InitializeComponent();
        DataContext = this;

        if (ElevationHelper.IsProcessElevated())
            Title = "Useless Terminal — Administrator";

        SessionPanel.SessionLaunched += (session, runAsAdministrator) =>
        {
            // When this process is already elevated, ConPTY children inherit the same token — use
            // embedded tabs only. External UAC launch is only needed when we are not elevated.
            if (runAsAdministrator && !ElevationHelper.IsProcessElevated())
            {
                try
                {
                    ElevatedShellLauncher.Launch(session);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Could not start an elevated shell.\n\n{ex.Message}",
                        "Run as administrator",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }

                return;
            }

            string command = session.GetFullCommand();
            string? workDir = string.IsNullOrWhiteSpace(session.WorkingDirectory) ? null : session.WorkingDirectory;
            string? startCmd = string.IsNullOrWhiteSpace(session.StartingCommand) ? null : session.StartingCommand;
            string? clr = string.IsNullOrWhiteSpace(session.ColorTag) ? null : session.ColorTag;
            string? themeBg = string.IsNullOrWhiteSpace(session.ThemeBackground) ? null : session.ThemeBackground;
            int themeFontSize = session.ThemeFontSize;
            Dictionary<string, string>? envVars = ParseEnvVars(session.EnvironmentVariables);
            AddTab(session.Name, command, workDir, startCmd, clr, lockTitle: true, sessionThemeBg: themeBg, sessionFontSize: themeFontSize, extraEnv: envVars);
        };

        SessionPanel.SnippetTriggered += cmd =>
        {
            if (_activeTab is null) return;
            var pane = _activeTab.FocusedPane ?? _activeTab.Control;
            pane.SendCommand(cmd);
            pane.FocusTerminal();
        };

        SettingsStore.Instance.Load();
        SettingsStore.Instance.SettingsChanged += OnSettingsChanged;

        Loaded += (_, _) =>
        {
            RestoreWindowState();
            PopulateShellMenu();
            RegisterQuakeHotkey();
            ApplyWindowBackdrop(SettingsStore.Instance.Current.WindowBackdrop);
            InitializeTrayIcon();

            var statusTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            statusTimer.Tick += (_, _) => RefreshStatusBar();
            statusTimer.Start();

            // Periodic snapshot of window/tab state so a force-kill or crash still leaves
            // a usable session to restore. SaveWindowState swallows its own errors.
            var autoSaveTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(15)
            };
            autoSaveTimer.Tick += (_, _) => { try { SaveWindowState(); } catch { } };
            autoSaveTimer.Start();
        };

        SystemEvents_SessionEndingHook();
        StateChanged += MainWindow_StateChanged;
        LocationChanged += (_, _) => CaptureWindowBounds();
        SizeChanged += (_, _) => CaptureWindowBounds();
        Activated += (_, _) =>
        {
            _commandNotifier.WindowIsActive = true;
            _commandNotifier.Reset();
        };
        Deactivated += (_, _) => _commandNotifier.WindowIsActive = false;
        _commandNotifier.CommandFinished += ShowCommandNotification;

        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    private void SystemEvents_SessionEndingHook()
    {
        try
        {
            // Save state when Windows is shutting down / signing the user out.
            SystemEvents.SessionEnding += (_, _) => { try { SaveWindowState(); } catch { } };
        }
        catch { }
    }

    private void RegisterQuakeHotkey()
    {
        try
        {
            var helper = new WindowInteropHelper(this);
            _hwndSource = HwndSource.FromHwnd(helper.Handle);
            _hwndSource?.AddHook(WndProc);
            // Win + ` (backtick / OemTilde = 0xC0)
            _quakeRegistered = RegisterHotKey(helper.Handle, HOTKEY_ID_QUAKE, MOD_WIN, 0xC0);
        }
        catch { }
    }

    private void UnregisterQuakeHotkey()
    {
        if (!_quakeRegistered) return;
        try
        {
            var helper = new WindowInteropHelper(this);
            UnregisterHotKey(helper.Handle, HOTKEY_ID_QUAKE);
            _hwndSource?.RemoveHook(WndProc);
        }
        catch { }
        _quakeRegistered = false;
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID_QUAKE)
        {
            ToggleQuakeMode();
            handled = true;
        }
        return nint.Zero;
    }

    private void ToggleQuakeMode()
    {
        if (Visibility == Visibility.Visible && IsActive)
        {
            Hide();
        }
        else
        {
            ShowFromTray();
        }
    }

    // --- System Tray ---

    private void InitializeTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        var icon = File.Exists(iconPath)
            ? new System.Drawing.Icon(iconPath)
            : System.Drawing.SystemIcons.Application;

        var trayMenu = new System.Windows.Forms.ContextMenuStrip();
        trayMenu.Items.Add("Show / Hide", null, (_, _) => Dispatcher.Invoke(ToggleQuakeMode));
        trayMenu.Items.Add("New Tab", null, (_, _) => Dispatcher.Invoke(() => { ShowFromTray(); AddDefaultTab(); }));
        trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        trayMenu.Items.Add("Settings", null, (_, _) => Dispatcher.Invoke(() => { ShowFromTray(); OpenSettings(); }));
        trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        trayMenu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(() => { _reallyClosing = true; Close(); }));

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = icon,
            Text = "Useless Terminal",
            Visible = true,
            ContextMenuStrip = trayMenu
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
    }

    private void ShowFromTray()
    {
        Show();
        if (WindowState == System.Windows.WindowState.Minimized)
            WindowState = System.Windows.WindowState.Normal;
        Activate();
        _activeTab?.Control.FocusTerminal();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == System.Windows.WindowState.Minimized)
            Hide();
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        var kb = KeyBindingConfig.Instance;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (kb.Matches("nextTab", ctrl, shift, alt, key) && !shift)
        { SelectNextTab(); e.Handled = true; }
        else if (kb.Matches("prevTab", ctrl, shift, alt, key))
        { SelectPreviousTab(); e.Handled = true; }
        else if (ctrl && !alt && key >= Key.D1 && key <= Key.D9)
        {
            int index = key - Key.D1;
            if (index < _tabs.Count) { TabStrip.SelectedItem = _tabs[index].TabItem; e.Handled = true; }
        }
        else if (ctrl && alt && key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            int index = key - Key.NumPad0 - 1;
            if (index < 0) index = 9;
            if (index < _tabs.Count) { TabStrip.SelectedItem = _tabs[index].TabItem; e.Handled = true; }
        }
        else if (kb.Matches("movePaneFocus", ctrl, shift, alt, key))
        { MovePaneFocus(key); e.Handled = true; }
        else if (kb.Matches("newSession", ctrl, shift, alt, key))
        { SessionPanel.TriggerAddSession(); e.Handled = true; }
        else if (kb.Matches("settings", ctrl, shift, alt, key))
        { OpenSettings(); e.Handled = true; }
        else if (kb.Matches("duplicateTab", ctrl, shift, alt, key))
        { if (_activeTab != null) DuplicateTab(_activeTab); e.Handled = true; }
        else if (kb.Matches("quickConnect", ctrl, shift, alt, key))
        { QuickSshConnect(); e.Handled = true; }
        else if (ctrl && shift && key == Key.B)
        { ToggleBrowserPanel(); e.Handled = true; }
        else if (kb.Matches("commandPalette", ctrl, shift, alt, key))
        { ShowCommandPalette(); e.Handled = true; }
    }

    private void MovePaneFocus(Key direction)
    {
        if (_activeTab is null || _activeTab.PaneCount <= 1) return;
        var allPanes = _activeTab.AllPanes;
        int current = _activeTab.FocusedPane is not null
            ? allPanes.IndexOf(_activeTab.FocusedPane) : 0;
        if (current < 0) current = 0;

        int count = allPanes.Count;

        // Pane layout positions:
        // 2 panes: [0]=left, [1]=right
        // 3 panes: [0]=top-left, [1]=top-right, [2]=bottom (full width)
        // 4 panes: [0]=top-left, [1]=top-right, [2]=bottom-left, [3]=bottom-right
        int next = current;

        if (count == 2)
        {
            if (direction == Key.Left || direction == Key.Right)
                next = 1 - current;
        }
        else if (count == 3)
        {
            next = (current, direction) switch
            {
                (0, Key.Right) => 1,
                (1, Key.Left) => 0,
                (0, Key.Down) => 2,
                (1, Key.Down) => 2,
                (2, Key.Up) => 0,
                _ => current
            };
        }
        else if (count == 4)
        {
            next = (current, direction) switch
            {
                (0, Key.Right) => 1,
                (0, Key.Down) => 2,
                (1, Key.Left) => 0,
                (1, Key.Down) => 3,
                (2, Key.Right) => 3,
                (2, Key.Up) => 0,
                (3, Key.Left) => 2,
                (3, Key.Up) => 1,
                _ => current
            };
        }

        if (next != current)
        {
            _activeTab.FocusedPane = allPanes[next];
            UpdatePaneFocusVisuals(_activeTab);
            allPanes[next].FocusTerminal();
        }
    }

    private void SelectNextTab()
    {
        if (_tabs.Count < 2) return;
        int current = _activeTab is null ? 0 : _tabs.IndexOf(_activeTab);
        int next = (current + 1) % _tabs.Count;
        TabStrip.SelectedItem = _tabs[next].TabItem;
    }

    private void SelectPreviousTab()
    {
        if (_tabs.Count < 2) return;
        int current = _activeTab is null ? 0 : _tabs.IndexOf(_activeTab);
        int prev = (current - 1 + _tabs.Count) % _tabs.Count;
        TabStrip.SelectedItem = _tabs[prev].TabItem;
    }

    private void ToggleSessionPanel()
    {
        _sessionPanelOpen = !_sessionPanelOpen;
        SessionPanelColumn.MinWidth = _sessionPanelOpen ? 160 : 0;
        SessionPanelColumn.Width = _sessionPanelOpen
            ? new GridLength(_sessionPanelWidth, GridUnitType.Pixel)
            : new GridLength(0);
        SessionSplitterColumn.Width = _sessionPanelOpen
            ? new GridLength(5)
            : new GridLength(0);
    }

    private void SessionGridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!_sessionPanelOpen || e.Canceled) return;
        double w = SessionPanelColumn.ActualWidth;
        if (w >= 160 && w <= 900)
            _sessionPanelWidth = w;
    }

    private void ToggleBrowserPanel()
    {
        _browserPanelOpen = !_browserPanelOpen;
        BrowserPanelColumn.MinWidth = _browserPanelOpen ? 250 : 0;
        BrowserPanelColumn.Width = _browserPanelOpen
            ? new GridLength(_browserPanelWidth, GridUnitType.Pixel)
            : new GridLength(0);
        BrowserSplitterColumn.Width = _browserPanelOpen
            ? new GridLength(5)
            : new GridLength(0);
    }

    private void BrowserGridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!_browserPanelOpen || e.Canceled) return;
        double w = BrowserPanelColumn.ActualWidth;
        if (w >= 250 && w <= 1600)
            _browserPanelWidth = w;
    }

    private void BrowserToggleButton_Click(object sender, RoutedEventArgs e) => ToggleBrowserPanel();

    // --- Tab Management ---

    public void AddTab(string title, string command, string? workingDirectory = null, string? startingCommand = null, string? color = null, bool lockTitle = false, string? sessionThemeBg = null, int sessionFontSize = 0, Dictionary<string, string>? extraEnv = null)
    {
        var container = new Grid { Visibility = Visibility.Collapsed };
        var termControl = new TerminalControl();
        if (!string.IsNullOrWhiteSpace(sessionThemeBg))
            termControl.SessionThemeBackground = sessionThemeBg;
        if (sessionFontSize > 0)
            termControl.SessionFontSize = sessionFontSize;
        if (extraEnv is { Count: > 0 })
            termControl.ExtraEnvironment = extraEnv;
        container.Children.Add(termControl);

        var tabState = new TerminalTabState
        {
            Title = title,
            Command = command,
            WorkingDirectory = workingDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            StartingCommand = startingCommand,
            Control = termControl,
            Container = container,
            Renamed = lockTitle
        };

        var tabItem = new TabItem
        {
            Header = title,
            Tag = tabState
        };
        tabState.TabItem = tabItem;

        tabItem.ContextMenu = BuildTabContextMenu(tabState);

        termControl.TitleChanged += newTitle =>
        {
            Dispatcher.Invoke(() =>
            {
                if (!tabState.Renamed)
                {
                    tabState.Title = newTitle;
                    UpdateTabHeader(tabState);
                }
            });
        };

        termControl.PaneFocused += pane => OnPaneFocused(tabState, pane);

        termControl.BellRang += () => Dispatcher.Invoke(() => FlashTabOnBell(tabState));

        termControl.BufferExportRequested += text => Dispatcher.Invoke(() => ExportBufferToFile(text));

        termControl.OutputProduced += () => Dispatcher.InvokeAsync(() =>
        {
            if (_activeTab != tabState && !tabState.HasActivity)
            {
                tabState.HasActivity = true;
                UpdateTabHeader(tabState);
            }
            _commandNotifier.OnOutputReceived(tabState.Title);
        });

        termControl.CwdChanged += cwd => Dispatcher.Invoke(() =>
        {
            if (_activeTab == tabState)
            {
                string baseName = ElevationHelper.IsProcessElevated() ? "Useless Terminal — Administrator" : "Useless Terminal By Unnamed10110";
                Title = $"{baseName}  —  {cwd}";
                RefreshStatusBar();
            }
        });

        termControl.RawOutputReceived += data =>
        {
            tabState.Logger.Write(data);
            tabState.Recorder.WriteOutput(data);
        };

        termControl.ShellIntegrationEvent += marker => Dispatcher.InvokeAsync(() =>
        {
            if (marker.StartsWith("D"))
            {
                string code = marker.Length > 2 ? marker[2..] : "";
                tabState.LastExitCode = code;
                if (_activeTab == tabState) RefreshStatusBar();
            }
        });

        termControl.SearchAllTabsRequested += query => Dispatcher.InvokeAsync(() =>
        {
            string escaped = System.Text.Json.JsonSerializer.Serialize(query);
            foreach (var tab in _tabs)
            {
                foreach (var pane in tab.AllPanes)
                {
                    if (pane != termControl)
                        pane.ExecuteScript($"window.termSearchAll({escaped})");
                }
            }
        });

        WireBroadcast(tabState, termControl);

        TerminalHost.Children.Add(container);
        _tabs.Add(tabState);
        TabStrip.Items.Add(tabItem);
        TabStrip.SelectedItem = tabItem;

        if (!string.IsNullOrEmpty(color))
            SetTabColor(tabState, color);

        RenumberTabs();
        ActivateTab(tabState);
    }

    private void RenumberTabs()
    {
        foreach (var tab in _tabs)
            UpdateTabHeader(tab);
    }

    private void MoveTab(int fromIndex, int toIndexBeforeRemove)
    {
        if (fromIndex < 0 || fromIndex >= _tabs.Count) return;
        if (toIndexBeforeRemove < 0 || toIndexBeforeRemove > _tabs.Count) return;

        var tab = _tabs[fromIndex];
        if (toIndexBeforeRemove == fromIndex || toIndexBeforeRemove == fromIndex + 1)
            return;

        _tabs.RemoveAt(fromIndex);
        int insertPos = toIndexBeforeRemove > fromIndex ? toIndexBeforeRemove - 1 : toIndexBeforeRemove;
        _tabs.Insert(insertPos, tab);

        TabStrip.Items.Remove(tab.TabItem);
        TabStrip.Items.Insert(insertPos, tab.TabItem);

        TerminalHost.Children.Remove(tab.Container);
        TerminalHost.Children.Insert(insertPos, tab.Container);

        RenumberTabs();
        TabStrip.SelectedItem = tab.TabItem;
    }

    private int GetTabInsertIndexFromPoint(Point posInTabStrip)
    {
        int n = TabStrip.Items.Count;
        for (int i = 0; i < n; i++)
        {
            if (TabStrip.ItemContainerGenerator.ContainerFromIndex(i) is not TabItem ti)
                continue;
            if (ti.ActualWidth <= 0) continue;
            Point topLeft = ti.TranslatePoint(new Point(0, 0), TabStrip);
            double midX = topLeft.X + ti.ActualWidth / 2;
            if (posInTabStrip.X < midX)
                return i;
        }

        return n;
    }

    private bool IsPointOverTabHeaders(Point posInTabStrip)
    {
        if (_tabs.Count == 0) return false;
        if (TabStrip.ItemContainerGenerator.ContainerFromIndex(0) is not TabItem first)
            return posInTabStrip.Y <= 40;
        double h = first.ActualHeight;
        if (h <= 0) h = 32;
        return posInTabStrip.Y <= h + 12;
    }

    private void TabStrip_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Translate vertical wheel into horizontal scroll for the tab strip.
        var sv = FindNamedChild<ScrollViewer>(TabStrip, "TabScrollViewer");
        if (sv is null) return;
        sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private void TabStrip_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _tabDragSource = null;
        if (FindParent<Button>(e.OriginalSource as DependencyObject) is not null)
            return;
        if (FindParent<TabItem>(e.OriginalSource as DependencyObject) is not { Tag: TerminalTabState tab })
            return;
        _tabDragSource = tab;
        _tabDragMouseDown = e.GetPosition(null);
    }

    private void TabStrip_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _tabDragSource == null) return;
        if ((e.GetPosition(null) - _tabDragMouseDown).Length < TabDragThreshold) return;
        var data = _tabDragSource;
        _tabDragSource = null;
        DragDrop.DoDragDrop(TabStrip, new DataObject(typeof(TerminalTabState), data), DragDropEffects.Move);
    }

    private void TabStrip_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _tabDragSource = null;
    }

    private void TabStrip_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TerminalTabState))) return;
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void TabStrip_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TerminalTabState))) return;
        if (e.Data.GetData(typeof(TerminalTabState)) is not TerminalTabState dragged) return;

        var pos = e.GetPosition(TabStrip);
        if (!IsPointOverTabHeaders(pos))
        {
            e.Handled = true;
            return;
        }

        int insertIndex = GetTabInsertIndexFromPoint(pos);
        int fromIndex = _tabs.IndexOf(dragged);
        if (fromIndex < 0) return;

        MoveTab(fromIndex, insertIndex);
        e.Handled = true;
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T t) return t;
            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private void UpdateTabHeader(TerminalTabState tab)
    {
        int index = _tabs.IndexOf(tab);
        if (index < 0) return;

        var stack = new StackPanel { Orientation = Orientation.Horizontal };

        if (tab.Pinned)
        {
            stack.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "\uE718",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        var icon = new System.Windows.Controls.Image
        {
            Source = ShellIconLoader.GetIconForCommand(tab.Command),
            Width = 16,
            Height = 16,
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Stretch = Stretch.Uniform,
        };
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
        stack.Children.Add(icon);

        if (!string.IsNullOrEmpty(tab.GroupName))
        {
            stack.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = tab.GroupName,
                FontSize = 8,
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x8C, 0xF8)),
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        stack.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = tab.Pinned ? "" : $"{index + 1}  {tab.Title}",
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
        });

        if (tab.ReadOnly)
        {
            stack.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "\uE72E",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xAA, 0x00)),
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        if (tab.Logger.IsLogging)
        {
            stack.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 6, Height = 6,
                Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x3C)),
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Logging",
            });
        }

        if (tab.HasActivity && _activeTab != tab)
        {
            stack.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 6, Height = 6,
                Fill = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x44)),
                Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        tab.TabItem.Header = stack;
    }

    private void AddDefaultTab()
    {
        var profiles = ShellDetector.DetectShells();
        var defaultProfile = profiles.FirstOrDefault(p => p.IsDefault) ?? profiles.First();
        string cmd = ShellDetector.GetDefaultShell();
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        AddTab("Terminal", cmd, home, color: defaultProfile.Color);
    }

    private void ActivateTab(TerminalTabState tab)
    {
        if (_activeTab == tab) return;

        if (_activeTab != null)
            _activeTab.Container.Visibility = Visibility.Collapsed;

        _activeTab = tab;
        tab.Container.Visibility = Visibility.Visible;
        tab.HasActivity = false;
        UpdateTabHeader(tab);

        if (!tab.Started)
        {
            tab.Control.StartSession(tab.Command, tab.WorkingDirectory);
            tab.Started = true;

            if (!string.IsNullOrWhiteSpace(tab.StartingCommand))
            {
                Task.Delay(500).ContinueWith(_ =>
                    Dispatcher.Invoke(() => tab.Control.SendCommand(tab.StartingCommand!)),
                    TaskScheduler.Default);
            }
        }

        if (tab.FocusedPane is null) tab.FocusedPane = tab.Control;
        UpdatePaneFocusVisuals(tab);
        (tab.FocusedPane ?? tab.Control).FocusTerminal();
        RefreshStatusBar();

        ScheduleStatusBarRefresh();
    }

    private void ScheduleStatusBarRefresh()
    {
        Task.Delay(800).ContinueWith(_ => Dispatcher.InvokeAsync(RefreshStatusBar), TaskScheduler.Default);
        Task.Delay(2500).ContinueWith(_ => Dispatcher.InvokeAsync(RefreshStatusBar), TaskScheduler.Default);
    }

    private void CloseTab(TerminalTabState tab)
    {
        int index = _tabs.IndexOf(tab);
        _tabs.Remove(tab);
        TabStrip.Items.Remove(tab.TabItem);
        TerminalHost.Children.Remove(tab.Container);
        foreach (var pane in tab.ExtraPanes) pane.Dispose();
        tab.Control.Dispose();
        tab.Logger.Dispose();
        tab.Recorder.Dispose();

        if (_tabs.Count == 0)
        {
            Close();
            return;
        }

        RenumberTabs();

        if (_activeTab == tab)
        {
            _activeTab = null;
            int newIndex = Math.Min(index, _tabs.Count - 1);
            TabStrip.SelectedItem = _tabs[newIndex].TabItem;
        }
    }

    private void CloseActiveTab()
    {
        if (_activeTab != null)
            CloseTab(_activeTab);
    }

    // --- Tab Context Menu ---

    private ContextMenu BuildTabContextMenu(TerminalTabState tab)
    {
        var menu = new ContextMenu
        {
            Background = new SolidColorBrush(Colors.Black),
            Foreground = new SolidColorBrush(Colors.White),
            BorderBrush = new SolidColorBrush(ParseHexColor("#555555")),
            BorderThickness = new Thickness(1)
        };

        var pinItem = new MenuItem { Header = tab.Pinned ? "Unpin Tab" : "Pin Tab" };
        pinItem.Click += (_, _) => TogglePinTab(tab);
        menu.Items.Add(pinItem);

        var rename = new MenuItem { Header = "Rename Tab" };
        rename.Click += (_, _) => RenameTab(tab);
        menu.Items.Add(rename);

        var colorMenu = new MenuItem { Header = "Tab Color" };
        foreach (string color in TabColors)
        {
            var colorItem = new MenuItem();
            if (string.IsNullOrEmpty(color))
            {
                colorItem.Header = "None";
            }
            else
            {
                colorItem.Header = new System.Windows.Shapes.Ellipse
                {
                    Width = 14, Height = 14,
                    Fill = new SolidColorBrush(ParseHexColor(color))
                };
            }
            string c = color;
            colorItem.Click += (_, _) => SetTabColor(tab, c);
            colorMenu.Items.Add(colorItem);
        }
        menu.Items.Add(colorMenu);

        menu.Items.Add(new Separator());

        var splitPane = new MenuItem { Header = "Add Pane (max 4)" };
        splitPane.Click += (_, _) => SplitTab(tab, Orientation.Horizontal);
        menu.Items.Add(splitPane);

        var unsplit = new MenuItem { Header = "Unsplit All" };
        unsplit.Click += (_, _) => UnsplitTab(tab);
        menu.Items.Add(unsplit);

        var broadcast = new MenuItem { Header = tab.BroadcastInput ? "Stop Broadcast Input" : "Broadcast Input to Panes" };
        broadcast.Click += (_, _) => ToggleBroadcastInput(tab);
        menu.Items.Add(broadcast);

        var logItem = new MenuItem { Header = tab.Logger.IsLogging ? "Stop Logging" : "Start Logging" };
        logItem.Click += (_, _) => ToggleLogging(tab);
        menu.Items.Add(logItem);

        var recordItem = new MenuItem { Header = tab.Recorder.IsRecording ? "Stop Recording (.cast)" : "Start Recording (.cast)" };
        recordItem.Click += (_, _) => ToggleRecording(tab);
        menu.Items.Add(recordItem);

        var readOnlyItem = new MenuItem { Header = tab.ReadOnly ? "Disable Read-Only" : "Read-Only Mode" };
        readOnlyItem.Click += (_, _) => ToggleReadOnly(tab);
        menu.Items.Add(readOnlyItem);

        var groupItem = new MenuItem { Header = string.IsNullOrEmpty(tab.GroupName) ? "Set Tab Group" : $"Tab Group: {tab.GroupName}" };
        groupItem.Click += (_, _) => SetTabGroup(tab);
        menu.Items.Add(groupItem);

        menu.Items.Add(new Separator());

        var duplicate = new MenuItem { Header = "Duplicate Tab     Ctrl+Shift+D" };
        duplicate.Click += (_, _) => DuplicateTab(tab);
        menu.Items.Add(duplicate);

        var saveSession = new MenuItem { Header = "Save as Session" };
        saveSession.Click += (_, _) => SaveTabAsSession(tab);
        menu.Items.Add(saveSession);

        menu.Items.Add(new Separator());

        var close = new MenuItem { Header = "Close Tab          Ctrl+W" };
        close.Click += (_, _) => CloseTab(tab);
        menu.Items.Add(close);

        var closeOthers = new MenuItem { Header = "Close Other Tabs" };
        closeOthers.Click += (_, _) => CloseOtherTabs(tab);
        menu.Items.Add(closeOthers);

        var closeRight = new MenuItem { Header = "Close Tabs to the Right" };
        closeRight.Click += (_, _) => CloseTabsToRight(tab);
        menu.Items.Add(closeRight);

        return menu;
    }

    // --- Context Menu Handlers ---

    private void RenameTab(TerminalTabState tab)
    {
        var dlg = new RenameDialog(tab.Title) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            tab.Title = dlg.ResultName;
            tab.Renamed = true;
            UpdateTabHeader(tab);
        }
    }

    private void SetTabColor(TerminalTabState tab, string color)
    {
        tab.HighlightColor = color;
        UpdateTabColorBar(tab);
    }

    private void RefreshAllTabChrome()
    {
        foreach (var tab in _tabs)
            UpdateTabColorBar(tab);
    }

    private void UpdateTabColorBar(TerminalTabState tab)
    {
        bool selected = tab.TabItem.IsSelected;

        if (selected)
        {
            tab.TabItem.Background = Brushes.White;
            tab.TabItem.Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0));
        }
        else if (string.IsNullOrEmpty(tab.HighlightColor))
        {
            tab.TabItem.Background = Brushes.Transparent;
            tab.TabItem.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        }
        else
        {
            var color = ParseHexColor(tab.HighlightColor);
            color.A = 90;
            tab.TabItem.Background = new SolidColorBrush(color);
            tab.TabItem.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        }

        tab.TabItem.ApplyTemplate();
        if (FindNamedChild<System.Windows.Controls.Border>(tab.TabItem, "TabBorder") is not { } tabBorder)
            return;

        tabBorder.Opacity = 1;
        if (selected)
        {
            if (string.IsNullOrEmpty(tab.HighlightColor))
                tabBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
            else
                tabBorder.BorderBrush = new SolidColorBrush(ParseHexColor(tab.HighlightColor));
        }
        else if (string.IsNullOrEmpty(tab.HighlightColor))
            tabBorder.BorderBrush = Brushes.Transparent;
        else
            tabBorder.BorderBrush = new SolidColorBrush(ParseHexColor(tab.HighlightColor)) { Opacity = 0.5 };
    }

    private void SplitTab(TerminalTabState tab, Orientation orientation)
    {
        if (tab.PaneCount >= 4) return;

        var newPane = new TerminalControl();
        tab.ExtraPanes.Add(newPane);
        tab.Container.Children.Add(newPane);

        newPane.PaneFocused += pane => OnPaneFocused(tab, pane);
        WireBroadcast(tab, newPane);
        newPane.BellRang += () => Dispatcher.Invoke(() => FlashTabOnBell(tab));
        newPane.BufferExportRequested += text => Dispatcher.Invoke(() => ExportBufferToFile(text));
        RebuildSplitLayout(tab);
        newPane.StartSession(tab.Command, tab.WorkingDirectory);
        UpdatePaneFocusVisuals(tab);
    }

    private void OnPaneFocused(TerminalTabState tab, TerminalControl pane)
    {
        if (tab.FocusedPane == pane) return;
        tab.FocusedPane = pane;
        UpdatePaneFocusVisuals(tab);
        if (_activeTab == tab)
            RefreshStatusBar();
    }

    private static void UpdatePaneFocusVisuals(TerminalTabState tab)
    {
        if (tab.PaneCount <= 1)
        {
            tab.Control.SetFocusIndicator(true);
            return;
        }

        foreach (var p in tab.AllPanes)
            p.SetFocusIndicator(p == tab.FocusedPane);
    }

    private void CloseActivePane()
    {
        if (_activeTab is null) return;
        var tab = _activeTab;

        if (tab.Pinned && tab.PaneCount <= 1)
            return;

        if (tab.PaneCount <= 1)
        {
            CloseTab(tab);
            return;
        }

        var target = tab.FocusedPane;

        if (target is null || target == tab.Control)
            target = tab.ExtraPanes[^1];

        tab.ExtraPanes.Remove(target);
        tab.Container.Children.Remove(target);
        target.Dispose();

        RebuildSplitLayout(tab);

        tab.FocusedPane = tab.Control;
        UpdatePaneFocusVisuals(tab);
        tab.Control.FocusTerminal();
    }

    private void RebuildSplitLayout(TerminalTabState tab)
    {
        var container = tab.Container;

        foreach (var s in tab.Splitters)
            container.Children.Remove(s);
        tab.Splitters.Clear();
        container.ColumnDefinitions.Clear();
        container.RowDefinitions.Clear();

        var allPanes = new List<TerminalControl> { tab.Control };
        allPanes.AddRange(tab.ExtraPanes);
        int count = allPanes.Count;

        if (count == 1)
        {
            Grid.SetColumn(allPanes[0], 0);
            Grid.SetRow(allPanes[0], 0);
            Grid.SetColumnSpan(allPanes[0], 1);
            Grid.SetRowSpan(allPanes[0], 1);
            return;
        }

        var splitterBrush = new SolidColorBrush(ParseHexColor("#00FF44"));

        if (count == 2)
        {
            // Side by side
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2) });
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            SetCell(allPanes[0], 0, 0, 1, 1);
            SetCell(allPanes[1], 2, 0, 1, 1);

            var splitter = new GridSplitter
            {
                Width = 2, HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = splitterBrush, Cursor = Cursors.SizeWE
            };
            Grid.SetColumn(splitter, 1);
            Grid.SetRow(splitter, 0);
            container.Children.Add(splitter);
            tab.Splitters.Add(splitter);
        }
        else if (count == 3)
        {
            // 2 on top, 1 full-width on bottom
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2) });
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2) });
            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            SetCell(allPanes[0], 0, 0, 1, 1);
            SetCell(allPanes[1], 2, 0, 1, 1);
            SetCell(allPanes[2], 0, 2, 3, 1);

            var vSplitter = new GridSplitter
            {
                Width = 2, HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = splitterBrush, Cursor = Cursors.SizeWE
            };
            Grid.SetColumn(vSplitter, 1);
            Grid.SetRow(vSplitter, 0);
            container.Children.Add(vSplitter);
            tab.Splitters.Add(vSplitter);

            var hSplitter = new GridSplitter
            {
                Height = 2, HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = splitterBrush, Cursor = Cursors.SizeNS
            };
            Grid.SetColumn(hSplitter, 0);
            Grid.SetColumnSpan(hSplitter, 3);
            Grid.SetRow(hSplitter, 1);
            container.Children.Add(hSplitter);
            tab.Splitters.Add(hSplitter);
        }
        else // count == 4
        {
            // 2×2 grid
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2) });
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2) });
            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            SetCell(allPanes[0], 0, 0, 1, 1);
            SetCell(allPanes[1], 2, 0, 1, 1);
            SetCell(allPanes[2], 0, 2, 1, 1);
            SetCell(allPanes[3], 2, 2, 1, 1);

            var vSplitter = new GridSplitter
            {
                Width = 2, HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = splitterBrush, Cursor = Cursors.SizeWE
            };
            Grid.SetColumn(vSplitter, 1);
            Grid.SetRow(vSplitter, 0);
            Grid.SetRowSpan(vSplitter, 3);
            container.Children.Add(vSplitter);
            tab.Splitters.Add(vSplitter);

            var hSplitter = new GridSplitter
            {
                Height = 2, HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = splitterBrush, Cursor = Cursors.SizeNS
            };
            Grid.SetColumn(hSplitter, 0);
            Grid.SetColumnSpan(hSplitter, 3);
            Grid.SetRow(hSplitter, 1);
            container.Children.Add(hSplitter);
            tab.Splitters.Add(hSplitter);
        }
    }

    private static void SetCell(UIElement el, int col, int row, int colSpan, int rowSpan)
    {
        Grid.SetColumn(el, col);
        Grid.SetRow(el, row);
        Grid.SetColumnSpan(el, colSpan);
        Grid.SetRowSpan(el, rowSpan);
    }

    private void UnsplitTab(TerminalTabState tab)
    {
        if (tab.ExtraPanes.Count == 0) return;

        foreach (var s in tab.Splitters)
            tab.Container.Children.Remove(s);
        tab.Splitters.Clear();

        foreach (var pane in tab.ExtraPanes)
        {
            tab.Container.Children.Remove(pane);
            pane.Dispose();
        }
        tab.ExtraPanes.Clear();

        tab.Container.ColumnDefinitions.Clear();
        tab.Container.RowDefinitions.Clear();
        SetCell(tab.Control, 0, 0, 1, 1);

        tab.FocusedPane = tab.Control;
        UpdatePaneFocusVisuals(tab);
        tab.Control.FocusTerminal();
    }

    private void DuplicateTab(TerminalTabState tab)
    {
        AddTab(tab.Title, tab.Command, tab.WorkingDirectory, lockTitle: true);
    }

    private void SaveTabAsSession(TerminalTabState tab)
    {
        string shellPath = tab.Command;
        string args = "";
        if (tab.Command.StartsWith('"'))
        {
            int end = tab.Command.IndexOf('"', 1);
            if (end > 0)
            {
                shellPath = tab.Command[1..end];
                args = tab.Command[(end + 1)..].Trim();
            }
        }

        var session = new SavedSession
        {
            Name = tab.Title,
            ShellPath = shellPath,
            Arguments = args,
            WorkingDirectory = tab.WorkingDirectory ?? ""
        };

        var dlg = new SessionEditDialog(session) { Title = "Save as Session", Owner = this };
        if (dlg.ShowDialog() == true)
        {
            var store = new SessionStore();
            store.Load();
            store.Add(session);
            SessionPanel.Reload();
        }
    }

    private void CloseOtherTabs(TerminalTabState keepTab)
    {
        var toClose = _tabs.Where(t => t != keepTab).ToList();
        foreach (var tab in toClose)
            CloseTab(tab);
    }

    private void CloseTabsToRight(TerminalTabState fromTab)
    {
        int index = _tabs.IndexOf(fromTab);
        var toClose = _tabs.Skip(index + 1).ToList();
        foreach (var tab in toClose)
            CloseTab(tab);
    }

    // --- Shell Menu ---

    private static readonly string[] FunnyNames =
    [
        "Quantum Potato", "Turbo Hamster", "Cyber Pickle", "Neon Waffle",
        "Atomic Penguin", "Disco Llama", "Hyper Taco", "Stealth Nugget",
        "Pixel Wizard", "Cosmic Burrito", "Mega Toaster", "Ghost Banana",
        "Nitro Muffin", "Shadow Pretzel", "Laser Kitten", "Thunder Donut",
        "Phantom Noodle", "Rogue Pancake", "Ultra Cactus", "Turbo Snail",
        "Ninja Pineapple", "Electric Sloth", "Galactic Bagel", "Void Chicken"
    ];

    private static string GetFunnyName()
    {
        return FunnyNames[Random.Shared.Next(FunnyNames.Length)];
    }

    private void PopulateShellMenu()
    {
        ShellMenu.Items.Clear();
        foreach (var shell in ShellDetector.DetectShells())
        {
            var item = new MenuItem { Header = shell.Name };
            string cmd = string.IsNullOrEmpty(shell.Arguments) ? shell.Command : $"\"{shell.Command}\" {shell.Arguments}";
            string shellColor = shell.Color;
            item.Click += (_, _) =>
            {
                var dlg = new RenameDialog(GetFunnyName()) { Title = $"New {shell.Name}", Owner = this };
                if (dlg.ShowDialog() == true)
                {
                    string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    AddTab(dlg.ResultName, cmd, home, color: shellColor, lockTitle: true);
                }
            };
            ShellMenu.Items.Add(item);
        }
    }

    // --- Bell / Activity ---

    private void FlashTabOnBell(TerminalTabState tab)
    {
        if (_activeTab == tab) return;
        tab.HasActivity = true;
        UpdateTabHeader(tab);
    }

    // --- Export buffer ---

    private void ExportBufferToFile(string text)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Text files|*.txt|Log files|*.log|All files|*.*",
            FileName = "terminal-output.txt",
            DefaultExt = ".txt"
        };
        if (dlg.ShowDialog() == true)
        {
            try { File.WriteAllText(dlg.FileName, text); }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not save file.\n\n{ex.Message}", "Export",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
    }

    // --- Pin tab ---

    private void TogglePinTab(TerminalTabState tab)
    {
        tab.Pinned = !tab.Pinned;
        if (tab.Pinned)
        {
            int pi = 0;
            foreach (var t in _tabs)
            {
                if (t == tab) break;
                if (t.Pinned) pi++;
            }
            int from = _tabs.IndexOf(tab);
            if (from > pi)
                MoveTab(from, pi);
        }
        RenumberTabs();
    }

    // --- Broadcast input ---

    private void ToggleBroadcastInput(TerminalTabState tab)
    {
        tab.BroadcastInput = !tab.BroadcastInput;
    }

    private static void WireBroadcast(TerminalTabState tabState, TerminalControl source)
    {
        source.InputBroadcast += data =>
        {
            if (!tabState.BroadcastInput) return;
            foreach (var pane in tabState.AllPanes)
            {
                if (pane == source) continue;
                pane.SendCommand(data.Replace("\r", "").Replace("\n", ""));
            }
        };
    }

    // --- Command Palette ---

    private void ShowCommandPalette()
    {
        var entries = new List<PaletteEntry>
        {
            new() { Id = "newTab", Label = "New Tab", Shortcut = "Ctrl+T" },
            new() { Id = "closeTab", Label = "Close Tab / Pane", Shortcut = "Ctrl+W" },
            new() { Id = "togglePanel", Label = "Toggle Sessions Panel", Shortcut = "Ctrl+B" },
            new() { Id = "toggleBrowser", Label = "Toggle Browser Panel", Shortcut = "Ctrl+Shift+B" },
            new() { Id = "settings", Label = "Open Settings", Shortcut = "Ctrl+," },
            new() { Id = "duplicateTab", Label = "Duplicate Tab", Shortcut = "Ctrl+Shift+D" },
            new() { Id = "addSession", Label = "New Saved Session", Shortcut = "Ctrl+Shift+N" },
            new() { Id = "splitPane", Label = "Add Pane (Split)" },
            new() { Id = "unsplitAll", Label = "Unsplit All Panes" },
            new() { Id = "renameTab", Label = "Rename Tab" },
            new() { Id = "pinTab", Label = "Pin / Unpin Tab" },
            new() { Id = "broadcastToggle", Label = "Toggle Broadcast Input" },
            new() { Id = "nextTab", Label = "Next Tab", Shortcut = "Ctrl+Tab" },
            new() { Id = "prevTab", Label = "Previous Tab", Shortcut = "Ctrl+Shift+Tab" },
            new() { Id = "closeOthers", Label = "Close Other Tabs" },
            new() { Id = "closeRight", Label = "Close Tabs to the Right" },
            new() { Id = "quake", Label = "Toggle Window (Quake Mode)", Shortcut = "Win+`" },
            new() { Id = "quickConnect", Label = "Quick SSH Connect", Shortcut = "Ctrl+Shift+O" },
            new() { Id = "toggleLog", Label = "Toggle Session Logging" },
            new() { Id = "toggleReadOnly", Label = "Toggle Read-Only Mode" },
            new() { Id = "toggleRecording", Label = "Toggle Recording (asciicast)" },
            new() { Id = "toggleCrt", Label = "Toggle Retro CRT Mode" },
            new() { Id = "findAllTabs", Label = "Find in All Tabs" },
            new() { Id = "toggleMinimap", Label = "Toggle Minimap Scrollbar" },
            new() { Id = "saveWorkspace", Label = "Save Current Tabs as Workspace" },
        };

        WorkspaceStore.Instance.Load();
        foreach (var ws in WorkspaceStore.Instance.Workspaces)
            entries.Add(new PaletteEntry { Id = $"ws:{ws.Id}", Label = $"Open Workspace: {ws.Name}" });

        var dlg = new CommandPaletteDialog(entries) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.SelectedActionId is null) return;

        switch (dlg.SelectedActionId)
        {
            case "newTab": AddDefaultTab(); break;
            case "closeTab": CloseActivePane(); break;
            case "togglePanel": ToggleSessionPanel(); break;
            case "toggleBrowser": ToggleBrowserPanel(); break;
            case "settings": OpenSettings(); break;
            case "duplicateTab": if (_activeTab != null) DuplicateTab(_activeTab); break;
            case "addSession": SessionPanel.TriggerAddSession(); break;
            case "splitPane": if (_activeTab != null) SplitTab(_activeTab, Orientation.Horizontal); break;
            case "unsplitAll": if (_activeTab != null) UnsplitTab(_activeTab); break;
            case "renameTab": if (_activeTab != null) RenameTab(_activeTab); break;
            case "pinTab": if (_activeTab != null) TogglePinTab(_activeTab); break;
            case "broadcastToggle": if (_activeTab != null) ToggleBroadcastInput(_activeTab); break;
            case "nextTab": SelectNextTab(); break;
            case "prevTab": SelectPreviousTab(); break;
            case "closeOthers": if (_activeTab != null) CloseOtherTabs(_activeTab); break;
            case "closeRight": if (_activeTab != null) CloseTabsToRight(_activeTab); break;
            case "quake": ToggleQuakeMode(); break;
            case "quickConnect": QuickSshConnect(); break;
            case "toggleLog": if (_activeTab != null) ToggleLogging(_activeTab); break;
            case "toggleReadOnly": if (_activeTab != null) ToggleReadOnly(_activeTab); break;
            case "toggleRecording": if (_activeTab != null) ToggleRecording(_activeTab); break;
            case "toggleCrt": ToggleCrtMode(); break;
            case "findAllTabs": FindInAllTabs(); break;
            case "toggleMinimap": ToggleMinimap(); break;
            case "saveWorkspace": SaveCurrentAsWorkspace(); break;
            default:
                if (dlg.SelectedActionId.StartsWith("ws:"))
                    LaunchWorkspace(dlg.SelectedActionId[3..]);
                break;
        }
    }

    private void SaveCurrentAsWorkspace()
    {
        var dlg = new RenameDialog("My Workspace") { Title = "Workspace Name", Owner = this };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.ResultName)) return;

        var ws = new WorkspaceProfile { Name = dlg.ResultName };
        foreach (var tab in _tabs)
        {
            ws.Tabs.Add(new WorkspaceTab
            {
                Title = tab.Title,
                Command = tab.Command,
                WorkingDirectory = tab.WorkingDirectory,
                StartingCommand = tab.StartingCommand,
            });
        }
        WorkspaceStore.Instance.Add(ws);
    }

    private void LaunchWorkspace(string workspaceId)
    {
        var ws = WorkspaceStore.Instance.Workspaces.FirstOrDefault(w => w.Id == workspaceId);
        if (ws is null) return;

        var sessionStore = new SessionStore();
        sessionStore.Load();

        foreach (var wt in ws.Tabs)
        {
            if (!string.IsNullOrEmpty(wt.SessionId))
            {
                var session = sessionStore.FindById(wt.SessionId);
                if (session is not null)
                {
                    string cmd = session.GetFullCommand();
                    string? wd = string.IsNullOrWhiteSpace(session.WorkingDirectory) ? null : session.WorkingDirectory;
                    string? sc = string.IsNullOrWhiteSpace(session.StartingCommand) ? null : session.StartingCommand;
                    string? clr = string.IsNullOrWhiteSpace(session.ColorTag) ? null : session.ColorTag;
                    AddTab(session.Name, cmd, wd, sc, clr, lockTitle: true);
                    continue;
                }
            }

            string command = string.IsNullOrWhiteSpace(wt.Command)
                ? ShellDetector.GetDefaultShell()
                : wt.Command;
            string title = string.IsNullOrWhiteSpace(wt.Title) ? "Terminal" : wt.Title;
            AddTab(title, command, wt.WorkingDirectory, wt.StartingCommand);
        }
    }

    // --- Settings ---

    private void OpenSettings()
    {
        var win = new SettingsWindow(SettingsStore.Instance.Current) { Owner = this };
        win.ShowDialog();
    }

    private void OnSettingsChanged()
    {
        var settings = SettingsStore.Instance.Current;
        foreach (var tab in _tabs)
        {
            tab.Control.ApplySettings(settings);
            foreach (var pane in tab.ExtraPanes)
                pane.ApplySettings(settings);
        }
        ApplyWindowBackdrop(settings.WindowBackdrop);
    }

    private void ApplyWindowBackdrop(string backdrop)
    {
        WindowBackdropType type = backdrop switch
        {
            "Mica" => WindowBackdropType.Mica,
            "Acrylic" => WindowBackdropType.Acrylic,
            _ => WindowBackdropType.None
        };
        WindowBackdropType = type;

        if (type == WindowBackdropType.None)
            Background = new SolidColorBrush(Color.FromRgb(0, 0, 0));
        else
            Background = Brushes.Transparent;
    }

    // --- Window State ---

    private void RestoreWindowState()
    {
        var state = WindowStateStore.Load();

        if (!double.IsNaN(state.Left) && !double.IsNaN(state.Top))
        {
            Left = state.Left;
            Top = state.Top;
        }
        Width = state.Width;
        Height = state.Height;
        if (state.IsMaximized)
            WindowState = System.Windows.WindowState.Maximized;

        if (state.SessionPanelWidth >= 160 && state.SessionPanelWidth <= 900)
            _sessionPanelWidth = state.SessionPanelWidth;

        if (state.SessionPanelOpen)
            ToggleSessionPanel();

        if (state.Tabs.Count > 0)
        {
            foreach (var ts in state.Tabs)
            {
                string? clr = string.IsNullOrEmpty(ts.HighlightColor) ? null : ts.HighlightColor;
                AddTab(ts.Title, ts.Command, ts.WorkingDirectory, ts.StartingCommand, clr, lockTitle: ts.Renamed);
            }
            if (state.ActiveTabIndex >= 0 && state.ActiveTabIndex < _tabs.Count)
                TabStrip.SelectedItem = _tabs[state.ActiveTabIndex].TabItem;
        }
        else
        {
            AddDefaultTab();
        }
    }

    private void CaptureWindowBounds()
    {
        if (WindowState != System.Windows.WindowState.Normal) return;
        if (!double.IsNaN(Left) && !double.IsInfinity(Left)) _lastGoodLeft = Left;
        if (!double.IsNaN(Top) && !double.IsInfinity(Top)) _lastGoodTop = Top;
        if (ActualWidth > 0 && !double.IsNaN(ActualWidth)) _lastGoodWidth = ActualWidth;
        if (ActualHeight > 0 && !double.IsNaN(ActualHeight)) _lastGoodHeight = ActualHeight;
    }

    private void SaveWindowState()
    {
        // RestoreBounds is Rect.Empty (NaN) when window is in Normal state, which makes
        // System.Text.Json throw on serialize and the file silently never updates.
        // Prefer the cached bounds we captured during the last LocationChanged/SizeChanged.
        bool isMaximized = WindowState == System.Windows.WindowState.Maximized;
        Rect bounds = isMaximized
            ? RestoreBounds
            : new Rect(_lastGoodLeft, _lastGoodTop, _lastGoodWidth, _lastGoodHeight);

        double left = double.IsNaN(bounds.Left) || double.IsInfinity(bounds.Left) ? 100 : bounds.Left;
        double top = double.IsNaN(bounds.Top) || double.IsInfinity(bounds.Top) ? 100 : bounds.Top;
        double width = double.IsNaN(bounds.Width) || double.IsInfinity(bounds.Width) || bounds.Width <= 0 ? 1200 : bounds.Width;
        double height = double.IsNaN(bounds.Height) || double.IsInfinity(bounds.Height) || bounds.Height <= 0 ? 800 : bounds.Height;

        var state = new Models.WindowState
        {
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            IsMaximized = isMaximized,
            SessionPanelOpen = _sessionPanelOpen,
            SessionPanelWidth = _sessionPanelWidth,
            ActiveTabIndex = _activeTab is not null ? _tabs.IndexOf(_activeTab) : 0
        };

        foreach (var tab in _tabs)
        {
            state.Tabs.Add(new TabState
            {
                Title = tab.Title,
                Command = tab.Command,
                WorkingDirectory = tab.Control.CurrentWorkingDirectory ?? tab.WorkingDirectory,
                StartingCommand = tab.StartingCommand,
                HighlightColor = tab.HighlightColor,
                Renamed = tab.Renamed
            });
        }

        WindowStateStore.Save(state);
    }

    // --- Event Handlers ---

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettings();
    private void TogglePanelButton_Click(object sender, RoutedEventArgs e) => ToggleSessionPanel();
    private void NewTabButton_Click(object sender, RoutedEventArgs e) => AddDefaultTab();
    private void NewTabDropdown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
            btn.ContextMenu!.IsOpen = true;
    }

    private void TabStrip_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshAllTabChrome();
        if (TabStrip.SelectedItem is TabItem tabItem && tabItem.Tag is TerminalTabState tab)
        {
            ActivateTab(tab);
            EnsureTabVisible(tabItem);
        }
    }

    private void EnsureTabVisible(TabItem tabItem)
    {
        var sv = FindNamedChild<ScrollViewer>(TabStrip, "TabScrollViewer");
        if (sv is null) return;
        // Defer until layout finalises so ActualWidth/positions are valid for new tabs.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (tabItem.ActualWidth <= 0) tabItem.UpdateLayout();
                Point topLeft = tabItem.TranslatePoint(new Point(0, 0), sv);
                double left = topLeft.X + sv.HorizontalOffset;
                double right = left + tabItem.ActualWidth;
                if (left < sv.HorizontalOffset)
                    sv.ScrollToHorizontalOffset(Math.Max(0, left - 8));
                else if (right > sv.HorizontalOffset + sv.ViewportWidth)
                    sv.ScrollToHorizontalOffset(Math.Max(0, right - sv.ViewportWidth + 8));
            }
            catch { }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void TabCloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TabItem tabItem && tabItem.Tag is TerminalTabState tab)
            CloseTab(tab);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_reallyClosing)
        {
            // The X button only hides to tray. Persist tabs/window state now so the
            // next launch can restore even if the process is killed while hidden.
            try { SaveWindowState(); } catch { }
            e.Cancel = true;
            Hide();
            return;
        }

        if (HasRunningProcesses())
        {
            var result = System.Windows.MessageBox.Show(
                "One or more terminal sessions are still running.\n\nClose anyway?",
                "Useless Terminal",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            if (result != System.Windows.MessageBoxResult.Yes)
            {
                _reallyClosing = false;
                e.Cancel = true;
                return;
            }
        }

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        UnregisterQuakeHotkey();
        SaveWindowState();

        foreach (var tab in _tabs)
        {
            foreach (var pane in tab.ExtraPanes) pane.Dispose();
            tab.Control.Dispose();
        }
        _tabs.Clear();
        TerminalHost.Children.Clear();
        base.OnClosing(e);
    }

    private bool HasRunningProcesses()
    {
        foreach (var tab in _tabs)
        {
            if (tab.Control.IsSessionAlive) return true;
            foreach (var p in tab.ExtraPanes)
                if (p.IsSessionAlive) return true;
        }
        return false;
    }

    // --- Helpers ---

    private static T? FindNamedChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T fe && fe.Name == name) return fe;
            var found = FindNamedChild<T>(child, name);
            if (found is not null) return found;
        }
        return null;
    }

    private static Color ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 3)
            hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
        return Color.FromRgb(
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }

    private static Dictionary<string, string>? ParseEnvVars(string? envText)
    {
        if (string.IsNullOrWhiteSpace(envText)) return null;
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in envText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string key = line[..eq].Trim();
            string val = line[(eq + 1)..].Trim();
            if (!string.IsNullOrEmpty(key))
                dict[key] = val;
        }
        return dict.Count > 0 ? dict : null;
    }

    // --- Quick Connect ---

    private void QuickSshConnect()
    {
        var dlg = new RenameDialog("") { Title = "Quick SSH Connect (user@host or user@host:port)", Owner = this };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.ResultName)) return;

        string input = dlg.ResultName.Trim();
        string sshExe = FindSshExe();
        string args;

        int colonIdx = input.LastIndexOf(':');
        if (colonIdx > 0 && int.TryParse(input[(colonIdx + 1)..], out int port))
        {
            string hostPart = input[..colonIdx];
            args = $"-p {port} {hostPart}";
        }
        else
        {
            args = input;
        }

        string command = $"\"{sshExe}\" {args}";
        AddTab($"SSH: {input}", command, color: "#6be5ff");
    }

    private static string FindSshExe()
    {
        string system32Ssh = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "OpenSSH", "ssh.exe");
        if (File.Exists(system32Ssh)) return system32Ssh;

        string progFiles = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Git", "usr", "bin", "ssh.exe");
        if (File.Exists(progFiles)) return progFiles;

        return "ssh";
    }

    // --- Logging ---

    private void ToggleLogging(TerminalTabState tab)
    {
        if (tab.Logger.IsLogging)
        {
            tab.Logger.Stop();
        }
        else
        {
            tab.Logger.Start(tab.Title);
        }
        tab.TabItem.ContextMenu = BuildTabContextMenu(tab);
        UpdateTabHeader(tab);
    }

    private void ToggleRecording(TerminalTabState tab)
    {
        if (tab.Recorder.IsRecording)
            tab.Recorder.Stop();
        else
            tab.Recorder.Start(tab.Title);
        tab.TabItem.ContextMenu = BuildTabContextMenu(tab);
        UpdateTabHeader(tab);
    }

    private void ToggleMinimap()
    {
        _minimapMode = !_minimapMode;
        string js = $"window.termToggleMinimap({(_minimapMode ? "true" : "false")})";
        foreach (var tab in _tabs)
        {
            tab.Control.ExecuteScript(js);
            foreach (var pane in tab.ExtraPanes)
                pane.ExecuteScript(js);
        }
    }

    private void FindInAllTabs()
    {
        var dlg = new RenameDialog("") { Title = "Find in All Tabs", Owner = this };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.ResultName)) return;

        string term = System.Text.Json.JsonSerializer.Serialize(dlg.ResultName);
        foreach (var tab in _tabs)
        {
            tab.Control.ExecuteScript($"window.termSearchAll({term})");
            foreach (var pane in tab.ExtraPanes)
                pane.ExecuteScript($"window.termSearchAll({term})");
        }
    }

    private void ToggleCrtMode()
    {
        _crtMode = !_crtMode;
        string js = $"window.termToggleCRT({(_crtMode ? "true" : "false")})";
        foreach (var tab in _tabs)
        {
            tab.Control.ExecuteScript(js);
            foreach (var pane in tab.ExtraPanes)
                pane.ExecuteScript(js);
        }
    }

    // --- Tab Groups ---

    private void SetTabGroup(TerminalTabState tab)
    {
        string current = tab.GroupName ?? "";
        var dlg = new RenameDialog(current) { Title = "Tab Group Name (empty to remove)", Owner = this };
        if (dlg.ShowDialog() != true) return;
        tab.GroupName = string.IsNullOrWhiteSpace(dlg.ResultName) ? null : dlg.ResultName.Trim();
        tab.TabItem.ContextMenu = BuildTabContextMenu(tab);
        UpdateTabHeader(tab);
    }

    // --- Read-Only ---

    private void ToggleReadOnly(TerminalTabState tab)
    {
        tab.ReadOnly = !tab.ReadOnly;
        foreach (var pane in tab.AllPanes)
            pane.ReadOnly = tab.ReadOnly;
        tab.TabItem.ContextMenu = BuildTabContextMenu(tab);
        UpdateTabHeader(tab);
    }

    // --- Notifications ---

    private void ShowCommandNotification(string tabTitle)
    {
        try
        {
            var helper = new WindowInteropHelper(this);
            if (helper.Handle != nint.Zero)
                FlashWindow(helper.Handle, true);
        }
        catch { }
    }

    // --- Status Bar ---

    private void RefreshStatusBar()
    {
        try
        {
            if (_activeTab is null)
            {
                StatusShell.Text = StatusPid.Text = StatusCwd.Text = StatusGitBranch.Text = StatusAlive.Text = StatusExitCode.Text = "";
                return;
            }

            var pane = _activeTab.FocusedPane ?? _activeTab.Control;
            string cmd = _activeTab.Command;
            string exe = ShellGlyphResolver.ParseExecutable(cmd);
            string shellName = Path.GetFileNameWithoutExtension(string.IsNullOrEmpty(exe) ? cmd : exe);
            StatusShell.Text = shellName;

            int pid = pane.SessionProcessId;
            StatusPid.Text = pid > 0 ? $"PID {pid}" : "";

            string cwd = pane.CurrentWorkingDirectory
                ?? _activeTab.WorkingDirectory
                ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            StatusCwd.Text = "\uD83D\uDCC2 " + cwd;

            string branch = GetGitBranch(cwd);
            StatusGitBranch.Text = branch;

            if (!string.IsNullOrEmpty(_activeTab.LastExitCode) && _activeTab.LastExitCode != "0")
            {
                StatusExitCode.Text = $"exit: {_activeTab.LastExitCode}";
                StatusExitCode.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x3C));
            }
            else if (_activeTab.LastExitCode == "0")
            {
                StatusExitCode.Text = "exit: 0";
                StatusExitCode.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
            }
            else
            {
                StatusExitCode.Text = "";
            }

            StatusAlive.Text = pane.IsSessionAlive ? "\u25CF running" : "\u25CB exited";
        StatusAlive.Foreground = pane.IsSessionAlive
            ? new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e))
            : new SolidColorBrush(Color.FromRgb(0x6b, 0x72, 0x80));
        }
        catch { }
    }

    private static string GetGitBranch(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return "";
        try
        {
            var dir = new DirectoryInfo(directory);
            while (dir is not null)
            {
                string headPath = Path.Combine(dir.FullName, ".git", "HEAD");
                if (File.Exists(headPath))
                {
                    string head = File.ReadAllText(headPath).Trim();
                    const string prefix = "ref: refs/heads/";
                    if (head.StartsWith(prefix))
                        return "\uE8CB " + head[prefix.Length..];
                    if (head.Length >= 8)
                        return "\uE8CB " + head[..8];
                    return "";
                }
                dir = dir.Parent;
            }
        }
        catch { }
        return "";
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal sealed class TerminalTabState
{
    public string Title { get; set; } = "Terminal";
    public string Command { get; set; } = "";
    public string? WorkingDirectory { get; set; }
    public string? StartingCommand { get; set; }
    public TerminalControl Control { get; set; } = null!;
    public Grid Container { get; set; } = null!;
    public TabItem TabItem { get; set; } = null!;
    public bool Started { get; set; }
    public bool Renamed { get; set; }
    public string HighlightColor { get; set; } = "";
    public bool Pinned { get; set; }
    public bool HasActivity { get; set; }
    public bool BroadcastInput { get; set; }
    public bool ReadOnly { get; set; }
    public string? LastExitCode { get; set; }
    public string? GroupName { get; set; }
    public TerminalLogger Logger { get; } = new();
    public AsciicastRecorder Recorder { get; } = new();
    public List<TerminalControl> ExtraPanes { get; } = new();
    public List<GridSplitter> Splitters { get; } = new();
    public int PaneCount => 1 + ExtraPanes.Count;
    public TerminalControl? FocusedPane { get; set; }

    public List<TerminalControl> AllPanes
    {
        get
        {
            var list = new List<TerminalControl> { Control };
            list.AddRange(ExtraPanes);
            return list;
        }
    }
}

internal sealed class RelayCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}
