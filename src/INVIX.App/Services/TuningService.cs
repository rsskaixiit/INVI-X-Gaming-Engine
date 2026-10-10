using System.Text;
using Invix.Models;
using Microsoft.Win32;

namespace Invix.Services;

public sealed class TuningService
{
    private readonly PowerPlanService _power = new();
    private readonly BackupService _backup = new();

    public TuningService()
    {
        Descriptors = BuildDefinitions();
    }

    public IReadOnlyList<TweakDefinition> Descriptors { get; }

    public string? LastBackupPath { get; private set; }


    public IReadOnlyList<TweakResult> ApplyGamingMode()
    {
        var results = new List<TweakResult>(Descriptors.Count);
        var backup = new EngineBackup
        {
            CreatedUtc = DateTimeOffset.Now,
            WasAdmin = AdminHelper.IsAdministrator(),
        };

        backup.PreviousPowerPlanGuid = _power.GetActiveGuid();
        if (backup.PreviousPowerPlanGuid is not null)
        {
            backup.PreviousPowerPlanName = PowerPlanService.Describe(backup.PreviousPowerPlanGuid);
        }

        foreach (var tweak in Descriptors)
        {
            results.Add(ApplyTweak(tweak, backup));
        }

        try
        {
            LastBackupPath = _backup.Save(backup);
        }
        catch (Exception ex)
        {
            results.Add(new TweakResult("backup", "Rollback record", TweakStatus.Failed,
                $"Applied values could not be written to disk: {ex.Message}"));
        }

        MouseAcceleration.Broadcast();

        return results;
    }

    private TweakResult ApplyTweak(TweakDefinition tweak, EngineBackup backup)
    {
        if (tweak.GamingPowerPlan is not null)
        {
            var ok = _power.TrySetActive(tweak.GamingPowerPlan, out var message);
            return new TweakResult(tweak.Id, tweak.Name, ok ? TweakStatus.Applied : TweakStatus.Failed, message);
        }

        try
        {
            var written = new List<string>(tweak.GamingWrites.Count);
            foreach (var write in tweak.GamingWrites)
            {
                CaptureBackup(backup, tweak.Id, write);
                WriteValue(write);
                written.Add($"{write.Name}={FormatValue(write.Value)}");
            }

            return new TweakResult(tweak.Id, tweak.Name, TweakStatus.Applied, string.Join(", ", written));
        }
        catch (Exception ex)
        {
            return new TweakResult(tweak.Id, tweak.Name, TweakStatus.Failed, Explain(ex, tweak.RequiresAdmin));
        }
    }


    public IReadOnlyList<TweakResult> RevertFromBackup()
    {
        var backup = _backup.LoadLatest();
        if (backup is null)
        {
            return new[]
            {
                new TweakResult("backup", "Rollback record", TweakStatus.Failed,
                    "No backup found in the INVI X data folder - nothing to revert."),
            };
        }

        LastBackupPath = backup.FilePath;
        var results = new List<TweakResult>();

        if (!string.IsNullOrWhiteSpace(backup.PreviousPowerPlanGuid))
        {
            var ok = _power.TrySetActive(backup.PreviousPowerPlanGuid!, out var message);
            results.Add(new TweakResult("power_plan", "Power Plan (restore previous)",
                ok ? TweakStatus.Reverted : TweakStatus.Failed, message));
        }

        foreach (var entry in backup.Entries)
        {
            try
            {
                RestoreEntry(entry);
                var detail = entry.Existed
                    ? $"Restored {entry.ValueName}={entry.Data} in {entry.KeyPath}."
                    : $"Removed {entry.ValueName} from {entry.KeyPath} (did not exist before).";
                results.Add(new TweakResult(entry.TweakId, $"Registry: {entry.ValueName}", TweakStatus.Reverted, detail));
            }
            catch (Exception ex)
            {
                results.Add(new TweakResult(entry.TweakId, $"Registry: {entry.ValueName}", TweakStatus.Failed,
                    Explain(ex, requiresAdmin: entry.Hive.Equals("HKLM", StringComparison.OrdinalIgnoreCase))));
            }
        }

        MouseAcceleration.Broadcast();

        return results;
    }


