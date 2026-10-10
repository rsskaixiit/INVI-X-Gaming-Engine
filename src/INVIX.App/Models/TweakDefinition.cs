using Microsoft.Win32;

namespace Invix.Models;

public sealed record RegistryWrite(string Hive, string KeyPath, string Name, RegistryValueKind Kind, object Value);

public sealed class TweakDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public bool RequiresAdmin { get; init; }

    public IReadOnlyList<RegistryWrite> GamingWrites { get; init; } = Array.Empty<RegistryWrite>();

    public IReadOnlyList<RegistryWrite> DefaultWrites { get; init; } = Array.Empty<RegistryWrite>();

    public string? GamingPowerPlan { get; init; }

    public string? DefaultPowerPlan { get; init; }
}