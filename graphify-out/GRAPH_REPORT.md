# Graph Report - UselessPileOfJunk_Terminal  (2026-09-07)

## Corpus Check
- 83 files · ~176,891 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2513 nodes · 4744 edges · 154 communities (86 shown, 66 thin omitted)
- Extraction: 96% EXTRACTED · 4% INFERRED · 0% AMBIGUOUS · INFERRED: 178 edges (avg confidence: 0.83)
- Token cost: 0 input · 903,025 output

## Community Hubs (Navigation)
- Xterm.js Core Terminal API
- Xterm.js Escape Sequence Handlers
- Settings Window UI
- ConPTY Terminal Control
- Session Panel & Folder UI
- Main Window Commands
- App Settings & Color Scheme
- Xterm.js Buffer & Viewport
- Session Edit Dialog & Shell Profiles
- Theme Presets
- Main Window Chrome & Tray
- Command Palette & File Conflict Dialogs
- Xterm.js Public Terminal API
- Xterm.js Parser Handler Registry
- ConPTY Win32 Interop
- Session Folder Management
- Core Feature Overview (README)
- Xterm.js Accessibility & Encodings
- Xterm.js Render Refresh Pipeline
- Session Tree Data Model
- Xterm.js Scrolling & CSI Params
- WebGL Renderer Addon Core
- WebGL Glyph Atlas
- WebGL Cell Color Model
- Tab & Pane Close Logic
- Search Addon (xterm-addon-search)
- SSH Connection & File Drop
- Tab Strip UI Interactions
- Xterm.js Options Management
- Xterm.js Cell Color Getters
- Image Addon (Sixel/iTerm)
- Session Drag/Drop & WT Import
- Xterm.js Buffer Line Editing
- WebGL Cursor & Selection Rendering
- Xterm.js Unicode Char Properties
- WebGL Cell Model Updates
- Xterm.js Selection & IME Events
- Xterm.js ESC Handler & Logging
- Shell File Drop Copy Logic
- App Namespace & Entry Points
- Image Addon Sixel Decoder
- Image Addon Canvas Management
- Unicode11 Addon
- Process Command-Line Enumeration
- Xterm.js CSI/DCS Handler Registration
- Embedded Browser Panel UI
- Xterm.js Mouse Selection Handling
- File Drop UI Feedback
- SSH Command Execution
- Xterm.js Link Hover Handling
- Xterm.js Decoration Refresh
- Session Tree Building & Filtering
- Window/Tab State Model
- Rename Tab Dialog
- Session Tree Drag Interactions
- Window Backdrop & UI Scaling
- WebGL Background Rendering
- Workspace Profile Storage
- Clipboard & Broadcast Input
- Main Window Action Handlers
- Tab Lifecycle & Git Branch
- WebGL Texture Lifecycle
- Snippets Storage
- App Screenshot: Main Window
- Terminal Session Logging
- Shell Icon Resolution
- Xterm.js Buffer Memory Cleanup
- Image Addon Cell Parsing
- Xterm.js Marker Management
- Font & UI Chrome Settings
- Settings Store Persistence
- WPF Value Converters
- Web Links Addon
- WebGL Character Rendering
- Terminal File-Drop Overlay UI
- Asciicast Recorder
- Command Completion Notifier
- Custom Keybinding Config
- App Startup & Elevation
- Xterm.js Smooth Scroll & Touch
- Converters (Multi-value)
- Terminal Tab Model
- Image Addon Storage Eviction
- Xterm.js Buffer Activation & Scroll Sync
- Settings-to-Theme JSON Bridge
- App Screenshot: AI Browser Panel
- Workspace Tab Model
- Demo GIF: Sessions Sidebar & Prompt
- App Screenshot: Tab Context Menu
- Demo GIF: Admin Window & Watermark
- Demo GIF: Edit Session Dialog
- Demo GIF: SSH Session Setup
- App Screenshot: Split-Pane SSH View
- Project Manifest & Dependencies
- WebGL Atlas Registration
- Xterm.js Contrast & Color Accessibility
- Legacy Settings Migration
- Minimap Scrollbar Feature
- Fit Addon
- Image Addon Mode Parsing
- SSH Config Importer
- Window State Store
- File Drag JS Handlers
- Session Tree Node Model
- App Screenshot: Appearance Settings
- Asciicast Recording Format
- PTY Output Base64 Decode
- Clickable File Path Links
- Command Completion Notifications
- Custom Keybindings Feature
- WebGL Renderer Claim vs Reality
- Retro CRT Mode
- Session Logging Feature
- Settings UI Feature
- Snippets Feature
- Profile Import (WT/SSH)
- Theme Presets & Window Backdrop
- App Icon (from EXE)
- App Icon (ICO Asset)
- App Icon (EXE Resource)
- Close Confirmation Feature
- Command Palette Feature
- Per-Session Environment Variables
- Per-Session Theme Overrides
- Quake Mode Feature
- Quick SSH Connect Feature
- Read-Only Mode Feature
- Session Folders Feature
- Session Import/Export Feature
- Shell Detector
- Status Bar Feature
- Custom Tab Bar
- Tab Groups Feature
- Tab Row Scroll Strip
- Multi-Pane Terminal Splits
- Window State Persistence
- App Icon (Placeholder)
- Fit Addon Instance
- Image Addon Instance
- Search Addon Instance
- Terminal Instance (xterm.js)
- Host API: termClear
- Host API: termFocus
- Host API: termResize
- Host API: Cursor Blink
- Host API: Cursor Style
- Host API: Font Family
- Host API: Scrollback
- Host API: Theme
- Unicode11 Addon Instance
- WebGL Addon Instance
- Web Links Addon Instance

