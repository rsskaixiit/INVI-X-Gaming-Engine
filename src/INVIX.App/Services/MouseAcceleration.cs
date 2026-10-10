using Invix.Native;
using Microsoft.Win32;

namespace Invix.Services;

public static class MouseAcceleration
{
    private const string KeyPath = @"Control Panel\Mouse";
    private const uint SpiSetMouse = 0x0004;
    private const uint SpifUpdateIniFile = 0x0001;
    private const uint SpifSendChange = 0x0002;

    public static void Broadcast()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
            var threshold1 = ReadInt(key, "MouseThreshold1", 6);
            var threshold2 = ReadInt(key, "MouseThreshold2", 10);
            var acceleration = ReadInt(key, "MouseSpeed", 1);

            var values = new[] { threshold1, threshold2, acceleration };
            NativeMethods.SystemParametersInfo(SpiSetMouse, 0, values, SpifUpdateIniFile | SpifSendChange);
        }
        catch
        {
        }
    }

    private static int ReadInt(RegistryKey? key, string name, int fallback)
    {
        var raw = key?.GetValue(name, null) as string;
        return int.TryParse(raw, out var value) ? value : fallback;
    }
}
