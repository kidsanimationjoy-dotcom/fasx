# FASX Remote Screen Design

## Status

**Source implementation:** a same-LAN WebRTC screen-view path is in the repository. It includes separately approved input, local Connection Guardian metrics, and a separate phone-to-PC file-transfer path. Windows Release build, unit tests, self-contained publish, and Android debug APK assembly passed on branch commit `a59722d49cccae7b7e3927d189b0e286ae998979`; physical-device tests are still required.

**Release status:** Windows build, tests, self-contained publish and Android debug APK assembly passed on the commit above. A real-device same-Wi-Fi test, File Bridge transfer, permission-revocation test and performance measurements are still required. Do not treat the source implementation as a verified remote-desktop release until the Windows companion and Android app are tested together on actual devices.

## User-approved capture

The Windows sender uses Microsoft Edge WebView2 and the Screen Capture API:

- The local sender page is packaged under the Windows app's `wwwroot` directory and loaded through a virtual HTTPS origin.
- The phone must already be paired with the Windows computer.
- The phone requests a new session by signing a fresh session challenge with the P-256 private key stored in Android Keystore.
- Windows verifies the signature against that phone's stored paired public key.
- Windows displays the request and the user must approve **View screen**. The invitation can optionally request **ControlInput**, but a second Windows checkbox must be selected before input is granted.
- The Windows user must then press **Start screen sharing** and choose a display or application window in the system picker.
- WebView2 raises `ScreenCaptureStarting`; the host window shows capture state.
- The sender adds only the selected video track to the RTCPeerConnection. It creates a `fasx-control` data channel only when ControlInput is currently approved.
- Stop, window close, permission expiry, or a disconnected/failed peer tears down the capture and revokes the view permission.

The capture page calls `navigator.mediaDevices.getDisplayMedia()` only from a direct user click. The W3C Screen Capture specification requires a user-agent-controlled display selection each time. References:
- https://www.w3.org/TR/screen-capture/
- https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/winrt/microsoft.web.webview2.core/corewebview2screencapturestartingeventargs
- https://developer.mozilla.org/en-US/docs/Web/API/MediaDevices/getDisplayMedia

## Android receiver

The Android app loads a bundled local WebView page. It:

- creates an RTCPeerConnection with no public STUN/TURN servers in this LAN-only prototype;
- adds a receive-only video transceiver;
- accepts the optional `fasx-control` data channel only when the Windows host has approved ControlInput;
- exposes File Bridge only when the Windows host has separately approved TransferFiles; the phone user selects a single file using Android's system document picker;
- applies the Windows offer;
- creates an SDP answer and waits for ICE candidate gathering to complete;
- submits the answer over the temporary HTTPS endpoint with the session token;
- displays the received video track in a local video element.

The Android WebView is not granted camera, microphone, location, or file permissions for this receiver. The viewer page exposes pointer actions, a limited navigation-key allowlist, and short text entry only when the approved input data channel opens.

## Authenticated view-session signaling

The Windows companion starts a temporary HTTPS endpoint only when the user creates a view invitation. It binds to one private RFC1918 IPv4 interface on port 47834.

The invitation QR includes:
- random session ID;
- 256-bit one-time session secret;
- 256-bit device proof nonce;
- private IPv4 request endpoint;
- SHA-256 SPKI pin for the temporary host certificate;
- short expiry.

The phone owner confirms the computer address before sending. The phone signs the challenge using its paired P-256 key. Windows verifies the signature against the paired-device public key and checks the one-time secret. Windows then creates a pending request for **ViewScreen**, optionally including **ControlInput** when the Windows owner selected it before creating the invitation.

The session secret is also used as a token for status, offer and answer exchanges. The session request must be locally approved before the offer endpoint returns an SDP offer. The local approval lease is separate from the short request expiry and is limited to 10 minutes.

The signaling service validates SDP length and basic format, uses fixed-window request limits, disables response caching, and stores only one offer and one answer for this one-time session.

## Same-LAN transport

The first prototype uses browser-native WebRTC with an empty ICE server list. It is intended for direct connections between devices on the same reachable private network. It does not provide:

- STUN-based public-address discovery;
- TURN relay fallback;
- remote-from-anywhere access;
- a persistent internet signaling service.
- authenticated phone-to-PC file upload on the same temporary HTTPS session endpoint, with 20 MB cap and SHA-256 validation;

A router's client-isolation setting, separate VLANs, Windows firewall policy, or network topology may prevent the direct peer connection. Do not disable firewall/security features or weaken router protection to force a connection. Record the failure and test on a trusted home network.

WebRTC media uses its standardized secure transport. FASX does not invent its own media encryption:
- https://www.rfc-editor.org/rfc/rfc8827.html

## What is deliberately not included

- Full keyboard shortcuts or unrestricted system input.
- Audio.
- Clipboard synchronization.
- Windows-to-phone file transfer and two-way folder browsing.
- Power actions or arbitrary command execution.
- Hidden/unattended capture.
- Internet relay.

Limited pointer/key input and phone-to-PC File Bridge are implemented in source under separate `ControlInput` and `TransferFiles` permissions, but the latest changes require CI and physical-device tests. Pairing or approval of ViewScreen never grants either permission.

## Validation required before release

1. CI build and package for Windows and Android.
2. Pair the actual phone and Windows laptop on a trusted private Wi-Fi network.
3. Create a view invitation for that paired phone and confirm the device identity is verified.
4. Approve View screen locally; verify no offer is returned before approval.
5. Press Start screen sharing and choose the screen in the Windows picker.
6. Verify viewing works without input when ControlInput is not granted.
7. Request and separately approve ControlInput; verify only the allowlisted pointer, navigation-key, and printable-text actions work.
8. Stop from Windows and Android; verify input and capture stop and all permissions are revoked.
9. Test wrong session token, wrong key signature, expired request, expired approval, phone disconnect, Windows close, Wi-Fi loss and re-pair.
10. Measure resolution, frame rate, CPU/GPU use, thermal effect, and network use on the target hardware.