    public IReadOnlyList<TweakResult> RestoreDefaults()
    {
        var results = new List<TweakResult>(Descriptors.Count);

        foreach (var tweak in Descriptors)
        {
            if (tweak.DefaultPowerPlan is not null)
            {
                var ok = _power.TrySetActive(tweak.DefaultPowerPlan, out var message);
                results.Add(new TweakResult(tweak.Id, tweak.Name, ok ? TweakStatus.Restored : TweakStatus.Failed, message));
                continue;
            }

            try
            {
                var written = new List<string>(tweak.DefaultWrites.Count);
                foreach (var write in tweak.DefaultWrites)
                {
                    WriteValue(write);
                    written.Add($"{write.Name}={FormatValue(write.Value)}");
                }

                results.Add(new TweakResult(tweak.Id, tweak.Name, TweakStatus.Restored, string.Join(", ", written)));
            }
            catch (Exception ex)
            {
                results.Add(new TweakResult(tweak.Id, tweak.Name, TweakStatus.Failed, Explain(ex, tweak.RequiresAdmin)));
            }
        }

        MouseAcceleration.Broadcast();

        return results;
    }


    private static RegistryKey OpenBase(string hive) =>
        hive.Equals("HKLM", StringComparison.OrdinalIgnoreCase)
            ? RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
            : RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);

    private static void WriteValue(RegistryWrite write)
    {
        using var baseKey = OpenBase(write.Hive);
        using var key = baseKey.CreateSubKey(write.KeyPath, writable: true)
            ?? throw new InvalidOperationException($"Could not open or create '{write.KeyPath}'.");
        key.SetValue(write.Name, write.Value, write.Kind);
    }

    private static void CaptureBackup(EngineBackup backup, string tweakId, RegistryWrite write)
    {
        var entry = new BackupEntry
        {
            TweakId = tweakId,
            Hive = write.Hive,
            KeyPath = write.KeyPath,
            ValueName = write.Name,
        };

        using var baseKey = OpenBase(write.Hive);
        using var key = baseKey.OpenSubKey(write.KeyPath, writable: false);
        var existing = key?.GetValue(write.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);

        if (existing is null || key is null)
        {
            entry.Existed = false;
            entry.Kind = "None";
        }
        else
        {
            entry.Existed = true;
            var kind = key.GetValueKind(write.Name);
            entry.Kind = kind.ToString();
            entry.Data = Encode(kind, existing);
        }

        backup.Entries.Add(entry);
    }

    private static void RestoreEntry(BackupEntry entry)
    {
        using var baseKey = OpenBase(entry.Hive);

        if (!entry.Existed)
        {
            using var existing = baseKey.OpenSubKey(entry.KeyPath, writable: true);
            existing?.DeleteValue(entry.ValueName, throwOnMissingValue: false);
            return;
        }

        using var key = baseKey.CreateSubKey(entry.KeyPath, writable: true)
            ?? throw new InvalidOperationException($"Could not open or create '{entry.KeyPath}'.");
        key.SetValue(entry.ValueName, Decode(entry), Enum.Parse<RegistryValueKind>(entry.Kind));
    }

    private static string Encode(RegistryValueKind kind, object value) => kind switch
    {
        RegistryValueKind.DWord => Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture),
        RegistryValueKind.QWord => Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture),
        RegistryValueKind.Binary => Convert.ToBase64String((byte[])value),
        RegistryValueKind.MultiString => string.Join('\n', (string[])value),
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
    };

    private static object Decode(BackupEntry entry)
    {
        var kind = Enum.Parse<RegistryValueKind>(entry.Kind);
        var data = entry.Data ?? string.Empty;

        return kind switch
        {
            RegistryValueKind.DWord => int.Parse(data, System.Globalization.CultureInfo.InvariantCulture),
            RegistryValueKind.QWord => long.Parse(data, System.Globalization.CultureInfo.InvariantCulture),
            RegistryValueKind.Binary => Convert.FromBase64String(data),
            RegistryValueKind.MultiString => data.Split('\n'),
            _ => data,
        };
    }

    private static string FormatValue(object value) => value switch
    {
        int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
        uint u => u.ToString(System.Globalization.CultureInfo.InvariantCulture),
        long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static string Explain(Exception ex, bool requiresAdmin)
    {
        if (ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
        {
            return requiresAdmin
                ? "Access denied - this step needs an elevated process. Use RESTART AS ADMIN."
                : $"Access denied: {ex.Message}";
        }

        return ex.Message;
    }


    private static IReadOnlyList<TweakDefinition> BuildDefinitions() => new List<TweakDefinition>
    {
        new()
        {
            Id = "power_plan",
            Name = "Power Plan - High Performance",
            Description = "Switches the active Windows power scheme to High performance, which stops "
                        + "the CPU from aggressively down-clocking. Falls back cleanly if the scheme is absent.",
            RequiresAdmin = false,
            GamingPowerPlan = PowerPlanService.HighPerformanceGuid,
            DefaultPowerPlan = PowerPlanService.BalancedGuid,
        },
        new()
        {
            Id = "game_mode",
            Name = "Windows Game Mode",
            Description = "Ensures Game Mode is enabled so Windows prioritises the foreground title.",
            RequiresAdmin = false,
            GamingWrites = new[]
            {
                new RegistryWrite("HKCU", @"Software\Microsoft\GameBar", "AutoGameModeEnabled", RegistryValueKind.DWord, 1),
                new RegistryWrite("HKCU", @"Software\Microsoft\GameBar", "AllowAutoGameMode", RegistryValueKind.DWord, 1),
            },
            DefaultWrites = new[]
            {
                new RegistryWrite("HKCU", @"Software\Microsoft\GameBar", "AutoGameModeEnabled", RegistryValueKind.DWord, 1),
                new RegistryWrite("HKCU", @"Software\Microsoft\GameBar", "AllowAutoGameMode", RegistryValueKind.DWord, 1),
            },
        },
        new()
        {
            Id = "game_dvr",
            Name = "Disable Game DVR / Background Capture",
            Description = "Turns off the background recording overlay, which otherwise costs CPU/GPU "
                        + "time and disk I/O while gaming.",
            RequiresAdmin = false,
            GamingWrites = new[]
            {
                new RegistryWrite("HKCU", @"System\GameConfigStore", "GameDVR_Enabled", RegistryValueKind.DWord, 0),
                new RegistryWrite("HKCU", @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", RegistryValueKind.DWord, 0),
            },
            DefaultWrites = new[]
            {
                new RegistryWrite("HKCU", @"System\GameConfigStore", "GameDVR_Enabled", RegistryValueKind.DWord, 1),
                new RegistryWrite("HKCU", @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", RegistryValueKind.DWord, 1),
            },
        },
        new()
        {
            Id = "mmcss_responsiveness",
            Name = "MMCSS System Responsiveness",
            Description = "Lowers the Multimedia Class Scheduler reservation from 20% to 10% so less CPU "
                        + "is held back from foreground work.",
            RequiresAdmin = true,
            GamingWrites = new[]
            {
                new RegistryWrite("HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                    "SystemResponsiveness", RegistryValueKind.DWord, 10),
            },
            DefaultWrites = new[]
            {
                new RegistryWrite("HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                    "SystemResponsiveness", RegistryValueKind.DWord, 20),
            },
        },
        new()
        {
            Id = "network_throttling",
            Name = "Disable Multimedia Network Throttling",
            Description = "Removes the MMCSS throttling index that caps non-multimedia network packets "
                        + "at ~10 packets per millisecond.",
            RequiresAdmin = true,
            GamingWrites = new[]
            {
                new RegistryWrite("HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                    "NetworkThrottlingIndex", RegistryValueKind.DWord, unchecked((int)0xFFFFFFFF)),
            },
            DefaultWrites = new[]
            {
                new RegistryWrite("HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                    "NetworkThrottlingIndex", RegistryValueKind.DWord, 10),
            },
        },
        new()
        {
            Id = "games_task_priority",
            Name = "MMCSS Games Task Priority",
            Description = "Raises the scheduler priority profile used for the Games task class "
                        + "(CPU priority 6, GPU priority 8, High scheduling category).",
            RequiresAdmin = true,
            GamingWrites = new[]
            {
                new RegistryWrite("HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority", RegistryValueKind.DWord, 8),
                new RegistryWrite("HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Priority", RegistryValueKind.DWord, 6),
                new RegistryWrite("HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Scheduling Category", RegistryValueKind.String, "High"),
                new RegistryWrite("HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "SFIO Priority", RegistryValueKind.String, "High"),
            },
            DefaultWrites = new[]
            {
                new RegistryWrite("HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority", RegistryValueKind.DWord, 8),
                new RegistryWrite("HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Priority", RegistryValueKind.DWord, 2),
                new RegistryWrite("HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Scheduling Category", RegistryValueKind.String, "Medium"),
                new RegistryWrite("HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "SFIO Priority", RegistryValueKind.String, "Normal"),
            },
        },
        new()
        {
            Id = "win32_priority",
            Name = "Win32 Priority Separation",
            Description = "Sets Win32PrioritySeparation to 38 (0x26), the classic 'short, variable' "
                        + "quantum bias toward the foreground process.",
            RequiresAdmin = true,
            GamingWrites = new[]
            {
                new RegistryWrite("HKLM", @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", RegistryValueKind.DWord, 38),
            },
            DefaultWrites = new[]
            {
                new RegistryWrite("HKLM", @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", RegistryValueKind.DWord, 2),
            },
        },
        new()
        {
            Id = "menu_delay",
            Name = "Reduce Menu Show Delay",
            Description = "Removes the 400 ms shell menu hover delay so the desktop feels snappier.",
            RequiresAdmin = false,
            GamingWrites = new[]
            {
                new RegistryWrite("HKCU", @"Control Panel\Desktop", "MenuShowDelay", RegistryValueKind.String, "0"),
            },
            DefaultWrites = new[]
            {
                new RegistryWrite("HKCU", @"Control Panel\Desktop", "MenuShowDelay", RegistryValueKind.String, "400"),
            },
        },
        new()
        {
            Id = "mouse_acceleration",
            Name = "Mouse Acceleration Off (1:1 Aim)",
            Description = "Turns off Windows Enhanced Pointer Precision so pointer movement maps 1:1 "
                        + "to the mouse. Removes the acceleration curve that makes aim feel inconsistent "
                        + "at different swipe speeds. Applied immediately and fully reversible.",
            RequiresAdmin = false,
            GamingWrites = new[]
            {
                new RegistryWrite("HKCU", @"Control Panel\Mouse", "MouseSpeed", RegistryValueKind.String, "0"),
                new RegistryWrite("HKCU", @"Control Panel\Mouse", "MouseThreshold1", RegistryValueKind.String, "0"),
                new RegistryWrite("HKCU", @"Control Panel\Mouse", "MouseThreshold2", RegistryValueKind.String, "0"),
            },
            DefaultWrites = new[]
            {
                new RegistryWrite("HKCU", @"Control Panel\Mouse", "MouseSpeed", RegistryValueKind.String, "1"),
                new RegistryWrite("HKCU", @"Control Panel\Mouse", "MouseThreshold1", RegistryValueKind.String, "6"),
                new RegistryWrite("HKCU", @"Control Panel\Mouse", "MouseThreshold2", RegistryValueKind.String, "10"),
            },
        },
    };

    public string DescribeCatalogue()
    {
        var sb = new StringBuilder();
        foreach (var tweak in Descriptors)
        {
            var scope = tweak.RequiresAdmin ? "admin" : "user";
            sb.AppendLine($"[{scope}] {tweak.Name}");
            sb.AppendLine($"        {tweak.Description}");
        }

        return sb.ToString();
    }
}