using System.Runtime.InteropServices;
using Fasx.Agent.Core.Security;

namespace Fasx.Agent.Windows.Input;

/// <summary>
/// Sends only the deliberately small set of input events accepted by RemoteInputPolicy.
/// This class must only be called after the session authorization check.
/// </summary>
internal static class WindowsInputController
{
    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;
    private const uint MouseRightDown = 0x0008;
    private const uint MouseRightUp = 0x0010;
    private const uint MouseWheel = 0x0800;
    private const uint KeyUp = 0x0002;
    private const int ScreenWidthMetric = 0;
    private const int ScreenHeightMetric = 1;

    private static readonly IReadOnlyDictionary<string, byte> VirtualKeys =
        new Dictionary<string, byte>(StringComparer.Ordinal)
        {
            ["ENTER"] = 0x0D,
            ["BACKSPACE"] = 0x08,
            ["TAB"] = 0x09,
            ["ESCAPE"] = 0x1B,
            ["SPACE"] = 0x20,
            ["ARROWUP"] = 0x26,
            ["ARROWDOWN"] = 0x28,
            ["ARROWLEFT"] = 0x25,
            ["ARROWRIGHT"] = 0x27,
            ["DELETE"] = 0x2E,
            ["HOME"] = 0x24,
            ["END"] = 0x23,
            ["PAGEUP"] = 0x21,
            ["PAGEDOWN"] = 0x22
        };

    public static bool TryDispatch(RemoteInputMessage message, out string status)
    {
        if (!RemoteInputPolicy.IsValid(message))
        {
            status = "The remote input event was rejected by the FASX input policy.";
            return false;
        }

        try
        {
            switch (message.Kind)
            {
                case "pointer":
                    if (!MovePointer(message.X, message.Y))
                    {
                        status = "Windows could not position the pointer.";
                        return false;
                    }

                    switch (message.Action)
                    {
                        case "move":
                            break;
                        case "click":
                            mouse_event(MouseLeftDown | MouseLeftUp, 0, 0, 0, UIntPtr.Zero);
                            break;
                        case "rightClick":
                            mouse_event(MouseRightDown | MouseRightUp, 0, 0, 0, UIntPtr.Zero);
                            break;
                        case "scroll":
                            mouse_event(MouseWheel, 0, 0, unchecked((uint)message.Delta), UIntPtr.Zero);
                            break;
                    }

                    status = "Approved pointer input received.";
                    return true;

                case "key":
                    var key = VirtualKeys[message.Key!];
                    keybd_event(key, 0, 0, UIntPtr.Zero);
                    keybd_event(key, 0, KeyUp, UIntPtr.Zero);
                    status = "Approved key input received.";
                    return true;

                default:
                    status = "The remote input event type is not supported.";
                    return false;
            }
        }
        catch (Exception exception)
        {
            status = "Windows rejected the input event: " + exception.Message;
            return false;
        }
    }

    private static bool MovePointer(double x, double y)
    {
        var width = GetSystemMetrics(ScreenWidthMetric);
        var height = GetSystemMetrics(ScreenHeightMetric);
        if (width <= 0 || height <= 0) return false;

        var pixelX = (int)Math.Round(x * (width - 1));
        var pixelY = (int)Math.Round(y * (height - 1));
        return SetCursorPos(pixelX, pixelY);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern void mouse_event(
        uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);

    [DllImport("user32.dll")]
    private static extern void keybd_event(
        byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
}
