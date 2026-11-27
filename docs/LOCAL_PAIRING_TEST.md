# FASX Same-LAN Pairing Test Checklist

## Purpose

This checklist verifies the first FASX milestone: a phone can request pairing with a Windows computer on the same trusted local network, and the Windows user must approve the request.

This is a development build, not a public release. The Windows executable is not code-signed, and the Android package is a debug APK. Do not disable SmartScreen, Microsoft Defender, Google Play Protect, or device security controls to force installation or execution. If a device blocks a build, stop and record the warning; a signed release package is a later task. For a minor user, have a trusted adult review the test build and supervise installation.

## Before testing

- Use only devices you own or have permission to test.
- Put the Windows computer and Android phone on the same trusted home Wi-Fi network.
- Do not use a public hotspot, school/work shared network, or an untrusted network.
- Confirm the Windows computer has an active private IPv4 address.
- Keep the Windows FASX companion visible during the pairing test.
- Do not disable a firewall or create a public inbound firewall rule.

## Expected pairing flow

1. Start the FASX Windows companion.
2. Select **Pair a phone**.
3. Select **Enable local pairing**. The companion should display a QR invitation, local IP address, and expiry time.
4. Open the FASX Android debug app and select **Scan FASX invitation**.
5. Scan the QR. Review the displayed computer IP address and confirm only when it is the computer you intend to pair.
6. The phone sends its signed device identity over the QR-pinned HTTPS connection.
7. The Windows pairing window should show a pending request with the phone's device name and a shortened device ID.
8. Select **Approve device** on Windows.
9. The Android app should report that the device is paired.
10. Close the pairing window or select **Stop pairing** on Windows. The temporary listener should close and any still-pending invitation should be cancelled.

## Negative checks

- Create a new QR after an earlier QR was issued. The earlier invitation should no longer be usable.
- Let a QR invitation expire. A phone should be told to request a fresh invitation.
- Select **Deny** on Windows. The phone should report that the request was denied.
- Stop pairing while a request is pending. The phone should report that pairing was cancelled.
- Scan a QR with the wrong format or an expired invitation. The Android app should reject it before connecting.

## What this milestone does not do

Pairing only registers a trusted phone public key on the Windows computer. It does not grant remote-control permissions and it does not enable:

- live screen viewing or recording;
- remote keyboard or mouse input;
- audio or peripheral redirection;
- file transfer or clipboard sync;
- remote restart/shutdown;
- access from outside the local network.

These are later, separate milestones requiring their own session permissions and real-device tests.

## Troubleshooting notes

- If the Windows computer has more than one active private network adapter, check the IP address shown under the QR. The phone must be on a network that can reach that address.
- Some Wi-Fi routers enable client isolation, which prevents devices on the same Wi-Fi from communicating. Do not change router security settings to work around this on a shared network.
- A Windows firewall prompt may appear when local pairing is enabled. Do not turn the firewall off. Only consider allowing FASX on a trusted **Private** network, and do not enable access on Public networks.
- If a certificate, pin, or HTTPS validation error appears, stop the test. Do not ignore or bypass the certificate warning.
- If a security feature blocks the unsigned test package, do not disable it. Record the exact warning and use a signed test build when available.

## Test report template

Record the result without sharing private network details publicly:

| Check | Result |
|---|---|
| Windows companion launches | Pass / Fail / Not tested |
| Pairing listener starts on trusted private network | Pass / Fail / Not tested |
| QR appears and shows expiry | Pass / Fail / Not tested |
| Android scans and validates QR | Pass / Fail / Not tested |
| Android asks for confirmation before sending | Pass / Fail / Not tested |
| Windows displays pending phone request | Pass / Fail / Not tested |
| Windows approval succeeds | Pass / Fail / Not tested |
| Android receives approved status | Pass / Fail / Not tested |
| Stop/cancel closes pairing | Pass / Fail / Not tested |
| Denial and expiry are handled | Pass / Fail / Not tested |

Do not include public IP addresses, device serial numbers, Wi-Fi passwords, private keys, or pairing QR codes in an issue or public post.


## View-only session and screen-stream test

These checks are for a development build only and must be run on devices you own, on a trusted private home network.

1. Pair the phone first using **Pair a phone**.
2. On Windows choose **Request view-only session** and select the paired phone.
3. Scan the new **fasx-view** QR in the Android app and confirm the Windows IP shown in the phone dialog.
4. Confirm that the Windows app shows the request from the selected paired phone.
5. Before approving, verify that the phone cannot retrieve a WebRTC offer or see the screen.
6. On Windows approve **View screen** only.
7. On Windows press **Start screen sharing** and select a single application window in the system picker.
8. Confirm that the phone displays that video and that Windows visibly shows the capture indicator.
9. Press **Stop session** on Windows. Verify the phone viewer disconnects and the permission is revoked.
10. Repeat and stop viewing from the phone. Verify the Windows side stops sharing when the peer disconnects.

Negative checks:

- Request a view session from a phone other than the paired device selected on Windows; the request must be rejected.
- Change the session token or device signature; the request or offer/answer call must fail.
- Let the session request or approval lease expire; the screen stream must stop.
- Disconnect the phone from Wi-Fi or close the viewer; the Windows sender must stop capture and revoke View screen.
- Verify there is no keyboard, pointer, audio, clipboard, or file-transfer channel in this build.

This view-only source implementation is not yet confirmed on the target Windows/Android hardware. Do not disable firewalls, SmartScreen, Defender, or Play Protect for the test.
