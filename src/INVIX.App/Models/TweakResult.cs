namespace Invix.Models;

public enum TweakStatus
{
    Unknown = 0,

    Applied,

    Reverted,

    Restored,

    Failed
}

public sealed record TweakResult(string Id, string Name, TweakStatus Status, string Detail);