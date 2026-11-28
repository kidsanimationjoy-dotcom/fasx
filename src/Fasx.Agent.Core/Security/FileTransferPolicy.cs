namespace Fasx.Agent.Core.Security;

/// <summary>
/// Shared constraints for explicitly selected phone-to-PC file transfers.
/// This policy validates names only; it never interprets or executes file contents.
/// </summary>
public static class FileTransferPolicy
{
    public const long MaximumFileSizeBytes = 20L * 1024 * 1024;
    public const int MaximumFileNameLength = 120;

    private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static bool IsSizeAllowed(long size) =>
        size is > 0 and <= MaximumFileSizeBytes;

    public static bool IsSafeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            fileName.Length > MaximumFileNameLength ||
            fileName is "." or ".." ||
            fileName.EndsWith(' ') ||
            fileName.EndsWith('.') ||
            fileName.Any(character =>
                char.IsControl(character) ||
                character is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|'))
        {
            return false;
        }

        var stem = fileName.Split('.')[0].TrimEnd(' ', '.');
        return stem.Length > 0 && !ReservedWindowsNames.Contains(stem);
    }
}
