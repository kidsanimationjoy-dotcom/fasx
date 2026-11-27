# FASX Android Client

**Status: UI foundation scaffold.** The current app provides Home, Devices, and Security tabs plus the first SmartDeck layout. It does not yet scan pairing QR codes, authenticate to a signaling service, or control a computer.

## Stack

- Kotlin with Android Gradle Plugin 9.5.0.
- Kotlin Compose compiler plugin 2.4.21.
- Jetpack Compose stable BOM 2026.11.01.
- Minimum Android API 26; compile/target API 37.
- Android Studio with JDK 17 or newer.

## Build

Open `apps/android` in Android Studio and sync Gradle, or use an installed Gradle 9.6+ distribution:

```bash
gradle -p apps/android :app:assembleDebug
gradle -p apps/android :app:testDebugUnitTest
```

The first connected release will add QR scanning, device-key enrollment, local approval status, and the authenticated signaling protocol. Do not treat the foundation preview as a working remote-control app.
