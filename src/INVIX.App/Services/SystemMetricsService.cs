using System.Runtime.InteropServices;
using Invix.Models;
using Invix.Native;

namespace Invix.Services;

public sealed class SystemMetricsService
{
    private ulong _previousIdle;
    private ulong _previousKernel;
    private ulong _previousUser;
    private bool _hasPreviousSample;

    public double SampleCpuPercent()
    {
        if (!NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return double.NaN;
        }

        var idleTicks = idle.ToUInt64();
        var kernelTicks = kernel.ToUInt64();
        var userTicks = user.ToUInt64();

        if (!_hasPreviousSample)
        {
            _previousIdle = idleTicks;
            _previousKernel = kernelTicks;
            _previousUser = userTicks;
            _hasPreviousSample = true;
            return double.NaN;
        }

        var deltaIdle = idleTicks - _previousIdle;
        var deltaKernel = kernelTicks - _previousKernel;
        var deltaUser = userTicks - _previousUser;
        var deltaTotal = deltaKernel + deltaUser;

        _previousIdle = idleTicks;
        _previousKernel = kernelTicks;
        _previousUser = userTicks;

        if (deltaTotal == 0)
        {
            return 0.0;
        }

        var busy = deltaTotal - deltaIdle;
        return Math.Clamp(busy * 100.0 / deltaTotal, 0.0, 100.0);
    }

    public MemorySample SampleMemory()
    {
        var status = new NativeMethods.MEMORYSTATUSEX
        {
            dwLength = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>(),
        };

        if (!NativeMethods.GlobalMemoryStatusEx(ref status))
        {
            return new MemorySample(0, 0, 0);
        }

        var used = status.ullTotalPhys - status.ullAvailPhys;
        return new MemorySample(status.ullTotalPhys, used, status.dwMemoryLoad);
    }

    public static string FormatBytes(ulong bytes)
    {
        const double kb = 1024.0;
        const double mb = kb * 1024.0;
        const double gb = mb * 1024.0;

        return bytes switch
        {
            >= (ulong)gb => $"{bytes / gb:0.0} GB",
            >= (ulong)mb => $"{bytes / mb:0.0} MB",
            >= (ulong)kb => $"{bytes / kb:0.0} KB",
            _ => $"{bytes} B",
        };
    }
}