## God Nodes (most connected - your core abstractions)
1. `MainWindow` - 117 edges
2. `k` - 105 edges
3. `SessionPanel` - 77 edges
4. `TerminalControl` - 71 edges
5. `constructor()` - 65 edges
6. `d` - 65 edges
7. `AppSettings` - 62 edges
8. `UserControl` - 59 edges
9. `TerminalTabState` - 58 edges
10. `P` - 54 edges

## Surprising Connections (you probably didn't know these)
- `toggleSearch()` --implements--> `Search Buffer`  [INFERRED]
  src/UselessTerminal/Assets/terminal.html → README.md
- `USE_WEBGL_TERMINAL_RENDERER flag (disabled by default)` --conceptually_related_to--> `GPU-accelerated WebGL Renderer (claimed)`  [AMBIGUOUS]
  src/UselessTerminal/Assets/terminal.html → README.md
- `forceFullRedraw() / window.termForceRedraw` --implements--> `WebGL Redraw-after-Clear Fix`  [INFERRED]
  src/UselessTerminal/Assets/terminal.html → README.md
- `doSearch()` --implements--> `'All Tabs' Search Broadcast`  [INFERRED]
  src/UselessTerminal/Assets/terminal.html → README.md
- `window.termSearchAll` --implements--> `'All Tabs' Search Broadcast`  [INFERRED]
  src/UselessTerminal/Assets/terminal.html → README.md

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Application Services Layer** — readme_settingsstore, readme_sessionstore, readme_workspacestore, readme_snippetstore, readme_terminallogger, readme_asciicastrecorder, readme_commandnotifier, readme_keybindingconfig, readme_sshconfigimporter, readme_shelldetector [EXTRACTED 1.00]
- **Host-Invokable Terminal API (window.term*)** — src_uselessterminal_assets_terminal_termwrite, src_uselessterminal_assets_terminal_termresize, src_uselessterminal_assets_terminal_termfocus, src_uselessterminal_assets_terminal_termclear, src_uselessterminal_assets_terminal_termsettheme, src_uselessterminal_assets_terminal_termsetfontsize, src_uselessterminal_assets_terminal_termsetfontfamily, src_uselessterminal_assets_terminal_termsetcursorblink, src_uselessterminal_assets_terminal_termsetcursorstyle, src_uselessterminal_assets_terminal_termsetscrollback, src_uselessterminal_assets_terminal_termapplysettingsraw, src_uselessterminal_assets_terminal_exportbuffer, src_uselessterminal_assets_terminal_termtoggleminimap, src_uselessterminal_assets_terminal_termtogglecrt, src_uselessterminal_assets_terminal_termsearchall, src_uselessterminal_assets_terminal_refitandnotifyhost, src_uselessterminal_assets_terminal_forcefullredraw [INFERRED 0.85]
- **WebGL/Canvas Ghost-Pixel Redraw Mitigation System** — src_uselessterminal_assets_terminal_forcefullredraw, src_uselessterminal_assets_terminal_scheduleredraw, src_uselessterminal_assets_terminal_scheduleredrawafterptyidle, src_uselessterminal_assets_terminal_repaintaftererasedisplay, src_uselessterminal_assets_terminal_csijhandler, src_uselessterminal_assets_terminal_csikhandler, src_uselessterminal_assets_terminal_bufferchangehandler, src_uselessterminal_assets_terminal_writeparsedhandler, src_uselessterminal_assets_terminal_osc133handler [INFERRED 0.85]
- **Useless Terminal Main Window Layout (Sidebar + Tabs + Terminal + Embedded Browser)** — ipcwqazue6_sessions_sidebar, ipcwqazue6_multi_tab_terminal, ipcwqazue6_embedded_ai_browser_panel [INFERRED 0.85]
- **Defaults Session Group (six shell profiles)** — uselessterminal_04_04_2026_16_09_09_sessions_panel, uselessterminal_04_04_2026_16_09_09_powershell_profile, uselessterminal_04_04_2026_16_09_09_windows_powershell_profile, uselessterminal_04_04_2026_16_09_09_wsl_profile, uselessterminal_04_04_2026_16_09_09_wsl_kalilinux_profile, uselessterminal_04_04_2026_16_09_09_cmd_profile, uselessterminal_04_04_2026_16_09_09_git_bash_profile [EXTRACTED 1.00]
- **Multi-Profile Terminal Shell UI (sidebar + tabs + profile launch)** — uselessterminal_04_04_2026_16_11_45_sessions_sidebar, uselessterminal_04_04_2026_16_11_45_shell_profiles, uselessterminal_04_04_2026_16_11_45_tab_management [INFERRED 0.75]
- **Tab Context Menu Feature Group** — uselessterminal_04_04_2026_16_12_57_tab_context_menu, uselessterminal_04_04_2026_16_12_57_tab_color_picker, uselessterminal_04_04_2026_16_12_57_pane_splitting, uselessterminal_04_04_2026_16_12_57_save_as_session [INFERRED 0.75]
- **Appearance Settings Dialog Grouping** — uselessterminal_04_04_2026_16_14_14, uselessterminal_04_04_2026_16_14_14_font_settings, uselessterminal_04_04_2026_16_14_14_shell_background, uselessterminal_04_04_2026_16_14_14_ansi_color_scheme [EXTRACTED 1.00]
- **Multi-Shell Customizable Terminal Experience** — uselessterminal_04_04_2026_16_16_59_session_sidebar, uselessterminal_04_04_2026_16_16_59_tabbed_interface, uselessterminal_04_04_2026_16_16_59_custom_prompt [INFERRED 0.75]
- **SSH Session Setup Workflow** — uselessterminal_04_04_2026_16_25_48_session_management_ui, uselessterminal_04_04_2026_16_25_48_edit_session_dialog, uselessterminal_04_04_2026_16_25_48_ssh_session_config [INFERRED 0.75]
- **Terminal Session Organization Features** — uselessterminal_04_04_2026_16_25_48_multi_tab_terminal, uselessterminal_04_04_2026_16_25_48_color_tag_feature, uselessterminal_04_04_2026_16_25_48_session_management_ui [INFERRED 0.65]
- **Session configuration workflow (sidebar list, edit dialog, SSH preset)** — uselessterminal_05_04_2026_03_47_50_edit_session_dialog, uselessterminal_05_04_2026_03_47_50_sessions_sidebar, uselessterminal_05_04_2026_03_47_50_ssh_session_preset [INFERRED 0.75]
- **Terminal Multiplexer UI (sidebar + tabs + split panes)** — uselessterminal_05_04_2026_07_13_38_session_sidebar, uselessterminal_05_04_2026_07_13_38_tabbed_terminal, uselessterminal_05_04_2026_07_13_38_split_pane_view [INFERRED 0.80]

