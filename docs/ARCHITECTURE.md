# FASX Architecture

## Platform baseline

- Windows companion: C# / .NET 10 LTS / WPF.
- Android controller: Kotlin / Jetpack Compose.
- Control plane: ASP.NET Core on .NET 10.
- Media/data transport: WebRTC candidate, subject to an end-to-end proof of concept.
- Relay: TURN for sessions that cannot establish a direct peer-to-peer path.
- First shared domain library: `Fasx.Agent.Core`.

.NET 10 support policy: https://dotnet.microsoft.com/en-us/platform/support/policy

Android Jetpack Compose documentation: https://developer.android.com/develop/ui/compose

## Logical components

### Windows companion
- Visible desktop application and system-tray presence.
- Device identity stored using Windows-protected storage.
- Local pairing approval.
- Local session consent and permission selection.
- Screen capture initiated through Windows.Graphics.Capture picker.
- Input, clipboard, files, audio, diagnostics, and power actions are separate capability providers.
- Local emergency disconnect.

Microsoft documents that Windows.Graphics.Capture uses a system picker for the user to select a display or app window and shows a visible system capture border: https://learn.microsoft.com/en-us/windows/uwp/audio-video-camera/screen-capture

### Android client
- Device list, SmartDeck, session view, touchpad, keyboard, File Bridge, and Security Shield.
- Device credentials stored with Android Keystore-backed mechanisms.
- Biometric/device authentication may protect local app access, but must not replace host approval for new device pairing.

### Control plane
- Device registration and presence.
- Short-lived pairing challenges.
- Signaling exchange for peer connection establishment.
- Session metadata and revocation.
- No screen frames, keystrokes, clipboard payloads, or transferred file contents should be stored by the signaling service.

### Media and transport
- Evaluate WebRTC for screen media and real-time input data.
- Prefer direct connections where possible; use TURN relay fallback.
- Use WebRTC's standardized secure media/data transports. Do not design custom cryptography.
- The relay should forward encrypted traffic and should not be trusted with screen plaintext.

WebRTC security architecture: https://www.rfc-editor.org/rfc/rfc8827.html

## Connection sequence

1. User opens FASX on the Windows computer and selects Pair device.
2. Windows companion creates a short-lived, single-use challenge.
3. Android scans the QR code and authenticates to the control plane.
4. Windows displays the requesting device identity and requested capabilities.
5. Local user approves or denies pairing.
6. Both sides establish device trust and exchange session signaling.
7. Windows asks the user to select the screen/window using the system capture UI.
8. Local user grants individual session permissions.
9. WebRTC establishes a direct connection or uses TURN relay.
10. Session state and permission changes remain visible; either side can terminate.

## Data boundaries

- Signaling plane: identifiers, challenges, connection offers/answers, ICE candidates, minimal session metadata.
- Media plane: encrypted screen/audio stream and real-time input channel.
- Local agent: capture, input, files, health metrics, clipboard, power actions.
- SmartDeck: UI configuration and typed action identifiers only.
- Audit store: time, device, action category, approval, result; never record typed text or screen content by default.

## RustDesk evaluation

RustDesk remains a candidate reference or integration option, not an assumed dependency. Its repository is published under AGPL-3.0. Before copying, linking, modifying, or distributing any code, complete a license and dependency review. FASX must not quietly incorporate third-party code without preserving required notices and source obligations.

Repository: https://github.com/rustdesk/rustdesk
License: https://github.com/rustdesk/rustdesk/blob/master/LICENCE

## Reliability requirements

- Reconnection must not silently restore permissions after an approval expires.
- A dropped control channel must fail closed for remote input.
- Loss of host visibility or session consent must stop capture and control.
- TURN relay usage must be disclosed in connection details.
- File transfers must be resumable only after re-authentication and permission revalidation.
