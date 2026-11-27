using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace Fasx.Agent.Windows.Diagnostics;

public sealed record LocalHealthSnapshot(
    string OperatingSystem,
    string LogicalProcessors,
    string Memory,
    string SystemDrive,
    string Battery,
    string Uptime,
    DateTimeOffset CapturedAt);

/// <summary>
/// Reads a small set of local, read-only Windows health metrics.
/// No network activity, screen capture, or file-content inspection is performed.
/// </summary>
public static class LocalHealthService
{
    public static LocalHealthSnapshot Read()
    {
        var memory = ReadMemory();
        var drive = ReadSystemDrive();

        return new LocalHealthSnapshot(
            Environment.OSVersion.VersionString,
            Environment.ProcessorCount.ToString(),
            memory,
            drive,
            ReadBattery(),
            FormatUptime(Environment.TickCount64),
            DateTimeOffset.Now);
    }

    private static string ReadMemory()
    {
        var status = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        if (!GlobalMemoryStatusEx(ref status))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var used = status.TotalPhysical - status.AvailablePhysical;
        return $"{FormatBytes(used)} / {FormatBytes(status.TotalPhysical)} ({status.MemoryLoad}% used)";
    }

    private static string ReadSystemDrive()
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory);
        if (string.IsNullOrWhiteSpace(root))
        {
            return "Unavailable";
        }

        var drive = new DriveInfo(root);
        if (!drive.IsReady)
        {
            return "Not ready";
        }

        return $"{FormatBytes((ulong)drive.AvailableFreeSpace)} free of {FormatBytes((ulong)drive.TotalSize)}";
    }

    private static string ReadBattery()
    {
        if (!GetSystemPowerStatus(out var status))
        {
            return "Unavailable";
        }

        if ((status.BatteryFlag & 0x80) != 0)
        {
            return "No battery detected";
        }

        if (status.BatteryLifePercent == 255)
        {
            return "Level unavailable";
        }

        var charging = (status.BatteryFlag & 0x08) != 0;
        return $"{status.BatteryLifePercent}%{(charging ? " · Charging" : string.Empty)}";
    }

    private static string FormatUptime(long milliseconds)
    {
        var uptime = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
    }

    private static string FormatBytes(ulong bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.0} {units[unit]}";
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
}