## Communities (154 total, 66 thin omitted)

### Community 0 - "Xterm.js Core Terminal API"
Cohesion: 0.03
Nodes (27): addDecoration(), _addLineToZone(), addOscHandler(), areSelectionValuesReversed(), compositionend(), compositionupdate(), _finalizeComposition(), finalSelectionEnd() (+19 more)

### Community 2 - "Settings Window UI"
Cohesion: 0.06
Nodes (45): IntPtr, IWin32Window, Property, Label, ColorGrid, CursorBlinkBox, CursorStyleBox, FontFamilyBox (+37 more)

### Community 3 - "ConPTY Terminal Control"
Cohesion: 0.06
Nodes (21): Cols, Command, CoreWebView2NavigationCompletedEventArgs, Queue, Rows, b, JsonSerializerOptions, r (+13 more)

### Community 4 - "Session Panel & Folder UI"
Cohesion: 0.10
Nodes (6): DependencyObject, HashSet, List, Point, RoutedEventArgs, SessionPanel

### Community 5 - "Main Window Commands"
Cohesion: 0.05
Nodes (48): CloseTabCommand, NewTabCommand, Title, TogglePanelCommand, DragCompletedEventArgs, BrowserGridSplitter, BrowserPanel, BrowserPanelChrome (+40 more)

### Community 6 - "App Settings & Color Scheme"
Cohesion: 0.04
Nodes (46): AppSettings, ColorAccent, ColorCommand, ColorError, ColorHighlight, ColorInput, ColorMessage, ColorWarning (+38 more)

