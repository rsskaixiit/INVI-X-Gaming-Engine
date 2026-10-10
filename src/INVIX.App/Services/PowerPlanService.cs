using System.Text.RegularExpressions;

namespace Invix.Services;

public sealed partial class PowerPlanService
{
    public const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
    public const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    public const string UltimatePerformanceGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

    private static readonly Dictionary<string, string> FriendlyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        [BalancedGuid] = "Balanced",
        [HighPerformanceGuid] = "High performance",
        [UltimatePerformanceGuid] = "Ultimate Performance",
    };

    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidPattern();

    public string? GetActiveGuid()
    {
        var result = ProcessRunner.Run("powercfg", "/getactivescheme", timeoutMs: 8000);
        if (!result.Succeeded)
        {
            return null;
        }

        var match = GuidPattern().Match(result.StdOut);
        return match.Success ? match.Value : null;
    }

    public bool TrySetActive(string guid, out string message)
    {
        var result = ProcessRunner.Run("powercfg", $"/setactive {guid}", timeoutMs: 12000);
        if (result.Succeeded)
        {
            message = $"Power scheme set to {Describe(guid)}.";
            return true;
        }

        var reason = string.IsNullOrWhiteSpace(result.StdErr) ? result.StdOut.Trim() : result.StdErr.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = "powercfg returned a non-zero exit code (elevation is usually required).";
        }

        message = $"Could not change power scheme: {reason}";
        return false;
    }

    public static string Describe(string guid) =>
        FriendlyNames.TryGetValue(guid, out var name) ? name : guid;
}