using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Invix.Services;

internal static class IntegrityGuard
{
    private const string AssemblyFileName = "INVIX.dll";

    private static readonly byte[] ExpectedHash =
    {
        0x44, 0x45, 0x41, 0x44, 0x42, 0x45, 0x45, 0x46,
        0x44, 0x45, 0x41, 0x44, 0x42, 0x45, 0x45, 0x46,
        0x44, 0x45, 0x41, 0x44, 0x42, 0x45, 0x45, 0x46,
        0x44, 0x45, 0x41, 0x44, 0x42, 0x45, 0x45, 0x46,
        0x44, 0x45, 0x41, 0x44, 0x42, 0x45, 0x45, 0x46,
        0x44, 0x45, 0x41, 0x44, 0x42, 0x45, 0x45, 0x46,
        0x44, 0x45, 0x41, 0x44, 0x42, 0x45, 0x45, 0x46,
        0x44, 0x45, 0x41, 0x44, 0x42, 0x45, 0x45, 0x46,
    };

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsDebuggerPresent();

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CheckRemoteDebuggerPresent(IntPtr process, [MarshalAs(UnmanagedType.Bool)] ref bool debuggerPresent);

    internal static bool IsCompromised()
    {
        if (Debugger.IsAttached)
        {
            return true;
        }

        if (IsDebuggerPresent())
        {
            return true;
        }

        var present = false;
        try
        {
            CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref present);
        }
        catch
        {
        }

        return present;
    }

    internal static bool VerifySelf()
    {
        try
        {
            var path = LocateAssembly();
            if (path is null)
            {
                return false;
            }

            var bytes = File.ReadAllBytes(path);
            var offset = IndexOf(bytes, ExpectedHash);
            if (offset < 0)
            {
                return false;
            }

            for (var i = 0; i < ExpectedHash.Length; i++)
            {
                bytes[offset + i] = 0;
            }

            var digest = SHA256.HashData(bytes);
            var expected = DecodeHex(ExpectedHash);
            return expected is not null && CryptographicOperations.FixedTimeEquals(digest, expected);
        }
        catch
        {
            return false;
        }
    }

    private static string? LocateAssembly()
    {
        foreach (var candidate in Candidates())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> Candidates()
    {
        yield return Path.Combine(AppContext.BaseDirectory, AssemblyFileName);

        var processDir = Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty);
        if (!string.IsNullOrEmpty(processDir))
        {
            yield return Path.Combine(processDir, AssemblyFileName);
        }

        var location = typeof(IntegrityGuard).Assembly.Location;
        var locationDir = string.IsNullOrEmpty(location) ? null : Path.GetDirectoryName(location);
        if (!string.IsNullOrEmpty(locationDir))
        {
            yield return Path.Combine(locationDir, AssemblyFileName);
        }
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        var limit = haystack.Length - needle.Length;
        for (var i = 0; i <= limit; i++)
        {
            if (haystack[i] != needle[0])
            {
                continue;
            }

            var match = true;
            for (var j = 1; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return i;
            }
        }

        return -1;
    }

    private static byte[]? DecodeHex(byte[] ascii)
    {
        if (ascii.Length % 2 != 0)
        {
            return null;
        }

        var result = new byte[ascii.Length / 2];
        for (var i = 0; i < result.Length; i++)
        {
            var high = HexValue(ascii[i * 2]);
            var low = HexValue(ascii[(i * 2) + 1]);
            if (high < 0 || low < 0)
            {
                return null;
            }

            result[i] = (byte)((high << 4) | low);
        }

        return result;
    }

    private static int HexValue(byte value)
    {
        if (value >= 0x30 && value <= 0x39)
        {
            return value - 0x30;
        }

        if (value >= 0x61 && value <= 0x66)
        {
            return value - 0x61 + 10;
        }

        if (value >= 0x41 && value <= 0x46)
        {
            return value - 0x41 + 10;
        }

        return -1;
    }
}