### Community 7 - "Xterm.js Buffer & Viewport"
Cohesion: 0.08
Nodes (21): _convertViewportColToCharacterIndex(), createRow(), fillViewportRows(), fire(), getBlankLine(), getBufferElements(), getJoinedCharacters(), _getJoinedRanges() (+13 more)

### Community 8 - "Session Edit Dialog & Shell Profiles"
Cohesion: 0.07
Nodes (28): RadioButton, ArgumentsBox, ColorBlue, DescriptionBox, EnvVarsBox, NameBox, PresetShellCombo, ShellPathBox (+20 more)

### Community 9 - "Theme Presets"
Cohesion: 0.06
Nodes (38): b, Dictionary, r, ThemePreset, ColorAccent, ColorCommand, ColorError, ColorHighlight (+30 more)

### Community 10 - "Main Window Chrome & Tray"
Cohesion: 0.07
Nodes (13): CancelEventArgs, HwndSource, NotifyIcon, SizeChangedEventArgs, DependencyObject, DispatcherTimer, DllImport, EventArgs (+5 more)

### Community 11 - "Command Palette & File Conflict Dialogs"
Cohesion: 0.06
Nodes (30): Label, Shortcut, Bd, ResultsList, SearchBox, Window, KeyEventArgs, List (+22 more)

### Community 14 - "ConPTY Win32 Interop"
Cohesion: 0.09
Nodes (23): CancellationTokenSource, COORD, UselessTerminal.Interop, FileStream, LibraryImport, PROCESS_INFORMATION, SafeFileHandle, SECURITY_ATTRIBUTES (+15 more)

### Community 15 - "Session Folder Management"
Cohesion: 0.12
Nodes (15): SessionFolder, Id, Name, ParentId, SortOrder, IReadOnlyList, JsonSerializerOptions, List (+7 more)

### Community 16 - "Core Feature Overview (README)"
Cohesion: 0.06
Nodes (37): Browser Panel, ConPTY, Font Zoom, OSC 133 (Shell Integration / Prompt Markers), OSC 7 (CWD Tracking), Session Panel, SessionStore, Shell CWD Integration Strategy (+29 more)

### Community 17 - "Xterm.js Accessibility & Encodings"
Cohesion: 0.07
Nodes (38): addEncoding(), addProtocol(), _announceCharacters(), _clearLiveRegion(), clearRange(), clearTextureAtlas(), constructor(), _createAccessibilityTreeNode() (+30 more)

### Community 18 - "Xterm.js Render Refresh Pipeline"
Cohesion: 0.07
Nodes (3): P, selectAll(), selectLines()

### Community 19 - "Session Tree Data Model"
Cohesion: 0.06
Nodes (32): Children, Command, HexColor, SessionShellIcon, Folder.Id, Folder.Name, IsExpanded, IsMultiSelected (+24 more)

### Community 21 - "WebGL Renderer Addon Core"
Cohesion: 0.08
Nodes (18): clearListeners(), constructor(), debug(), dispose(), error(), _evalLazyOptionalParams(), info(), _log() (+10 more)

### Community 22 - "WebGL Glyph Atlas"
Cohesion: 0.09
Nodes (5): d(), g, get(), p(), v

### Community 24 - "Tab & Pane Close Logic"
Cohesion: 0.07
Nodes (27): Grid, GridSplitter, List, TerminalTabState, AllPanes, BroadcastInput, Command, Container (+19 more)

### Community 25 - "Search Addon (xterm-addon-search)"
Cohesion: 0.12
Nodes (5): clearListeners(), dispose(), i(), n, register()

### Community 26 - "SSH Connection & File Drop"
Cohesion: 0.09
Nodes (15): HashSet, List, SshConnection, ConfigFile, ControlPath, DisplayTarget, ExtraDashO, Host (+7 more)

### Community 27 - "Tab Strip UI Interactions"
Cohesion: 0.07
Nodes (15): ICommand, MouseWheelEventArgs, ScrollViewer, TabStrip, Action, Button, DragEventArgs, MouseButtonEventArgs (+7 more)

### Community 30 - "Image Addon (Sixel/iTerm)"
Cohesion: 0.11
Nodes (20): a(), activate(), constructor(), _da1(), _decrst(), _decset(), _dim(), _disposeLater() (+12 more)

### Community 31 - "Session Drag/Drop & WT Import"
Cohesion: 0.08
Nodes (21): IReadOnlyList, SavedSession, Arguments, ColorTag, Description, DisplayCommand, EnvironmentVariables, FolderId (+13 more)

