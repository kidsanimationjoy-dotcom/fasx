# FASX Architecture

## Platform baseline

- Windows companion: C# / .NET 10 LTS / WPF.
- Android controller: Kotlin / Jetpack Compose.
- Control plane: ASP.NET Core on .NET 10 for future internet signaling.
- Local same-network pairing: Kestrel HTTPS listener bound to one selected RFC1918 IPv4 interface.
- Media/data transport: evaluate WebRTC for future screen/audio/data sessions.
- Relay: TURN for sessions that cannot establish a direct peer-to-peer path.
- Shared domain rules: `Fasx.Agent.Core`.

## Windows companion

- Visible WPF window and user-started pairing window.
- Device identity records protected using Windows DPAPI for the current user.
- Pairing challenge expires and is single-use.
- Local approval, denial, and cancellation are UI-only operations.
- HTTPS certificate is generated for a temporary pairing window and its SPKI pin is included in the QR invitation.
- Listener binds to one RFC1918 private IPv4 adapter on port 47832.
- Only pairing claim and status routes are exposed.
- No local approval, arbitrary command, screen capture, or remote input route is exposed by the pairing service.

## Android client

- Device list, SmartDeck, session view, touchpad, keyboard, File Bridge, and Security Shield are planned modules.
- QR scanning uses Google Code Scanner.
- Device signing key is P-256 ECDSA and is held in Android Keystore.
- Device credentials must not leave the device.
- The phone checks its chosen computer address before sending the claim.
- HTTPS certificate SPKI is pinned to the one-time QR invite; normal hostname verification remains enabled.

## Same-LAN pairing flow

1. The Windows user opens FASX and selects Pair a phone.
2. FASX selects an active RFC1918 private IPv4 address.
3. It generates an ephemeral TLS server certificate and SPKI SHA-256 pin.
4. The user explicitly enables the listener; Kestrel binds only that local address and port 47832.
5. FASX creates a short-lived 256-bit invitation secret and QR.
6. The Android app scans it, validates the endpoint/pin/expiry, and asks the phone owner to confirm.
7. The Android app signs the FASX pairing proof with its Keystore key.
8. The claim is submitted using HTTPS with the scanned SPKI pin.
9. The Windows companion verifies the invitation secret and signature and displays the device for local approval.
10. The Windows user approves or denies. Approved public-device identity is DPAPI protected.
11. Android polls a read-only status route. Pairing does not grant screen or input access.

## Pairing service routes

- `POST /api/pairing/claim` — accepts a signed device claim. Fixed-window rate limit.
- `GET /api/pairing/status/{pairingId}` — returns invitation state only. Separate higher polling limit.
- Local approval, denial, cancellation, and device revocation are not HTTP routes.

## Data boundaries

- Pairing HTTP: short-lived pairing secret, device identity, public key, proof nonce, signature, and state.
- Windows local storage: paired public identities only, protected by DPAPI.
- Android storage: private signing key in Android Keystore and local device ID.
- Future signaling plane: authenticated device identity, challenges, ICE offers/candidates, minimal session metadata.
- Future media plane: encrypted screen/audio stream and real-time input channel.
- Audit store: time, device, action category, approval, and result; never typed text or screen content by default.

## Future screen capture and media

Windows screen capture should use Windows.Graphics.Capture with the user selecting a display/window via the system picker and visible capture indication:
https://learn.microsoft.com/en-us/windows/uwp/audio-video-camera/screen-capture

WebRTC security architecture:
https://www.rfc-editor.org/rfc/rfc8827.html

## RustDesk evaluation

RustDesk remains a candidate reference or integration option, not an assumed dependency. Its repository is published under AGPL-3.0. Before copying, linking, modifying, or distributing any code, complete a license and dependency review.
- https://github.com/rustdesk/rustdesk
- https://github.com/rustdesk/rustdesk/blob/master/LICENCE

## Reliability requirements

- Reconnection must not silently restore permissions after an approval expires.
- A dropped control channel must fail closed for remote input.
- Loss of host visibility or session consent must stop capture and control.
- TURN relay usage must be disclosed in connection details.
- File transfers must be resumable only after re-authentication and permission revalidation.
