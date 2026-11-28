using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Fasx.Agent.Windows.Diagnostics;

[SupportedOSPlatform("windows")]
internal sealed record HealthSnapshot(
    string OperatingSystem,
    string Architecture,
    int LogicalProcessors,
    string TotalMemory,
    string AvailableMemory,
    string SystemDrive,
    string DriveFree,
    string DriveTotal,
    string Uptime,
    string DotNetVersion)
{
    public string ToDisplayText() =>
        $"OPERATING SYSTEM\n{OperatingSystem}\n\n" +
        $"SYSTEM\nArchitecture: {Architecture}\nLogical processors: {LogicalProcessors}\n.NET runtime: {DotNetVersion}\nUptime: {Uptime}\n\n" +
        $"MEMORY\nTotal physical memory: {TotalMemory}\nAvailable physical memory: {AvailableMemory}\n\n" +
        $"SYSTEM DRIVE ({SystemDrive})\nFree space: {DriveFree}\nTotal capacity: {DriveTotal}";
}

[SupportedOSPlatform("windows")]
internal static class WindowsDiagnosticsProvider
{
    public static HealthSnapshot Capture()
    {
        var memory = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        if (!GlobalMemoryStatusEx(ref memory))
        {
            throw new InvalidOperationException(
                "Windows could not provide physical-memory statistics.",
                Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
        }

        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory)
            ?? throw new InvalidOperationException("The Windows system-drive path is unavailable.");
        var drive = new DriveInfo(systemRoot);
        if (!drive.IsReady)
        {
            throw new IOException("The Windows system drive is not ready.");
        }

        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var uptimeText = $"{(int)uptime.TotalDays} days, {uptime.Hours} hours, {uptime.Minutes} minutes";

        return new HealthSnapshot(
            Environment.OSVersion.VersionString,
            RuntimeInformation.OSArchitecture.ToString(),
            Environment.ProcessorCount,
            FormatBytes(memory.TotalPhysical),
            FormatBytes(memory.AvailablePhysical),
            drive.Name,
            FormatBytes((ulong)drive.AvailableFreeSpace),
            FormatBytes((ulong)drive.TotalSize),
            uptimeText,
            Environment.Version.ToString());
    }

    private static string FormatBytes(ulong bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
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

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
