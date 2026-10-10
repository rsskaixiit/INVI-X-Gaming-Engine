using System.Globalization;
using System.Text.RegularExpressions;
using Invix.Models;
using Microsoft.Win32;

namespace Invix.Services;

public sealed partial class GpuInfoService
{
    private const string DisplayClassKey =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    private static readonly string[] NvidiaSmiCandidates =
    {
        "nvidia-smi",
        @"C:\Windows\System32\nvidia-smi.exe",
        @"C:\Program Files\NVIDIA Corporation\NVSMI\nvidia-smi.exe",
    };

    [GeneratedRegex(@"^\d{4}$")]
    private static partial Regex AdapterSubKeyPattern();

    public GpuInfo Detect()
    {
        var adapters = ReadAdapterNames();
        var primaryName = adapters.Count > 0 ? adapters[0] : "NO COMPATIBLE GPU DETECTED";
        var vram = ReadBestVram();
        var source = adapters.Count > 0 ? "Windows Registry (adapter name / VRAM)" : "Unavailable";

        var nvidia = TryNvidiaSmi();
        if (nvidia is not null)
        {
            return nvidia with
            {
                VramBytes = nvidia.VramBytes ?? vram,
                Adapters = adapters.Count > 0 ? adapters : new[] { nvidia.Name },
            };
        }

        return new GpuInfo(primaryName, vram, null, null, source, adapters);
    }

    private static IReadOnlyList<string> ReadAdapterNames()
    {
        var names = new List<string>();
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var classKey = baseKey.OpenSubKey(DisplayClassKey);
            if (classKey is null)
            {
                return names;
            }

            foreach (var subName in classKey.GetSubKeyNames())
            {
                if (!AdapterSubKeyPattern().IsMatch(subName))
                {
                    continue;
                }

                using var sub = classKey.OpenSubKey(subName);
                if (sub?.GetValue("DriverDesc") is string desc && !string.IsNullOrWhiteSpace(desc))
                {
                    names.Add(desc.Trim());
                }
            }
        }
        catch
        {
        }

        return names;
    }

    private static ulong? ReadBestVram()
    {
        ulong best = 0;
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var classKey = baseKey.OpenSubKey(DisplayClassKey);
            if (classKey is null)
            {
                return null;
            }

            foreach (var subName in classKey.GetSubKeyNames())
            {
                if (!AdapterSubKeyPattern().IsMatch(subName))
                {
                    continue;
                }

                using var sub = classKey.OpenSubKey(subName);
                if (sub?.GetValue("HardwareInformation.qwMemorySize") is byte[] bytes && bytes.Length >= 8)
                {
                    var value = BitConverter.ToUInt64(bytes, 0);
                    if (value > best)
                    {
                        best = value;
                    }
                }
            }
        }
        catch
        {
            return null;
        }

        return best > 0 ? best : null;
    }

    private static GpuInfo? TryNvidiaSmi()
    {
        const string args =
            "--query-gpu=name,utilization.gpu,memory.total,temperature.gpu --format=csv,noheader,nounits";

        foreach (var candidate in NvidiaSmiCandidates)
        {
            var result = ProcessRunner.Run(candidate, args, timeoutMs: 6000);
            if (!result.Succeeded || string.IsNullOrWhiteSpace(result.StdOut))
            {
                continue;
            }

            var line = result.StdOut
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 1)
            {
                continue;
            }

            var name = parts[0];
            var util = parts.Length > 1 ? ParseDouble(parts[1]) : null;
            var vramMb = parts.Length > 2 ? ParseDouble(parts[2]) : null;
            var temp = parts.Length > 3 ? ParseDouble(parts[3]) : null;

            return new GpuInfo(
                name,
                vramMb.HasValue ? (ulong)(vramMb.Value * 1024 * 1024) : null,
                util,
                temp,
                "NVIDIA NVML (nvidia-smi)",
                new[] { name });
        }

        return null;
    }

    private static double? ParseDouble(string text)
    {
        if (string.Equals(text, "N/A", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }
}