### Community 33 - "WebGL Cursor & Selection Rendering"
Cohesion: 0.13
Nodes (3): C, pause(), restartBlinkAnimation()

### Community 35 - "WebGL Cell Model Updates"
Cohesion: 0.11
Nodes (4): L(), m, n, resolve()

### Community 37 - "Xterm.js ESC Handler & Logging"
Cohesion: 0.09
Nodes (17): addEscHandler(), _cancelCallback(), debug(), error(), _evalLazyOptionalParams(), _handleSelectionChange(), hook(), info() (+9 more)

### Community 38 - "Shell File Drop Copy Logic"
Cohesion: 0.12
Nodes (15): CopyResult, Action, IReadOnlyList, List, CopyResult, Copied, CopiedNames, Errors (+7 more)

### Community 39 - "App Namespace & Entry Points"
Cohesion: 0.14
Nodes (5): UselessTerminal, UselessTerminal.Controls, UselessTerminal.Services, UselessTerminal.Models, ElevatedShellLauncher

### Community 40 - "Image Addon Sixel Decoder"
Cohesion: 0.09
Nodes (3): i(), init(), release()

### Community 41 - "Image Addon Canvas Management"
Cohesion: 0.14
Nodes (5): end(), getImageAtBufferCell(), r(), register(), unhook()

### Community 42 - "Unicode11 Addon"
Cohesion: 0.12
Nodes (7): activate(), charProperties(), clearListeners(), d(), dispose(), n, wcwidth()

### Community 43 - "Process Command-Line Enumeration"
Cohesion: 0.21
Nodes (10): ProcessEntry32W, ProcessInfo, Dictionary, DllImport, List, MarshalAs, ProcessCommandLines, ProcessEntry32W (+2 more)

### Community 44 - "Xterm.js CSI/DCS Handler Registration"
Cohesion: 0.10
Nodes (18): addCsiHandler(), addDcsHandler(), addRefreshCallback(), delete(), _equalEvents(), forEachByKey(), getKeyIterator(), _innerRefresh() (+10 more)

### Community 45 - "Embedded Browser Panel UI"
Cohesion: 0.14
Nodes (14): AddressBar, BackBtn, BrowserWebView, ForwardBtn, RefreshBtn, UserControl, KeyEventArgs, RoutedEventArgs (+6 more)

### Community 46 - "Xterm.js Mouse Selection Handling"
Cohesion: 0.11
Nodes (21): _addMouseDownListeners(), _areCoordsInSelection(), _dragScroll(), _getMouseBufferCoords(), _getMouseEventScrollAmount(), getWrappedRangeForLine(), _handleDoubleClick(), _handleIncrementalClick() (+13 more)

### Community 47 - "File Drop UI Feedback"
Cohesion: 0.14
Nodes (7): CoreWebView2WebMessageReceivedEventArgs, DropDestination, DispatcherTimer, DragEventArgs, EventArgs, DropDestination, TerminalMessage

### Community 48 - "SSH Command Execution"
Cohesion: 0.22
Nodes (8): IList, RunResult, Action, IReadOnlyList, List, RunResult, SshFileDrop, TimeSpan

### Community 49 - "Xterm.js Link Hover Handling"
Cohesion: 0.13
Nodes (19): _askForLink(), _checkLinkProviderResult(), _clearCurrentLink(), clearSelection(), _createLinkUnderlineEvent(), _fireEventIfSelectionChanged(), _fireOnSelectionChange(), _fireUnderlineEvent() (+11 more)

### Community 50 - "Xterm.js Decoration Refresh"
Cohesion: 0.13
Nodes (16): _createElement(), _doRefreshDecorations(), _queueRefresh(), _refreshCanvasDimensions(), _refreshColorZonePadding(), _refreshDecorations(), _refreshDrawConstants(), _refreshDrawHeightConstants() (+8 more)

### Community 51 - "Session Tree Building & Filtering"
Cohesion: 0.12
Nodes (11): IEnumerable, INotifyPropertyChanged, ItemsControl, ObservableCollection, ObservableCollection, SessionTreeNode, Children, Folder (+3 more)

### Community 52 - "Window/Tab State Model"
Cohesion: 0.11
Nodes (18): List, TabState, Command, HighlightColor, Renamed, StartingCommand, Title, WorkingDirectory (+10 more)

### Community 53 - "Rename Tab Dialog"
Cohesion: 0.15
Nodes (7): NameBox, Window, KeyEventArgs, RoutedEventArgs, RenameDialog, ResultName, TextBox

