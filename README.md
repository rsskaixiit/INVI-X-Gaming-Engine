# INVI X - Ultra Low Latency Gaming Engine

A real Windows desktop application (C# / .NET 8 / WPF) that renders itself live - there is
no image, sprite or static mock-up anywhere in this project. Every pixel on screen is
code: the magenta pixel wordmark, the frame, the status lamp, the live telemetry rows and
the operations log are all drawn at runtime.

```
███ █   █ █   █ ███  █   █
 █  ██  █ █   █  █    █ █ 
 █  █ █ █ █   █  █     █  
 █  █  ██  █ █   █    █ █ 
███ █   █   █   ███  █   █
```

---

## Panel

A compact `620 x 400` borderless terminal panel (`WindowChrome`, native resize border, no
Windows caption). Draggable by its title strip; minimised and closed from the same strip.

| Region | Contents |
| --- | --- |
| Header | `INVI X` title strip, magenta pixel wordmark, cyan subtitle, magenta rule |
| Status | `STATUS : ● ACTIVE` - real tracked state, plus a step tally |
| System | three rows only - `CPU`, `RAM`, `GPU` (model + supported live metrics) |
| Controls | `F6 TOGGLE GAMING MODE`, `ESC EXIT PANEL`, and `APPLY` / `REVERT` / `RESTORE DEFAULTS` |
| Log | operations log, **collapsed by default**; `SHOW LOG` expands it and grows the panel |
| Help / Support | **always visible** strip: `DEV</> INVI-XD !!` plus `YOUTUBE` / `DISCORD` links |
| Footer | `INVI X \| PRECISION MODE \| GAMING EDITION` |

Palette: background `#080808`, magenta `#FF00C8`, cyan `#20B8FF`, active green `#00FF41`,
muted gray `#777777`. Monospace throughout (`Consolas`), ClearType text, no gradients.

### Help / Support

A compact **HELP / SUPPORT** strip sits just above the footer and is always visible - no
toggle, nothing hidden. It shows the author credit and two buttons that open in your
default browser:

| Button | Link |
| --- | --- |
| `YOUTUBE @invi-xd` | https://www.youtube.com/@invi-xd |
| `DISCORD T5HzhQTnxe` | https://discord.gg/T5HzhQTnxe (INVI-XD server) |

The backup folder (`%LOCALAPPDATA%\INVI X\backups`) is printed in the operations log on
startup and after every apply. Only the log is collapsible.

---

## What it actually does

| Area | Implementation |
| --- | --- |
| CPU usage | `GetSystemTimes` (kernel32) deltas - real, no simulation |
| RAM usage | `GlobalMemoryStatusEx` (kernel32) - real physical memory |
| GPU | `nvidia-smi` (NVML) for live utilisation / temperature / VRAM; falls back to the Windows display-adapter registry class for name + VRAM |
| Power plan | `powercfg` by GUID (language independent) |
| Gaming optimisations | Documented, reversible registry + power-plan steps |
| Backups | Written to `%LOCALAPPDATA%\INVI X\backups\backup-*.json` **before** any change |
| Keyboard | `F6` toggles the gaming profile, `ESC` exits the panel |

### Keyboard

| Key | Action |
| --- | --- |
| `F6` | Toggle gaming mode (apply / revert) |
| `ESC` | Exit the panel |

### Buttons

* **APPLY** - writes the gaming profile (backup first).
* **REVERT** - restores every value from the most recent backup.
* **RESTORE DEFAULTS** - writes documented Windows defaults.
* **RUN AS ADMIN** - relaunches elevated so the HKLM steps can run. On an already-elevated
  panel (e.g. UAC disabled) it reads `ELEVATED` and is intentionally disabled.

---

## Tuning catalogue

Each step is defined declaratively in `TuningService` and shown in the panel with a live
status marker (`[ OK ]`, `[REV ]`, `[DEF ]`, `[FAIL]`, `[ -- ]`).

| Step | Scope | Gaming value | Windows default |
| --- | --- | --- | --- |
| Power Plan - High Performance | user | High performance scheme | Balanced scheme |
| Windows Game Mode | user | `AutoGameModeEnabled=1`, `AllowAutoGameMode=1` | same (default on) |
| Disable Game DVR / Background Capture | user | `GameDVR_Enabled=0`, `AppCaptureEnabled=0` | `1`, `1` |
| MMCSS System Responsiveness | admin | `SystemResponsiveness=10` | `20` |
| Disable Multimedia Network Throttling | admin | `NetworkThrottlingIndex=0xFFFFFFFF` | `10` |
| MMCSS Games Task Priority | admin | priority 6, GPU 8, `High`, `High` | priority 2, GPU 8, `Medium`, `Normal` |
| Win32 Priority Separation | admin | `Win32PrioritySeparation=38` | `2` |
| Reduce Menu Show Delay | user | `MenuShowDelay="0"` | `"400"` |
| Mouse Acceleration Off (1:1 Aim) | user | `MouseSpeed="0"`, `MouseThreshold1="0"`, `MouseThreshold2="0"` | `"1"`, `"6"`, `"10"` |

**Nothing else is touched.** No services are disabled, no files are deleted, no system
settings outside this list are modified.

---

## Honesty note

INVI X reports **measured state only**. It does not promise zero latency and does not
guarantee any frame-rate gain. The real effect of each step depends on your hardware,
drivers and workload, and several of the steps are simply regularising settings that many
gamers prefer. If a step cannot be read on your machine it is reported as unavailable
rather than invented.

---

## Prerequisites

* **Windows 10 or 11.**
* **.NET 8 Desktop Runtime** (`Microsoft.WindowsDesktop.App` 8.x) - needed only for the
  framework-dependent development build. The published `INVIX.exe` is self-contained.
* To **build**: a .NET SDK that supports `net8.0-windows` (**.NET 8 SDK or newer** -
  newer SDKs such as 10.x build this solution fine).
* Optional: **Visual Studio 2022** with the *.NET desktop development* workload
  (open `INVIX.sln`).

---

## Build

```powershell
cd <project-folder>
dotnet restore .\INVIX.sln
dotnet build   .\INVIX.sln -c Release
```

Or with the helper script:

```powershell
.\build.ps1 -Configuration Release
```

### Hardened + obfuscated release

```powershell
.\publish.ps1 -Runtime win-x64
```

Produces a single self-contained `publish\win-x64\INVIX.exe` that is compressed, ships no
DLLs/PDB, **and is aggressively obfuscated + self-integrity-checked** (see *Protection*
below). Use `-NoObfuscate` to skip obfuscation, or `-Runtime win-arm64` for Windows-on-ARM.

## Protection

The release exe is processed with **[Obfuscar](https://www.obfuscar.com)** during `publish.ps1`:

* **Aggressive renaming** - public API is renamed too (`KeepPublicApi=false`); every method,
  property, field, event and type outside the WPF/JSON surface is renamed to short/Unicode
  names, so the original names are gone from the metadata.
* **String hiding** - every string literal (registry paths, tweak values, command lines, URLs)
  is encrypted in the binary and decrypted only at run time, so they no longer show up as
  readable text in the EXE or the extracted IL.
* **WPF + JSON surface kept** - `Invix.App`, `Invix.MainWindow`, and the `Invix.ViewModels` /
  `Invix.Models` namespaces are skipped so XAML bindings and the JSON backup format keep
  working; WPF BAML resources (`INVIX.g.resources`) are left untouched.
* **Anti-debug** - the hardened build exits immediately if a debugger is attached (managed,
  native or remote).
* **Self-integrity check** - `publish.ps1` hashes the obfuscated assembly and embeds a SHA-256
  fingerprint into it (via `tools\patch-integrity.ps1`); at startup the hardened exe re-hashes
  its own assembly and **refuses to run if it was modified**.
* The hardened build is published with `IncludeAllContentForSelfExtract` and without
  ReadyToRun so the integrity check can read the exact assembly it shipped with.

> **Honest limit:** no .NET executable can be made truly "uncrackable". This build combines
> aggressive obfuscation, string hiding, anti-debug and an anti-tamper integrity check, which
> defeats casual reverse-engineering, reading, copying and naive patching - but a determined,
> skilled attacker can always invest time. It is **not** a licence or DRM - there is none to
> crack, the app is free.

The obfuscator is installed automatically on first run of `publish.ps1` if it is missing.

## Test

```powershell
dotnet test .\INVIX.sln -c Debug
```

## Run

```powershell
.\run.ps1 -Configuration Release
```

or directly:

```powershell
.\src\INVIX.App\bin\Release\net8.0-windows\INVIX.exe
```

To run elevated (enables the HKLM steps) either press **RUN AS ADMIN** in the panel or:

```powershell
.\run.ps1 -Elevate
```

---

## Project layout

```
INVIX.sln
build.ps1 / run.ps1 / publish.ps1
src/INVIX.App/
  INVIX.App.csproj
  app.manifest                     asInvoker + PerMonitorV2 DPI
  App.xaml / App.xaml.cs           theme palette, terminal button style, entry point
  MainWindow.xaml / .xaml.cs       panel layout, F6/ESC shortcuts, log auto-scroll
  Assets/AsciiArt.cs               code-drawn wordmark + text bars (no images)
  Native/NativeMethods.cs          GetSystemTimes / GlobalMemoryStatusEx / SystemParametersInfo P/Invoke
  Models/                          GPU, tuning and backup data types
  Services/
    SystemMetricsService.cs        real CPU + RAM sampling
    GpuInfoService.cs              nvidia-smi + registry GPU detection
    PowerPlanService.cs            powercfg read/switch by GUID
    BackupService.cs               JSON rollback records
    TuningService.cs               catalogue, apply / revert / restore defaults
    MouseAcceleration.cs           applies the pointer-acceleration change immediately
    IntegrityGuard.cs              anti-debug + self-integrity check (hardened build)
    ProcessRunner.cs, AdminHelper.cs
  ViewModels/                      MainViewModel, RelayCommand, ObservableObject
tools/patch-integrity.ps1          embeds the SHA-256 fingerprint into the obfuscated assembly
tests/INVIX.Tests/
  AsciiArtTests.cs                 logo geometry + bar rendering
  BackupServiceTests.cs            backup round-trip in a temp folder
  TuningCatalogueTests.cs          catalogue invariants (no registry access)
```

---

## Troubleshooting

* **`RUN AS ADMIN` is greyed out / does nothing** - expected when the panel is
  already elevated (the caption then reads `ELEVATED`). Many PCs, including
  ones with **UAC disabled** (`EnableLUA = 0`), always run elevated, so there is nothing
  to elevate. Check the `ADMIN:` label in the title strip.
* **HKLM steps show `[FAIL] Access denied`** - relaunch with **RUN AS ADMIN**, or run
  `.\run.ps1 -Elevate`.
* **GPU shows "live utilisation: unavailable"** - only NVIDIA cards expose metrics through
  `nvidia-smi` in this build; other vendors still show adapter name and VRAM from the
  registry.
* **`INVIX.exe` is locked during build** - close any running INVI X window first
  (`Get-Process INVIX | Stop-Process`).

---

INVI X | PRECISION MODE | GAMING EDITION
