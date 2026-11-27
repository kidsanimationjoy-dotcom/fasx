namespace Fasx.Agent.Core.Security;

/// <summary>
/// Capabilities that may be granted independently for a remote session.
/// Keep this list explicit: never replace it with an unrestricted "remote control" flag.
/// </summary>
[Flags]
public enum SessionPermission
{
    None = 0,
    ViewScreen = 1 << 0,
    ControlInput = 1 << 1,
    TransferFiles = 1 << 2,
    ShareClipboard = 1 << 3,
    ReadDiagnostics = 1 << 4,
    PlayRemoteAudio = 1 << 5,
    RunPowerActions = 1 << 6,
    ControlPeripherals = 1 << 7
}