### Community 54 - "Session Tree Drag Interactions"
Cohesion: 0.14
Nodes (10): SessionTree, SnippetList, Button, DragEventArgs, MouseButtonEventArgs, MouseEventArgs, RoutedPropertyChangedEventArgs, ListBox (+2 more)

### Community 55 - "Window Backdrop & UI Scaling"
Cohesion: 0.16
Nodes (3): ContextMenu, SolidColorBrush, Border

### Community 57 - "Workspace Profile Storage"
Cohesion: 0.18
Nodes (10): List, WorkspaceProfile, Id, Name, Tabs, JsonSerializerOptions, List, WorkspaceStore (+2 more)

### Community 58 - "Clipboard & Broadcast Input"
Cohesion: 0.17
Nodes (15): Broadcast Input, Copy / Paste Shortcuts, Export Buffer to File Shortcut, 'All Tabs' Search Broadcast, Search Buffer, copySelectionToClipboard(), Context Menu Click Handler, attachCustomKeyEventHandler callback (+7 more)

### Community 61 - "Tab Lifecycle & Git Branch"
Cohesion: 0.23
Nodes (3): Orientation, Dictionary, TerminalControl

### Community 63 - "Snippets Storage"
Cohesion: 0.21
Nodes (10): Snippet, Command, Id, Name, SortOrder, JsonSerializerOptions, List, SnippetStore (+2 more)

### Community 64 - "App Screenshot: Main Window"
Cohesion: 0.21
Nodes (15): Useless Terminal Application, Existential Banner Quote ('I can be free, it just takes making the last decision I will ever make'), Command Prompt Session Profile (cmd.exe), Custom Shell Prompt (troja skull/ghost - unnamed - lambda glyph), Terminal Date/Time Display Widget, Defaults Session Group, Foolish Tab (Active Terminal Tab), Git Bash Session Profile (bash.exe --login -i) (+7 more)

### Community 65 - "Terminal Session Logging"
Cohesion: 0.20
Nodes (6): GeneratedRegex, Regex, StreamWriter, TerminalLogger, IsLogging, LogFilePath

### Community 66 - "Shell Icon Resolution"
Cohesion: 0.25
Nodes (5): ImageSource, ShellGlyphResolver, Dictionary, ShellIconLoader, FallbackIcon

### Community 67 - "Xterm.js Buffer Memory Cleanup"
Cohesion: 0.16
Nodes (9): _batchedMemoryCleanup(), clear(), _getCorrectBufferLength(), getNullCell(), _reflow(), _reflowLarger(), _reflowLargerAdjustViewport(), resize() (+1 more)

### Community 68 - "Image Addon Cell Parsing"
Cohesion: 0.15
Nodes (5): parse(), s(), _storeKey(), _storeValue(), t()

### Community 69 - "Xterm.js Marker Management"
Cohesion: 0.18
Nodes (11): addLineToLink(), addMarker(), clearAllMarkers(), clearListeners(), clearMarkers(), dispose(), _getEntryIdKey(), registerLink() (+3 more)

### Community 70 - "Font & UI Chrome Settings"
Cohesion: 0.23
Nodes (7): FontFamily, FontWeight, DependencyObject, UiChrome, TextFormattingMode, TextHintingMode, TextRenderingMode

### Community 71 - "Settings Store Persistence"
Cohesion: 0.26
Nodes (4): JsonSerializerOptions, SettingsStore, Current, Instance

### Community 72 - "WPF Value Converters"
Cohesion: 0.23
Nodes (7): IValueConverter, CultureInfo, Type, HexColorConverter, CultureInfo, Type, SavedSessionShellIconConverter

### Community 73 - "Web Links Addon"
Cohesion: 0.24
Nodes (4): _addCallbacks(), n(), o, provideLinks()

### Community 75 - "Terminal File-Drop Overlay UI"
Cohesion: 0.23
Nodes (11): DimOverlay, FileDropCard, FileDropLabel, FileDropOverlay, FileDropTitle, FocusBorder, UserControl, WebView (+3 more)

### Community 76 - "Asciicast Recorder"
Cohesion: 0.21
Nodes (5): StreamWriter, AsciicastRecorder, FilePath, IsRecording, Stopwatch

### Community 77 - "Command Completion Notifier"
Cohesion: 0.18
Nodes (6): IDisposable, DispatcherTimer, EventArgs, CommandNotifier, Enabled, WindowIsActive

### Community 78 - "Custom Keybinding Config"
Cohesion: 0.20
Nodes (6): Dictionary, JsonSerializerOptions, Key, KeyBindingConfig, Bindings, Instance

### Community 79 - "App Startup & Elevation"
Cohesion: 0.24
Nodes (4): Application, App, ElevationHelper, StartupEventArgs

