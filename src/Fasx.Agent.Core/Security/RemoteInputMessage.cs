namespace Fasx.Agent.Core.Security;

/// <summary>
/// Small, typed input vocabulary for an explicitly approved remote-control session.
/// It intentionally excludes shell commands, arbitrary key combinations, and system hotkeys.
/// </summary>
public sealed record RemoteInputMessage(
    string Kind,
    string Action,
    double X = 0,
    double Y = 0,
    int Delta = 0,
    string? Key = null);

public static class RemoteInputPolicy
{
    public const int MaximumScrollDelta = 120;

    private static readonly HashSet<string> AllowedKeys = new(StringComparer.Ordinal)
    {
        "ENTER", "BACKSPACE", "TAB", "ESCAPE", "SPACE",
        "ARROWUP", "ARROWDOWN", "ARROWLEFT", "ARROWRIGHT",
        "DELETE", "HOME", "END", "PAGEUP", "PAGEDOWN"
    };

    public static bool IsValid(RemoteInputMessage? message)
    {
        if (message is null || string.IsNullOrWhiteSpace(message.Kind) ||
            string.IsNullOrWhiteSpace(message.Action))
        {
            return false;
        }

        if (message.Kind == "pointer")
        {
            if (!double.IsFinite(message.X) || !double.IsFinite(message.Y) ||
                message.X is < 0 or > 1 || message.Y is < 0 or > 1)
            {
                return false;
            }

            return message.Action switch
            {
                "move" or "click" or "rightClick" => message.Delta == 0 && message.Key is null,
                "scroll" => message.Delta is >= -MaximumScrollDelta and <= MaximumScrollDelta &&
                            message.Delta != 0 && message.Key is null,
                _ => false
            };
        }

        return message.Kind == "key" &&
               message.Action == "press" &&
               message.Delta == 0 &&
               message.X == 0 &&
               message.Y == 0 &&
               message.Key is not null &&
               AllowedKeys.Contains(message.Key);
    }
}
