using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
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
        {
            const string adminTitle = "Useless Terminal — Administrator";
            Title = adminTitle;
            MainTitleBar.Title = adminTitle;
        }

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
            AddTab(session.Name, command, workDir, startCmd, clr, lockTitle: true);
        };

        SettingsStore.Instance.Load();
        SettingsStore.Instance.SettingsChanged += OnSettingsChanged;

        Loaded += (_, _) =>
        {
            RestoreWindowState();
            PopulateShellMenu();
        };
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        if (ctrl && e.Key == Key.Tab)
        {
            if (shift) SelectPreviousTab(); else SelectNextTab();
            e.Handled = true;
        }
        else if (ctrl && !alt && e.Key >= Key.D1 && e.Key <= Key.D9)
        {
            int index = e.Key - Key.D1;
            if (index < _tabs.Count)
            {
                TabStrip.SelectedItem = _tabs[index].TabItem;
                e.Handled = true;
            }
        }
        else if (ctrl && alt && e.SystemKey >= Key.NumPad0 && e.SystemKey <= Key.NumPad9)
        {
            int index = e.SystemKey - Key.NumPad0 - 1;
            if (index < 0) index = 9;
            if (index < _tabs.Count)
            {
                TabStrip.SelectedItem = _tabs[index].TabItem;
                e.Handled = true;
            }
        }
        else if (ctrl && shift && (e.Key == Key.Left || e.Key == Key.Right ||
                                    e.Key == Key.Up || e.Key == Key.Down))
        {
            MovePaneFocus(e.Key);
            e.Handled = true;
        }
        else if (ctrl && shift && e.Key == Key.N)
        {
            SessionPanel.TriggerAddSession();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.OemComma)
        {
            OpenSettings();
            e.Handled = true;
        }
        else if (ctrl && shift && e.Key == Key.D)
        {
            if (_activeTab != null) DuplicateTab(_activeTab);
            e.Handled = true;
        }
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

    // --- Tab Management ---

    public void AddTab(string title, string command, string? workingDirectory = null, string? startingCommand = null, string? color = null, bool lockTitle = false)
    {
        var container = new Grid { Visibility = Visibility.Collapsed };
        var termControl = new TerminalControl();
        container.Children.Add(termControl);

        var tabState = new TerminalTabState
        {
            Title = title,
            Command = command,
            WorkingDirectory = workingDirectory,
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
        for (int i = 0; i < _tabs.Count; i++)
        {
            var tab = _tabs[i];
            tab.TabItem.Header = $"{i + 1}  {tab.Title}";
        }
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
        if (index >= 0)
            tab.TabItem.Header = $"{index + 1}  {tab.Title}";
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
    }

    private void CloseTab(TerminalTabState tab)
    {
        int index = _tabs.IndexOf(tab);
        _tabs.Remove(tab);
        TabStrip.Items.Remove(tab.TabItem);
        TerminalHost.Children.Remove(tab.Container);
        foreach (var pane in tab.ExtraPanes) pane.Dispose();
        tab.Control.Dispose();

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
            if (string.IsNullOrEmpty(tab.HighlightColor))
                tab.TabItem.Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            else
            {
                var rgb = ParseHexColor(tab.HighlightColor);
                tab.TabItem.Foreground = new SolidColorBrush(Color.FromRgb(
                    (byte)(255 - rgb.R), (byte)(255 - rgb.G), (byte)(255 - rgb.B)));
            }
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
        RebuildSplitLayout(tab);
        newPane.StartSession(tab.Command, tab.WorkingDirectory);
        UpdatePaneFocusVisuals(tab);
    }

    private void OnPaneFocused(TerminalTabState tab, TerminalControl pane)
    {
        if (tab.FocusedPane == pane) return;
        tab.FocusedPane = pane;
        UpdatePaneFocusVisuals(tab);
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

    private void SaveWindowState()
    {
        var state = new Models.WindowState
        {
            Left = RestoreBounds.Left,
            Top = RestoreBounds.Top,
            Width = RestoreBounds.Width,
            Height = RestoreBounds.Height,
            IsMaximized = WindowState == System.Windows.WindowState.Maximized,
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
                WorkingDirectory = tab.WorkingDirectory,
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
            ActivateTab(tab);
    }

    private void TabCloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TabItem tabItem && tabItem.Tag is TerminalTabState tab)
            CloseTab(tab);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
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
