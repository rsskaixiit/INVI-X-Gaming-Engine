namespace Invix.Models;

public sealed class BackupEntry
{
    public string TweakId { get; set; } = string.Empty;

    public string Hive { get; set; } = string.Empty;

    public string KeyPath { get; set; } = string.Empty;

    public string ValueName { get; set; } = string.Empty;

    public bool Existed { get; set; }

    public string Kind { get; set; } = "None";

    public string? Data { get; set; }
}

public sealed class EngineBackup
{
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.Now;

    public bool WasAdmin { get; set; }

    public string? PreviousPowerPlanGuid { get; set; }

    public string? PreviousPowerPlanName { get; set; }

    public List<BackupEntry> Entries { get; set; } = new();

    [System.Text.Json.Serialization.JsonIgnore]
    public string? FilePath { get; set; }
}