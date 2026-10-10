using System.IO;
using Invix.Models;
using Invix.Services;
using Xunit;

namespace Invix.Tests;

public class BackupServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "invix-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Save_then_load_round_trips_every_field()
    {
        var service = new BackupService(_directory);
        var original = new EngineBackup
        {
            WasAdmin = true,
            PreviousPowerPlanGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
            PreviousPowerPlanName = "High performance",
        };
        original.Entries.Add(new BackupEntry
        {
            TweakId = "games_task_priority",
            Hive = "HKLM",
            KeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
            ValueName = "SystemResponsiveness",
            Existed = true,
            Kind = "DWord",
            Data = "20",
        });
        original.Entries.Add(new BackupEntry
        {
            TweakId = "menu_delay",
            Hive = "HKCU",
            KeyPath = @"Control Panel\Desktop",
            ValueName = "MenuShowDelay",
            Existed = false,
            Kind = "None",
        });

        var path = service.Save(original);
        Assert.True(File.Exists(path));

        var loaded = service.LoadLatest();

        Assert.NotNull(loaded);
        Assert.True(loaded!.WasAdmin);
        Assert.Equal(original.PreviousPowerPlanGuid, loaded.PreviousPowerPlanGuid);
        Assert.Equal(2, loaded.Entries.Count);
        Assert.Equal("20", loaded.Entries[0].Data);
        Assert.False(loaded.Entries[1].Existed);
        Assert.Equal("None", loaded.Entries[1].Kind);
    }

    [Fact]
    public void LoadLatest_returns_null_when_nothing_was_ever_saved()
    {
        var service = new BackupService(_directory);
        Assert.Null(service.LoadLatest());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch
        {
        }
    }
}