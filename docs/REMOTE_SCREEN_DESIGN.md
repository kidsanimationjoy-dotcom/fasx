# FASX Remote Screen Design

## Status

**Source implementation:** a view-only, same-LAN WebRTC path is now in the repository. It has separate Windows and Android sender/receiver pages, authenticated session signaling, and local permission checks.

**Release status:** the new Windows/Android CI run and a real-device same-Wi-Fi test are still required. Do not treat the source implementation as a verified remote-desktop release until the Windows companion and Android app are tested together on actual devices.

## User-approved capture

The Windows sender uses Microsoft Edge WebView2 and the Screen Capture API:

- The local sender page is packaged under the Windows app's `wwwroot` directory and loaded through a virtual HTTPS origin.
- The phone must already be paired with the Windows computer.
- The phone requests a new session by signing a fresh session challenge with the P-256 private key stored in Android Keystore.
- Windows verifies the signature against that phone's stored paired public key.
- Windows displays the request and the user must approve **View screen**.
- The Windows user must then press **Start screen sharing** and choose a display or application window in the system picker.
- WebView2 raises `ScreenCaptureStarting`; the host window shows capture state.
- The sender adds only the selected video track to an RTCPeerConnection. It does not add audio or input/data channels.
- Stop, window close, permission expiry, or a disconnected/failed peer tears down the capture and revokes the view permission.

The capture page calls `navigator.mediaDevices.getDisplayMedia()` only from a direct user click. The W3C Screen Capture specification requires a user-agent-controlled display selection each time. References:
- https://www.w3.org/TR/screen-capture/
- https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/winrt/microsoft.web.webview2.core/corewebview2screencapturestartingeventargs
- https://developer.mozilla.org/en-US/docs/Web/API/MediaDevices/getDisplayMedia

## Android receiver

The Android app loads a bundled local WebView page. It:

- creates an RTCPeerConnection with no public STUN/TURN servers in this LAN-only prototype;
- adds a receive-only video transceiver;
- applies the Windows offer;
- creates an SDP answer and waits for ICE candidate gathering to complete;
- submits the answer over the temporary HTTPS endpoint with the session token;
- displays the received video track in a local video element.

The Android WebView is not granted camera, microphone, location, or file permissions for this receiver. The viewer page contains no keyboard or pointer event forwarding.

## Authenticated view-session signaling

The Windows companion starts a temporary HTTPS endpoint only when the user creates a view invitation. It binds to one private RFC1918 IPv4 interface on port 47834.

The invitation QR includes:
- random session ID;
- 256-bit one-time session secret;
- 256-bit device proof nonce;
- private IPv4 request endpoint;
- SHA-256 SPKI pin for the temporary host certificate;
- short expiry.

The phone owner confirms the computer address before sending. The phone signs the challenge using its paired P-256 key. Windows verifies the signature against the paired-device public key and checks the one-time secret. Windows then creates a pending session request for **ViewScreen** only.

The session secret is also used as a token for status, offer and answer exchanges. The session request must be locally approved before the offer endpoint returns an SDP offer. The local approval lease is separate from the short request expiry and is limited to 10 minutes.

The signaling service validates SDP length and basic format, uses fixed-window request limits, disables response caching, and stores only one offer and one answer for this one-time session.

## Same-LAN transport

The first prototype uses browser-native WebRTC with an empty ICE server list. It is intended for direct connections between devices on the same reachable private network. It does not provide:

- STUN-based public-address discovery;
- TURN relay fallback;
- remote-from-anywhere access;
- a persistent internet signaling service.

A router's client-isolation setting, separate VLANs, Windows firewall policy, or network topology may prevent the direct peer connection. Do not disable firewall/security features or weaken router protection to force a connection. Record the failure and test on a trusted home network.

WebRTC media uses its standardized secure transport. FASX does not invent its own media encryption:
- https://www.rfc-editor.org/rfc/rfc8827.html

## What is deliberately not included

- Remote keyboard or mouse input.
- Audio.
- Clipboard synchronization.
- File transfer.
- Power actions or arbitrary command execution.
- Hidden/unattended capture.
- Internet relay.

Those capabilities require separate permissions and separate design/review. Pairing or approval of ViewScreen never grants ControlInput.

## Validation required before release

1. CI build and package for Windows and Android.
2. Pair the actual phone and Windows laptop on a trusted private Wi-Fi network.
3. Create a view invitation for that paired phone and confirm the device identity is verified.
4. Approve View screen locally; verify no offer is returned before approval.
5. Press Start screen sharing and select a single application window.
6. Confirm the phone receives the video and no pointer/keyboard control is possible.
7. Stop from Windows and from Android; verify the peer/capture closes and permission is revoked.
8. Test wrong session token, wrong key signature, expired request, expired approval, phone disconnect, Windows close, Wi-Fi loss and re-pair.
9. Measure resolution, frame rate, CPU/GPU use, thermal effect, and mobile data/Wi-Fi use on the target hardware.