### Community 80 - "Xterm.js Smooth Scroll & Touch"
Cohesion: 0.29
Nodes (10): _applyScrollModifier(), _bubbleScroll(), _clearSmoothScrollState(), getLinesScrolled(), _getPixelsScrolled(), handleTouchMove(), handleWheel(), scrollLines() (+2 more)

### Community 81 - "Converters (Multi-value)"
Cohesion: 0.28
Nodes (5): UselessTerminal.Converters, IMultiValueConverter, CultureInfo, Type, TreeViewItemHeaderMaxWidthConverter

### Community 82 - "Terminal Tab Model"
Cohesion: 0.22
Nodes (7): TerminalControl, TerminalTab, Id, ShellCommand, TerminalControl, Title, WorkingDirectory

### Community 83 - "Image Addon Storage Eviction"
Cohesion: 0.39
Nodes (8): addImage(), _delImg(), dispose(), _evictOldest(), _evictOnAlternate(), _getStoredPixels(), getUsage(), wipeAlternate()

### Community 84 - "Xterm.js Buffer Activation & Scroll Sync"
Cohesion: 0.25
Nodes (7): disable(), end(), _handleBufferActivate(), handleTrim(), reset(), syncScrollArea(), unhook()

### Community 85 - "Settings-to-Theme JSON Bridge"
Cohesion: 0.25
Nodes (4): AppSettings, Color, Dictionary, JsonElement

### Community 86 - "App Screenshot: AI Browser Panel"
Cohesion: 0.33
Nodes (7): AI Provider Quick-Switch Bar (ChatGPT/DeepSeek/Claude/Gemini/Copilot/Perplexity/Grok), Custom Shell Prompt with Git Branch & Quote/Timestamp, Embedded AI Chat Browser Panel, Multi-Tab Terminal Interface, Session Profile Types (PowerShell 7, Windows PowerShell, WSL, WSL:kali-linux, Command Prompt, Git Bash, SSH server), Sessions Sidebar (Session Groups & Profiles), Useless Terminal App Screenshot

### Community 87 - "Workspace Tab Model"
Cohesion: 0.29
Nodes (6): WorkspaceTab, Command, SessionId, StartingCommand, Title, WorkingDirectory

### Community 88 - "Demo GIF: Sessions Sidebar & Prompt"
Cohesion: 0.38
Nodes (7): Faint Animal-Line-Art Background Watermark, Custom Styled Shell Prompt (troja/wolf/skull glyphs, lambda marker), MOTD-style Banner Text with Timestamp Clock, UselessTerminal Demo GIF (04-Apr-2026), Sessions Sidebar with Profile Groups, Defaults Shell Profile List (PowerShell, WSL, CMD, Git Bash), Tab Bar with Named Tab ('Foolish Tab') and Window Chrome

### Community 89 - "App Screenshot: Tab Context Menu"
Cohesion: 0.33
Nodes (7): Close Tab / Close Other Tabs / Close Tabs to the Right, Pane Splitting (Add Pane max 4 / Unsplit All), Duplicate Tab / Save as Session feature, Useless Terminal Tab Context Menu Screenshot, Session Profiles Sidebar (Defaults group), Tab Color Picker Submenu, Tab Right-Click Context Menu

### Community 90 - "Demo GIF: Admin Window & Watermark"
Cohesion: 0.43
Nodes (7): Administrator-Elevated Window Title, Custom Themed Shell Prompt (troja/skull/lambda), Useless Terminal Demo GIF (2026-04-04), Nihilistic MOTD Banner Text, Sessions Sidebar (Multi-Shell Profiles), Tabbed Terminal Interface, Rabbit/Skull Watermark Logo

### Community 91 - "Demo GIF: Edit Session Dialog"
Cohesion: 0.38
Nodes (7): Color-coded Session Tags, Dark-humor Branding Tagline ("...I can be free, it just takes making the last decision I will ever make..."), Useless Terminal - Session Editor Demo (GIF), Edit Session Dialog, Multi-tab Terminal Interface ("Foolish Tab"), Session Management Sidebar (Sessions list, folders, import/export), SSH Session Configuration (shell path, starting command)

### Community 92 - "Demo GIF: SSH Session Setup"
Cohesion: 0.48
Nodes (7): Useless Terminal demo GIF (Edit Session dialog), Session color tag picker, Custom shell prompt theme (skull/"troja" oh-my-posh style prompt with git status), Edit Session modal dialog, Sessions sidebar with Defaults group, "server 128.168.0.1" SSH session preset, Tabbed terminal UI (Foolish Tab / server tab)

