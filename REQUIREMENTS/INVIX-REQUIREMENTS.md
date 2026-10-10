# INVI X — Requirements & How To Run

INVI X — Ultra Low Latency Gaming Engine (Precision Mode / Gaming Edition)
A Windows desktop panel (.NET 8 WPF) that shows real CPU / RAM / GPU telemetry and applies
safe, fully reversible Windows gaming tweaks.

---

## 1. Which EXE do I open?

**Recommended (hardened, self-contained):**

```
C:\Users\Samrat Prince !!\Desktop\Creations\publish\win-x64\INVIX.exe
```

This is a single ~68 MB file with the .NET runtime bundled in — it needs **no** .NET install,
ships **no** DLLs and **no** PDB, and is **aggressively obfuscated + self-integrity-checked**
(string literals hidden, public/private members renamed, SHA-256 fingerprint embedded, exits
if the assembly is modified or a debugger is attached). Build it with:

```powershell
.\publish.ps1 -Runtime win-x64
```

**Normal development build:**

```
C:\Users\Samrat Prince !!\Desktop\Creations\src\INVIX.App\bin\Release\net8.0-windows\INVIX.exe
```

> Use a **Release** build. If that folder does not exist yet, build first (see section 4).

You can also launch it with the helper script from the project root:

```powershell
.\run.ps1 -Configuration Release            # normal launch
.\run.ps1 -Configuration Release -Elevate   # launch as administrator (UAC prompt)
```

---

## 2. Requirements

### Operating system
- Windows 10 (version 1809 or newer) or Windows 11.
- Windows on x64. WPF desktop session (not Server Core).

### Runtime (to RUN the exe)
- **.NET Desktop Runtime 8.0** (x64) — download: https://dotnet.microsoft.com/download/dotnet/8.0
  - Pick: **Desktop Runtime** (not the ASP.NET one).
  - Not required if you build from source with the SDK, but still needed to run the app later
    unless the runtime is already installed.
- The **normal development build** is framework-dependent (needs the runtime above).
- The **recommended** `publish\win-x64\INVIX.exe` is **self-contained** (runtime bundled in)
  and needs **nothing** installed.

### SDK (only to BUILD from source)
- **.NET SDK 8.0 or newer** (the project also builds with a newer SDK such as 10.x).
- .NET 8 targeting pack (installed with the SDK).

### Hardware
- Any modern CPU.
- 4 GB RAM minimum (8 GB+ recommended).
- GPU: optional. Works with any GPU.
  - **NVIDIA GPU + `nvidia-smi`** (comes with the NVIDIA driver) enables live GPU utilisation
    and temperature. Without it, the GPU row still shows the adapter name and VRAM, but live
    metrics show as unavailable — no fake numbers are ever shown.

### Permissions
- The app itself runs fine as a standard user (telemetry + Apply/Revert of user-level steps).
- **Administrator rights are required** for the HKLM steps (MMCSS `SystemResponsiveness`,
  `NetworkThrottlingIndex`, `Win32PrioritySeparation`, and the Games task). Without admin,
  those specific steps are skipped and reported as failed in the log — nothing is faked.
- This PC has UAC disabled, so the app already runs elevated and the button shows `ELEVATED`.

### Displays
- Minimum window size 620 × 400 (default). Resizable. Runs on 100%–200% DPI scaling.

### Protection
- The release build is processed with **Obfuscar** during `publish.ps1`: string literals are
  hidden and members/types are aggressively renamed (`KeepPublicApi=false`), so registry paths,
  tweak values and code structure are no longer readable in the binary. The WPF/JSON surface
  (`Invix.App`, `Invix.MainWindow`, `Invix.ViewModels`, `Invix.Models`) is skipped so bindings
  and the backup format keep working.
- The hardened build is **anti-tamper**: `publish.ps1` embeds a SHA-256 fingerprint of the
  obfuscated assembly and the app re-checks it at startup, refusing to run if the file was
  modified. It also **exits if a debugger is attached**.
- Honest limit: this strongly discourages reading/copying/patching the code, but **no .NET exe
  is truly uncrackable**. There is no licence or DRM in INVI X — it is free.

---

## 3. Using the panel

| Control | What it does |
| --- | --- |
| `F6` | Toggle Gaming Mode (apply / revert) |
| `ESC` | Exit the panel |
| `APPLY` | Apply the INVI X gaming profile (writes a backup first) |
| `REVERT` | Restore the exact configuration captured in the last backup |
| `RESTORE DEFAULTS` | Write documented Windows defaults for every step |
| `SHOW LOG` | Expand / collapse the operations log (collapsed by default) |
| `YOUTUBE` / `DISCORD` | Open the support links in your default browser |

Status shows `ACTIVE` only after a real successful apply, otherwise `INACTIVE`.

---

## 4. Build from source (optional)

From the project root:

```powershell
.\build.ps1 -Configuration Release
# or
dotnet build "INVIX.sln" -c Release
```

Run the tests:

```powershell
dotnet test "INVIX.sln" -c Release
```

Output exe: `src\INVIX.App\bin\Release\net8.0-windows\INVIX.exe`

---

## 5. What it changes (and how to undo)

Each apply writes a rollback record **before** any change, so everything is reversible.

- Power plan → High performance
- Game Mode → On
- Game DVR → Off
- MMCSS `SystemResponsiveness` → 10
- `NetworkThrottlingIndex` → 0xFFFFFFFF
- Games task priorities (Priority 6, GPU 8, High/High)
- `Win32PrioritySeparation` → 38
- `MenuShowDelay` → 0
- **Mouse acceleration off** (`MouseSpeed` / `MouseThreshold1` / `MouseThreshold2` → 0) — Enhanced
  Pointer Precision disabled for 1:1 aim, applied immediately via `SystemParametersInfo`

Rollback records are stored at:

```
%LOCALAPPDATA%\INVI X\backups\backup-*.json
```

Use `REVERT` (from the last backup) or `RESTORE DEFAULTS` (documented Windows defaults) to undo.
No services are disabled, no system files are modified, and nothing is deleted.

---

## 6. Troubleshooting

| Symptom | Fix |
| --- | --- |
| "You must install .NET Desktop Runtime" on launch | Install .NET Desktop Runtime 8.0 (x64) from the link above |
| Windows SmartScreen warning | Click `More info` → `Run anyway` (unsigned local build) |
| Some steps show `[FAIL]` in the log | Launch as administrator (needed for HKLM steps) |
| GPU utilisation/temp not shown | No NVIDIA driver / `nvidia-smi` not found; adapter + VRAM still shown |
| Window looks clipped after resizing | Resize back to at least 620 × 400 (minimum size) |
| Nothing happens on Apply | Check the operations log — every step result is listed |

---

## 7. Support

- YouTube: https://www.youtube.com/@invi-xd
- Discord (INVI-XD): https://discord.gg/T5HzhQTnxe

Dev</> INVI-XD !!
