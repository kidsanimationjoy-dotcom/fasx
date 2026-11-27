# FASX Remote Screen Design

## Status

**Local capture preview:** implemented in the Windows companion source and included in the current build pipeline. Runtime testing on a physical Windows computer remains required.

**Phone streaming:** not implemented. No screen frames are transmitted by the preview window.

## Capture source

The first capture proof of concept uses Microsoft Edge WebView2 and the browser Screen Capture API:

- The local FASX page is served from the app's packaged `wwwroot` folder through a virtual HTTPS origin.
- The owner must press **Start local preview** inside the FASX preview window.
- The page invokes `navigator.mediaDevices.getDisplayMedia()` from that direct user click.
- The Windows/Edge picker lets the user select a display or application window.
- Windows displays its system capture border around the selected item.
- Capture tracks stop when the user presses Stop, the captured item ends, or the preview window closes.
- The preview is displayed locally only.

This uses the WebView2 ScreenCaptureStarting event to reflect capture state in the native WPF window. The application denies other browser permission requests; no camera, microphone, location, clipboard or general web permission is granted by this preview.

Microsoft reference:
- https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/winrt/microsoft.web.webview2.core/corewebview2
- https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture

## Why this route

WebView2 embeds the Microsoft Edge browser engine in a WPF app and provides a supported WPF integration. Its Screen Capture API surfaces a user-controlled selection flow. It avoids implementing screen capture using hidden desktop scraping or an ad-hoc GDI screenshot loop.

The WebView2 runtime is a Windows prerequisite. The self-contained FASX executable bundles the .NET application, not the Evergreen WebView2 Runtime. If WebView2 is missing, FASX should show a clear installation message rather than bypassing the requirement.

## Streaming transport decision

WebRTC remains the preferred candidate for live screen delivery because it provides standardized peer-connection, ICE network traversal, RTP media and DTLS-SRTP protected media. FASX must use a maintained implementation with a license that supports the product's distribution model; it must not invent its own media encryption.

The following items still require a proof of concept and license review before choosing the final Android/Windows transport:
- Windows WebView2 WebRTC sender behavior and media encoding.
- Android receiver implementation and decoding performance on target devices.
- Android WebView support compared with a native WebRTC Android SDK.
- Same-Wi-Fi direct connection and ICE negotiation.
- Internet relay/TURN design and operating cost.
- Resolution, frame rate, bitrate, thermal and mobile data usage.
- Session expiry and permission revocation while a stream is active.

## Required next stream protocol

The future remote-session handshake must be separate from pairing:

1. The phone uses its paired device identity to request a short-lived remote session.
2. Windows displays the phone identity and requested capability set.
3. The user approves **ViewScreen** only; no input-control permission is implied.
4. The Windows session opens the system capture picker on a clear user action.
5. The peers exchange authenticated signaling data over the already-pinned host connection.
6. WebRTC establishes an encrypted peer connection, directly on the LAN where possible.
7. The Windows companion displays the active phone, capture state, stream status and an immediate Stop button.
8. Either side can end the session. Revoking permission closes the media and data channels.

## Not part of the preview milestone

- Sending screen frames to the phone.
- Remote keyboard or mouse input.
- Audio.
- Clipboard or file access.
- Unattended or hidden capture.
- Internet relay.
