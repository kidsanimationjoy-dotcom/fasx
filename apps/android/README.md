# FASX Android Client

## Project status

The Android client contains the same-LAN pairing flow and the view-only WebRTC receiver source. GitHub Actions successfully assembled the current debug APK. Real-device pairing and screen-stream playback on the target phone and Windows computer are still pending, so this is a development build rather than a production release.

## Build environment

- Android Gradle Plugin 9.4.0
- Gradle 9.6.0
- Kotlin 2.4.10
- Jetpack Compose BOM 2026.11.01
- Google Play services Code Scanner 16.1.0
- OkHttp 4.12.0
- Java 17
- Android SDK 37.2 (API level 37)
- Minimum Android API 26

## Build

From this directory, with Android SDK and Gradle 9.6 installed:

```bash
gradle --no-daemon :app:assembleDebug
```

## Included in this foundation

- Compose SmartDeck landing screen.
- Google Code Scanner integration for FASX QR invitations.
- Strict invitation parsing: version, invitation type, 256-bit secret, HTTPS endpoint, expected claim/session route and port, SPKI pin, and short expiry.
- P-256 ECDSA device signing key held in Android Keystore.
- Locally stored device identity ID.
- Phone-side confirmation of the scanned computer address before transmission.
- Pinned HTTPS pairing claim and pairing-status polling.
- View-session QR parsing and validation for an already-paired device.
- Signed view-session request using the Android Keystore identity.
- Session-token-protected status and offer retrieval, plus answer submission.
- Bundled WebView receiver page using a receive-only video transceiver.
- Restricted viewer navigation to the bundled receiver page.

## Still to verify or build

- Real same-Wi-Fi pairing test with a physical Windows computer.
- Real view-only video playback and stop/revoke test on physical devices.
- Wi-Fi interruption, phone sleep/backgrounding, expiry, and reconnect behaviour.
- Stream quality, device temperature, battery and network usage on target hardware.
- Paired-device management and revocation UI.
- Remote input, only as a later separate permission with explicit Windows-local approval.
- File Bridge, internet relay, and other later roadmap features.
- Signed production release package.

## Permission boundary

Pairing registers a phone identity; it does not itself grant screen viewing or computer control. A separate view-only session request must be approved locally on Windows, and the Windows user must explicitly start screen sharing and choose what to share. This build has no keyboard, mouse, audio, file-transfer, clipboard, or arbitrary-command channel.
