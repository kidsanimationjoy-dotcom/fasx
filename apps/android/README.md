# FASX Android Client

## Build environment

- Android Gradle Plugin 9.4.0
- Gradle 9.6.0
- Kotlin 2.4.10
- Jetpack Compose BOM 2026.11.01
- Google Play services Code Scanner 16.1.0
- OkHttp 4.12.0
- Java 17
- Android SDK 37
- Minimum Android API 26

## Build

From this directory, with Android SDK and Gradle 9.6 installed:

```bash
gradle --no-daemon :app:assembleDebug
```

## Included in this foundation

- Compose SmartDeck landing screen.
- Google Code Scanner integration for FASX QR invitations.
- Strict invitation parsing: version, type, 256-bit secret, HTTPS endpoint, specific claim route/port, SPKI pin, and short expiry.
- P-256 ECDSA device signing key held in Android Keystore.
- Device identity ID stored locally.
- Phone-side confirmation of the scanned computer address before transmission.
- HTTPS claim submission with certificate SPKI pin verification.
- Read-only polling of pairing state.

## What remains

- Build validation in GitHub Actions and Android Studio.
- Real phone-to-Windows same-network pairing test.
- Re-pair/revoke device screen.
- Persisted paired-device list in Android.
- Remote screen, touchpad, keyboard, File Bridge, and session permission UI.

This app does not enable remote control merely because it has paired a device. Every remote session must be separately approved by the Windows user.
