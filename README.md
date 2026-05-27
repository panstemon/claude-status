# Claude Status

A lightweight Windows **system-tray** app that shows your live Claude subscription
limits — the 5-hour session window, the weekly (all-models) window, and the
per-model weekly windows (Opus / Sonnet) — the same numbers you see in Claude
Code's `/usage`.

- **Tray badge** that always shows the most-constrained limit as a number,
  tinted green / amber / red by severity.
- **Left-click** the tray icon → a clean, borderless flyout with a progress bar
  and reset time for each limit window.
- **Right-click** → menu: *Start with Windows*, *Refresh interval*,
  *Refresh now*, *Quit*.
- **No separate login.** It reuses the Claude.ai OAuth token that Claude Code
  already stores, and **refreshes it itself** so the tray keeps working even when
  Claude Code isn't running.

## How it works

| Piece | Detail |
|---|---|
| Usage data | `GET https://api.anthropic.com/api/oauth/usage` with the OAuth bearer token, `anthropic-beta: oauth-2025-04-20`, and a `claude-code/<ver>` User-Agent. |
| Token | Read from `%USERPROFILE%\.claude\.credentials.json` (`claudeAiOauth`). |
| Token refresh | When expired, `POST https://claude.ai/v1/oauth/token` (grant_type=refresh_token) and the new token is written back to the same file. |
| Autostart | Per-user `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value (no admin needed). |
| Settings | `%APPDATA%\ClaudeStatus\settings.json` (poll interval). |

> ⚠️ The usage endpoint is **undocumented** and may change without notice. All of
> it is isolated in `src/UsageClient.cs` so it's easy to adjust.

## Requirements

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (to build) — the runtime
  is enough to run a published build.
- A Claude.ai subscription already signed in via Claude Code.

## Run from source

```powershell
dotnet run --project src
```

## Build a standalone exe (recommended for autostart)

Framework-dependent (needs the .NET 10 Desktop runtime, which the SDK installs):

```powershell
dotnet publish src -c Release -r win-x64 --self-contained false -o publish
```

Then run `publish\ClaudeStatus.exe` once and toggle **Start with Windows** from the
right-click menu — autostart will point at that exe.

## Project layout

```
src/
├── App.xaml(.cs)        # tray icon, context menu, poll loop
├── FlyoutWindow.xaml    # the borderless status popup
├── UsageClient.cs       # usage endpoint + OAuth token refresh
├── CredentialStore.cs   # read/write ~/.claude/.credentials.json
├── IconRenderer.cs      # dynamic %+color tray badge
├── AutostartManager.cs  # HKCU Run-key toggle
├── AppSettings.cs       # settings persistence
├── Severity.cs          # shared green/amber/red palette
└── Models.cs            # data models
```
