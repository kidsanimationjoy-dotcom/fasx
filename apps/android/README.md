# FASX Android Client

## Build environment

- Android Gradle Plugin 9.4.0
- Gradle 9.6.0
- Kotlin 2.4.10
- Jetpack Compose BOM 2026.11.01
- Google Play services Code Scanner 16.1.0
- Java 17
- Android SDK 37
- Minimum Android API 26

These versions follow the Android Gradle Plugin 9.4 compatibility guidance and the stable November 2026 Compose release. The Code Scanner API delegates QR scanning to Google Play services and does not require FASX to request camera permission. See:
- https://developer.android.com/build/releases/agp-9-4-0-release-notes
- https://developer.android.com/blog/posts/jetpack-compose-november-2026-release
- https://kotlinlang.org/docs/releases.html
- https://developers.google.com/ml-kit/vision/barcode-scanning/code-scanner

## Build

From this directory, with Android SDK and Gradle 9.6 installed:

```bash
gradle --no-daemon :app:assembleDebug
```

## Included in this foundation

- Compose SmartDeck landing screen.
- Google Code Scanner integration for FASX QR invitations.
- Strict invitation parsing: version, type, 256-bit secret, HTTPS endpoint, SHA-256 certificate pin, and short expiry.
- P-256 ECDSA device signing key created and held in Android Keystore.
- Device identity ID stored locally.

## Not yet connected

The Android app validates the invitation and can create the device proof locally. It does not yet submit the claim to a Windows host. The Windows LAN transport, certificate/pinning handshake, pending-request approval screen, and end-to-end paired-device test are the next implementation steps.
