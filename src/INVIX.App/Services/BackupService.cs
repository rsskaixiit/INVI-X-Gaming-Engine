using System.IO;
using System.Text.Json;
using Invix.Models;

namespace Invix.Services;

public sealed class BackupService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public BackupService(string? backupDirectory = null)
    {
        BackupDirectory = backupDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "INVI X",
            "backups");
    }

    public string BackupDirectory { get; }

    public string Save(EngineBackup backup)
    {
        Directory.CreateDirectory(BackupDirectory);
        var fileName = $"backup-{DateTime.Now:yyyyMMdd-HHmmss}.json";
        var path = Path.Combine(BackupDirectory, fileName);

        var suffix = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(BackupDirectory, $"backup-{DateTime.Now:yyyyMMdd-HHmmss}-{suffix++}.json");
        }

        File.WriteAllText(path, JsonSerializer.Serialize(backup, SerializerOptions));
        backup.FilePath = path;
        return path;
    }

    public EngineBackup? LoadLatest()
    {
        try
        {
            if (!Directory.Exists(BackupDirectory))
            {
                return null;
            }

            var newest = new DirectoryInfo(BackupDirectory)
                .GetFiles("backup-*.json")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();

            if (newest is null)
            {
                return null;
            }

            var backup = JsonSerializer.Deserialize<EngineBackup>(File.ReadAllText(newest.FullName), SerializerOptions);
            if (backup is not null)
            {
                backup.FilePath = newest.FullName;
            }

            return backup;
        }
        catch
        {
            return null;
        }
    }
}