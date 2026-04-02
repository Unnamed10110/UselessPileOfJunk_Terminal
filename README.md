# Useless Terminal

A fast Windows terminal frontend wrapper built with WPF + WebView2 + xterm.js, featuring a toggleable session management panel inspired by MobaXterm.

## Features

- **GPU-accelerated terminal** via xterm.js WebGL renderer in WebView2
- **ConPTY backend** for native Windows pseudo-console support
- **Tabbed interface** with close buttons, shell selector dropdown
- **Session panel** (Ctrl+B) for saving, editing, and launching terminal sessions
- **Auto-detects shells**: PowerShell 7, Windows PowerShell, CMD, WSL distros, Git Bash
- **Windows Terminal profile import** from WT settings.json
- **Dark theme** with Mica backdrop via WPF-UI (Fluent design)

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| Ctrl+B | Toggle session panel |
| Ctrl+T | New tab (default shell) |
| Ctrl+W | Close current tab |
| Ctrl+Tab | Next tab |
| Ctrl+Shift+Tab | Previous tab |
| Ctrl+1-9 | Switch to tab N |
| Ctrl+Shift+N | New session dialog |

## Requirements

- Windows 10 version 1809+ (for ConPTY)
- .NET 9 SDK
- WebView2 Runtime (pre-installed on Windows 10 21H2+ and Windows 11)

## Build & Run

```bash
dotnet build UselessTerminal.sln -c Debug
dotnet run --project src/UselessTerminal
```

## Architecture

```
WPF App (Fluent theme via WPF-UI)
├── Session Panel (XAML sidebar)
├── Tab Bar (custom TabControl)
└── Terminal Panes
    └── WebView2 Control
        └── xterm.js (WebGL GPU renderer)
            └── ConPTY (via P/Invoke)
```

## Tech Stack

- **UI Framework**: WPF + [WPF-UI](https://github.com/lepoco/wpfui) (Fluent design system)
- **Terminal Renderer**: [xterm.js](https://xtermjs.org/) v5 with WebGL addon
- **Shell Backend**: Windows ConPTY via direct P/Invoke
- **Session Storage**: JSON in `%APPDATA%/UselessTerminal/sessions.json`
