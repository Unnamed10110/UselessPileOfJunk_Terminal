# Useless Terminal

A fast Windows terminal frontend wrapper built with WPF + WebView2 + xterm.js, featuring a toggleable session management panel inspired by MobaXterm.

## Features

- **GPU-accelerated terminal** via xterm.js WebGL renderer in WebView2
- **ConPTY backend** for native Windows pseudo-console support
- **Tabbed interface** with close buttons, shell selector dropdown, and optional per-tab highlight colors (active tab uses a white background with black text)
- **Session panel** (Ctrl+B) for saving, editing, and launching terminal sessions
- **Session folders**: organize sessions in a tree; drag-and-drop to reorder or move between folders and root; expand/collapse with folder row affordances
- **Session import/export** (JSON) and **import from Windows Terminal** profiles
- **Auto-detects shells**: PowerShell 7, Windows PowerShell, CMD, WSL distros, Git Bash
- **Settings** (`settings.json` in `%APPDATA%/UselessTerminal/`):
  - **Prompt & output colors** — semantic colors (errors, warnings, commands, messages, paths, etc.) mapped to the xterm ANSI theme so shells and prompts stay readable
  - **Color picker** — Windows color dialog for each color entry (with hex editing still supported)
  - **Font** — choose from **all installed system fonts** in the family dropdown (editable for custom stacks)
  - Font size, cursor style/blink, scrollback, optional shell background image + opacity
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

## Usage
- Terminal appearence

![View](UselessTerminal_04_04_2026_16_09_09.png)


- Add any default shell

![Default shell](UselessTerminal_04_04_2026_16_11_45.gif)

- Tab options

![tab options](UselessTerminal_04_04_2026_16_12_57.png)

- Terminal Settings

![terminal settings](UselessTerminal_04_04_2026_16_14_14.png)

- Custom new session

![new custom session](UselessTerminal_04_04_2026_16_16_59.gif)

- Start command example

<img src="UselessTerminal_05_04_2026_03_47_50.gif" alt="run example" width="100%">

## Requirements

- Windows 10 version 1809+ (for ConPTY)
- .NET 9 SDK
- WebView2 Runtime (pre-installed on Windows 10 21H2+ and Windows 11)

## Build & Run

```bash
dotnet build UselessTerminal.sln -c Debug
dotnet run --project src/UselessTerminal
```

### Standalone publish (repository root)

From the repo root, `build.bat` publishes a **self-contained** `win-x64` app to the `publish` folder (see script for options).

## Architecture

```
WPF App (Fluent theme via WPF-UI)
├── Session Panel (XAML sidebar, tree + folders)
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
- **Session storage**: JSON in `%APPDATA%/UselessTerminal/sessions.json` (sessions + folders)

## Credits

- **Developer:** Unnamed10110  
- **Contact:** [trojan.v6@gmail.com](mailto:trojan.v6@gmail.com) · [sergiobritos10110@gmail.com](mailto:sergiobritos10110@gmail.com) (secondary)
