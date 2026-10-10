namespace Invix.Models;

public sealed record GpuInfo(
    string Name,
    ulong? VramBytes,
    double? UtilizationPercent,
    double? TemperatureC,
    string Source,
    IReadOnlyList<string> Adapters)
{
    public static GpuInfo Unknown { get; } = new(
        "NO COMPATIBLE GPU DETECTED",
        null,
        null,
        null,
        "Unavailable",
        Array.Empty<string>());

    public bool HasLiveMetrics => UtilizationPercent.HasValue || TemperatureC.HasValue;
}

public readonly record struct MemorySample(ulong TotalBytes, ulong UsedBytes, uint LoadPercent);