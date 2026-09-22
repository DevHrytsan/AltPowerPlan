# AltPowerPlan

<p align="center">
  <img src="https://raw.githubusercontent.com/DevHrytsan/AltPowerPlan/main/.promo/altpowerplan01_pic.png" alt="AltPowerPlan" width="100%">
</p>

[![CI](https://github.com/DevHrytsan/AltPowerPlan/actions/workflows/ci.yml/badge.svg)](https://github.com/DevHrytsan/AltPowerPlan/actions/workflows/ci.yml)
[![GitHub Release](https://img.shields.io/github/v/release/DevHrytsan/AltPowerPlan?include_prereleases&logo=github)](https://github.com/DevHrytsan/AltPowerPlan/releases)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)
[![Platform: Windows 10 / 11](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?logo=windows)](https://microsoft.com/windows)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com)

AltPowerPlan is a small Windows utility for switching between power plans. It's built with .NET 10 and WPF, talks to the native Win32 power APIs directly, and runs on both x64 and ARM64 Windows 10/11 machines.

## What it does

**A popup menu you can actually reach**

Hit `Ctrl + Alt + P` (configurable) and a small popup menu appears with your power plans.

**Keyboard navigation**

Use `Up`/`Down` to move through the list, `Enter` or `Space` to apply a plan, and `Escape` to back out without changing anything. No mouse required.

**Switches plans automatically on AC/battery**

Tell it which plan you want on wall power and which one you want on battery. It listens for the OS power-status notifications, so the switch happens the moment you unplug or plug back in.

**Full plan management**

Create, duplicate, rename, export, import, and delete power plans from inside the app.

## Downloads for users

If you don't care about the code. And just want to download this application and use it. 

Grab the latest build from the [Releases](https://github.com/DevHrytsan/AltPowerPlan/releases/latest) page. Pick the package that matches your machine:

| File | Architecture | Type | Notes |
| :--- | :---: | :---: | :--- |
| `AltPowerPlan-vX.X.X-Setup.exe` | x64 | Installer | Standard installer. Needs the .NET 10 Desktop Runtime — it'll offer to install it if it's missing. |
| `AltPowerPlan-vX.X.X-Setup-Standalone.exe` | x64 | Installer | Self-contained installer with the runtime baked in. Nothing else to install. |
| `AltPowerPlan-vX.X.X-win-arm64-Setup-Standalone.exe` | ARM64 | Installer | Self-contained installer built natively for Windows on ARM64. |
| `AltPowerPlan-vX.X.X-win-x64-standalone.zip` | x64 | Portable | Self-contained single-file executable, zipped. |
| `AltPowerPlan-vX.X.X-win-arm64-standalone.zip` | ARM64 | Portable | Same thing, native ARM64 build. |
| `AltPowerPlan-vX.X.X-win-x64-portable.zip` | x64 | Portable | Lightweight, framework-dependent, with a portable config file. |
| `AltPowerPlan-vX.X.X-win-arm64-portable.zip` | ARM64 | Portable | Lightweight ARM64 build with portable config. |

Every release ships with `SHA256SUMS.txt` so you can verify what you downloaded.

## Keyboard shortcuts

| Shortcut | Action |
| :--- | :--- |
| `Ctrl + Alt + P` | Open/close the quick switch popup menu |
| `Up` / `Down` | Move through the plan list |
| `Enter` / `Space` | Apply the highlighted plan and close the popup menu |
| `Escape` | Close the popup menu without applying |
| Double-click tray icon | Open the main window |
| Right-click tray icon | Open the tray menu |

The global shortcut is configurable in Application Settings. You can also set it to `Win + Alt + P`, `Alt + Shift + P`, `Ctrl + Shift + P`, or turn it off entirely.

## Requirements

- Windows 10 (1809 or newer) or Windows 11, 64-bit
- x64 (AMD/Intel) or ARM64 (Snapdragon, Surface Pro X, etc.)
- Runtime: nothing extra if you grab a standalone package. Framework-dependent builds need the [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

## Building from source

You'll need:

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (10.0.100 or newer)
- Visual Studio 2022, Rider, or VS Code with the .NET workload
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) (if you want to build the setup executables)

### Using the build pipeline

The repo uses [ModularPipelines](https://github.com/thomhurst/ModularPipelines) to handle building, testing, and packaging:

```powershell
# Full pipeline: build, test, package
dotnet run --project AltPowerPlan.Pipeline -- --clean

# Skip parts you don't need
dotnet run --project AltPowerPlan.Pipeline -- --skip-tests --skip-inno
```
## Special Thanks

A huge thanks to my best friend for designing the AltPowerPlan app icon.