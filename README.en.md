# LSM Display Resizer

[![Build](https://github.com/nes19233/LSMDisplayResizer/actions/workflows/build.yml/badge.svg)](https://github.com/nes19233/LSMDisplayResizer/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

[Deutsch](README.md) | **English**

A free, open-source Windows tray app that **automatically adjusts a window's size to the target monitor** as soon as the
window is moved to another monitor.

With monitors of different resolution or scaling, a window often looks too big or too small after moving it and has to be
resized by hand every time. LSM Display Resizer does that for you.

## Features

- Runs quietly in the notification area – no window at startup. Interface in **English and German**.
- Detects when a window lands on another monitor: after dragging with the mouse, with **Win + Shift + Arrow**, or when a
  program moves its own window.
- Two modes:
  - **Percentage** (default): size and position are transferred relative to the work area (screen without taskbar) –
    60 % width on monitor A → 60 % width on monitor B. The height scales in a damped way (default 67 %); different display
    scaling (e.g. laptop 200 %, monitor 100 %) is taken into account.
  - **Fixed rules**: a target size per program and monitor, in percent or pixels.
- **Docking/undocking:** after Windows has rearranged the windows, the app adjusts each moved window as soon as it stops
  moving (usually after 2–3 seconds), based on its state before the change. The 1024×768 placeholder display Windows
  briefly activates when you unplug with the lid closed is skipped: windows are adjusted on the next real monitor.
- **Layout per monitor setup:** for every combination of connected monitors (recognized by EDID serial number, since Windows often assigns new device IDs when docking) the app remembers
  which monitor each window was on. When you return to a known setup, windows go back – normal windows with position and
  size, maximized windows stay maximized and only switch monitor.
- Remembers each window's **last normal size** – also across maximizing and snapping. Windows that only fill the full
  screen height (snapped) are treated like maximized ones.
- **Wallpaper across monitors:** one large image is spread seamlessly over all monitors (by physical size via EDID or by
  pixels), recreated automatically when the monitor arrangement changes. Picture-by-picture halves of one monitor are
  detected. The previous wallpaper is backed up and can be restored; if Windows does not accept a new image, the previous
  wallpaper is kept.
- **Monitor separator line** (optional, off by default): a very thin line (1–4 px, color/opacity adjustable) where one
  screen ends – e.g. on a split ultrawide monitor.
- Optional **hotkey** for the active window, **exclusion list**, **start with Windows**, log window.

Never adjusted: maximized, minimized, snapped and full-screen windows, invisible/cloaked windows, tool and child windows,
windows without a resizable frame, the Windows shell, and programs on the exclusion list.

## Installation

From [Releases](../../releases) (Windows 10 or 11, 64-bit; no .NET installation needed):

| File | Purpose |
|---|---|
| `LSMDisplayResizer-x.y.z-win-x64-en.msi` | **Recommended.** Installer (English) with Start menu entry, autostart, "Apps" entry and updates |
| `LSMDisplayResizer-x.y.z-win-x64-de.msi` | The same installer in German |
| `LSMDisplayResizer-x.y.z-win-x64.exe` | Portable single file, no installation |

**Double-click** the MSI to install for the current user into `%LocalAppData%\Programs\LSM Display Resizer\` –
**no administrator rights required**. Uninstall via Settings → Apps.

**Updates:** once a day the app asks GitHub for a new version and shows a notification. "Update now" downloads the MSI,
verifies it against the SHA-256 checksum from the release, installs it and restarts the app – no administrator rights,
settings are kept. Manually: tray menu → **Check for updates …**, or simply install a newer MSI.

**For all users / IT deployment** (with administrator rights, e.g. via Intune, SCCM or script):

```powershell
msiexec /i LSMDisplayResizer-x.y.z-win-x64-en.msi ALLUSERS=1 /qn               # to C:\Program Files, autostart for all
msiexec /i LSMDisplayResizer-x.y.z-win-x64-en.msi ALLUSERS=1 AUTOSTART=0 /qn   # same without autostart
msiexec /i LSMDisplayResizer-x.y.z-win-x64-en.msi ALLUSERS=1 UPDATECHECK=0 /qn # without update check
msiexec /x LSMDisplayResizer-x.y.z-win-x64-en.msi /qn                          # uninstall
```

The app itself always runs in the user context. With a per-machine installation it does not install updates itself but
only points them out – deployment stays with IT. With `UPDATECHECK=0` it never contacts GitHub (registry value
`UpdateCheck` = 0 under `HKLM` or `HKCU\Software\LSM Display Resizer`); the setting is then locked in the app.

The files are not signed: SmartScreen may warn on download, and
AppLocker/WDAC policies may block unsigned programs in user folders – installing per machine (`ALLUSERS=1`) into
`C:\Program Files` is allowed by the default AppLocker rules.

## Usage

Right-click the tray icon: **Active / Paused**, **Settings …** (also by double-click or by starting the EXE again),
**Show log …**, **Check for updates …**, **Exit**. In the settings window, **OK** applies and closes, **Cancel** discards, **Apply** applies and keeps
the window open. Language: *General → System → Language* ("Automatic" follows the Windows display language).
Log entries are currently always in German.

Settings and log are stored per user in `%AppData%\LSM Display Resizer\`.

## Known limitations

- Windows of programs running **as administrator** can only be adjusted if the app itself runs as administrator.
- Some programs enforce their own minimum/maximum size.
- During the first ~1.5 seconds after a window opens the app does not intervene (many programs restore their own position).
- The layout memory per monitor setup applies to windows that stay open and is lost when the app exits.
- Physical wallpaper layout supports side-by-side monitors; other arrangements are split by pixels.

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet test
pwsh tools/Build-Release.ps1 -Version 1.2.3   # EXE + MSIs into artifacts/
```

Releases: push a tag `vX.Y.Z` – the workflow builds the MSIs and the EXE and publishes the release.

## License

Free and open source under the [MIT License](LICENSE). Copyright © 2026 Sebastian Neumeyer