### Community 93 - "App Screenshot: Split-Pane SSH View"
Cohesion: 0.48
Nodes (7): Custom Shell Prompt Theme (ASCII art + colored segments), Useless Terminal UI Screenshot (05-Apr-2026), Session Profile Sidebar (Defaults group), Multiple Shell Profile Types (PowerShell, WSL, CMD, Git Bash), Split-Pane Terminal View, SSH Remote Server Session Profile (128.168.0.1), Tabbed Terminal Interface

### Community 94 - "Project Manifest & Dependencies"
Cohesion: 0.33
Nodes (4): net9.0-windows, Microsoft.Web.WebView2 (1.0.2903.40), WPF-UI (3.0.5), Microsoft.NET.Sdk

### Community 96 - "Xterm.js Contrast & Color Accessibility"
Cohesion: 0.40
Nodes (5): _addStyle(), _applyMinimumContrast(), getColor(), _getContrastCache(), setColor()

### Community 98 - "Minimap Scrollbar Feature"
Cohesion: 0.60
Nodes (5): Minimap Scrollbar, minimapEnabled (shared state), renderMinimap(), scheduleMinimapRender(), window.termToggleMinimap

### Community 102 - "Window State Store"
Cohesion: 0.50
Nodes (3): JsonSerializerOptions, WindowStateStore, WindowState

### Community 104 - "Session Tree Node Model"
Cohesion: 0.50
Nodes (3): SessionTreeNodeKind, Folder, Session

### Community 105 - "App Screenshot: Appearance Settings"
Cohesion: 0.67
Nodes (4): Terminal Appearance Settings Screenshot, Prompt & Output ANSI Color Mapping, Font Settings Panel (Family, Size, Cursor Style/Blink, Scrollback), Shell Background Image Setting (image path + opacity)

### Community 106 - "Asciicast Recording Format"
Cohesion: 0.67
Nodes (3): Asciicast v2 Format, AsciicastRecorder, Terminal Recording (Asciicast)

## Ambiguous Edges - Review These
- `GPU-accelerated WebGL Renderer (claimed)` → `USE_WEBGL_TERMINAL_RENDERER flag (disabled by default)`  [AMBIGUOUS]
  src/UselessTerminal/Assets/terminal.html · relation: conceptually_related_to

## Knowledge Gaps
- **362 isolated node(s):** `TextBox`, `WebView2`, `TextBox`, `ListBox`, `Border` (+357 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 829 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **66 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What is the exact relationship between `GPU-accelerated WebGL Renderer (claimed)` and `USE_WEBGL_TERMINAL_RENDERER flag (disabled by default)`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **Why does `constructor()` connect `Xterm.js Accessibility & Encodings` to `Xterm.js Core Terminal API`, `Xterm.js Unicode Char Properties`, `Xterm.js Buffer Memory Cleanup`, `Xterm.js Selection & IME Events`, `Xterm.js ESC Handler & Logging`, `Xterm.js Marker Management`, `Xterm.js Buffer & Viewport`, `Image Addon Canvas Management`, `Xterm.js CSI/DCS Handler Registration`, `Xterm.js Public Terminal API`, `Xterm.js Parser Handler Registry`, `Xterm.js Mouse Selection Handling`, `Xterm.js Link Hover Handling`, `Xterm.js Decoration Refresh`, `Xterm.js Buffer Activation & Scroll Sync`, `WebGL Glyph Atlas`, `Xterm.js Hyperlink Cell Attributes`, `Xterm.js Options Management`?**
  _High betweenness centrality (0.083) - this node is a cross-community bridge._
- **Why does `p()` connect `WebGL Glyph Atlas` to `Xterm.js Accessibility & Encodings`, `WebGL Renderer Addon Core`, `Xterm.js Buffer & Viewport`?**
  _High betweenness centrality (0.070) - this node is a cross-community bridge._
- **Why does `TerminalControl` connect `ConPTY Terminal Control` to `App Namespace & Entry Points`, `Terminal File-Drop Overlay UI`, `Command Completion Notifier`, `ConPTY Win32 Interop`, `File Drop UI Feedback`, `Embedded Browser Panel UI`, `Settings-to-Theme JSON Bridge`, `SSH Connection & File Drop`?**
  _High betweenness centrality (0.042) - this node is a cross-community bridge._
- **Are the 4 inferred relationships involving `constructor()` (e.g. with `.document()` and `p()`) actually correct?**
  _`constructor()` has 4 INFERRED edges - model-reasoned connections that need verification._
- **What connects `TextBox`, `WebView2`, `TextBox` to the rest of the system?**
  _362 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Xterm.js Core Terminal API` be split into smaller, more focused modules?**
  _Cohesion score 0.027359781121751026 - nodes in this community are weakly interconnected._