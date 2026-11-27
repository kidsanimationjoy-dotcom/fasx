# FASX Architecture

## Platform baseline

- Windows companion: C# / .NET 10 LTS / WPF.
- Android controller: Kotlin / Jetpack Compose.
- Shared rules and state machines: `Fasx.Agent.Core`.
- Local pairing and view-session signaling: ASP.NET Core/Kestrel HTTPS listeners bound to one selected RFC1918 IPv4 interface.
- Same-LAN screen transport prototype: browser-native WebRTC inside Windows WebView2 and Android WebView.
- Public internet signaling and TURN relay are future work; not enabled in this prototype.

## Windows companion

- Visible WPF main window and user-started Pair a phone flow.
- Paired public-device identities are encrypted using Windows DPAPI for the current Windows user.
- Pairing invitation expires, is single-use, and can be locally cancelled.
- Pairing listener binds to one RFC1918 private IPv4 adapter on port 47832.
- Pairing exposes only claim and read-only status routes; local approval and revocation are WPF-only operations.
- A paired phone can request a separate ViewScreen session through a temporary HTTPS listener on port 47834.
- The view-session listener is bound to the same private IPv4 network interface and uses a QR-pinned ephemeral certificate.
- The selected phone must prove possession of its paired P-256 key and the view invitation secret.
- Windows locally approves ViewScreen only, with a separate 10-minute maximum lease.
- The host creates a WebRTC video offer only after local approval and after the user clicks Start screen sharing.
- WebView2 invokes the system display/window picker. The sender adds only a video track; no audio, keyboard, mouse, file, clipboard, or data channel exists.
- Local Stop, capture end, approval expiry, WebView failure, or phone disconnect closes the sender and revokes ViewScreen.

## Android client

- Jetpack Compose SmartDeck foundation and Google Code Scanner for QR invitations.
- Device P-256 ECDSA private key is held in Android Keystore; it is never exported.
- Pairing and view-session QR parsers validate schema, expiry, private IPv4 endpoint, HTTPS path/port, and TLS SPKI pin.
- The phone owner confirms the computer address before a request is sent.
- View-session requests are signed using the paired Android key.
- HTTPS status, offer, and answer requests use the per-session token and the QR-pinned host certificate.
- The bundled Android WebView receiver creates a receive-only video transceiver and renders the incoming stream.
- The view-only receiver does not forward pointer or keyboard events and has no camera/microphone permission.

## Pairing flow — port 47832

1. Windows user opens FASX and selects Pair a phone.
2. FASX selects one active RFC1918 private IPv4 address and generates an ephemeral certificate.
3. The QR includes a 256-bit single-use secret, pairing ID, endpoint, expiry, and SHA-256 SPKI pin.
4. Android scans, validates, and asks the phone owner to confirm the selected computer.
5. Android signs the pairing challenge with its Keystore key and submits over pinned HTTPS.
6. Windows verifies the secret and signature, then presents the phone for local approval.
7. Approved phone public key is saved using DPAPI.
8. Pairing is trust registration only; it does not grant a remote session.

## View-only session flow — port 47834

1. Windows user selects one already-paired phone and chooses Request view-only session.
2. FASX generates a new temporary HTTPS listener and one-time view-session QR with session ID, token, proof nonce, private IPv4 endpoint, SPKI pin, and expiry.
3. The phone owner scans and confirms the computer address.
4. Android signs the session challenge with its paired device key and submits the request.
5. Windows verifies selected device ID, one-time secret, nonce, and signature; it creates a pending SessionRequest for ViewScreen only.
6. Windows displays the pending request. No offer is returned until the Windows user approves.
7. Approval is local-only and creates a 10-minute ViewScreen lease. It does not include input, files, clipboard or audio.
8. Windows waits for the user to press Start screen sharing and select a display/window in the system picker.
9. The Windows WebView2 sender creates a WebRTC offer, gathers host ICE candidates, and submits it to the local in-process session endpoint.
10. Android retrieves the offer using the token, creates a receive-only video answer, gathers candidates and submits the answer over pinned HTTPS.
11. Windows applies the answer. Browser-native WebRTC establishes an encrypted video peer connection directly over the local network where possible.
12. Either side can stop. Windows monitors the approval lease and peer state; failure, disconnect, expiry or Stop ends the capture and revokes the session.

## Signaling routes

Pairing:
- `POST /api/pairing/claim`
- `GET /api/pairing/status/{pairingId}`

View session:
- `POST /api/view/request`
- `GET /api/view/status/{sessionId}` — requires `X-FASX-Session-Token`.
- `GET /api/view/offer/{sessionId}` — requires `X-FASX-Session-Token` and active ViewScreen approval.
- `POST /api/view/answer/{sessionId}` — requires the same one-time token in the JSON request.

Both temporary listeners:
- are started only by a visible user action;
- bind to one RFC1918 private IPv4 address, not all interfaces;
- use HTTPS with SPKI pinning in the Android app;
- rate-limit requests, cap request sizes, and set no-store response headers;
- expose no local approval, command execution, input injection, file, clipboard, or power-action routes.

## WebRTC prototype limits

- Empty ICE server list: direct same-LAN connectivity only in this prototype.
- No STUN public-address discovery or TURN relay.
- No public internet or remote-from-anywhere access.
- No audio or data channel.
- No mouse/keyboard input or file/clipboard transfer.
- Actual peer connectivity, stream quality, network compatibility, and device performance remain unverified until tested on a real Windows computer and Android phone together.

## Data boundaries

- Windows local storage: approved phone public identities only, DPAPI-protected.
- Android local storage: device ID and a non-exportable signing key in Android Keystore.
- Temporary QR/session secrets: in memory only; hashes/tokens are cleared when the listener stops.
- View-session SDP: short-lived in-memory signaling state; capped to 48 KB.
- No screen content, keystrokes, or clipboard content are written to the audit log.

## Screen capture and transport references

- WebView2: https://learn.microsoft.com/en-us/microsoft-edge/webview2/
- W3C Screen Capture API: https://www.w3.org/TR/screen-capture/
- WebRTC security architecture: https://www.rfc-editor.org/rfc/rfc8827.html

## RustDesk evaluation

RustDesk remains a candidate reference or integration option, not an assumed dependency. Its repository is published under AGPL-3.0. Before copying, linking, modifying, or distributing code, complete a license and dependency review.
- https://github.com/rustdesk/rustdesk
- https://github.com/rustdesk/rustdesk/blob/master/LICENCE

## Reliability and release requirements

- Reconnection never silently restores an expired or revoked permission.
- Network loss, peer failure, host closure, or session expiry stops screen capture and revokes the grant.
- Direct LAN pairing and view-only streaming must be tested on real devices.
- Windows Defender Firewall behaviour must be documented without disabling or weakening security.
- CI build/test/package must pass on Windows and Android.
- TURN relay, internet access, input control, audio, file transfer and clipboard are separate future milestones requiring their own permissions and tests.
