# Android Build and Version Notes

The Android foundation uses Android Gradle Plugin 9.5.0, Gradle 9.6.0, Kotlin Compose compiler plugin 2.4.21, and Jetpack Compose stable BOM 2026.11.01. AGP 9 includes built-in Kotlin support, so the legacy `org.jetbrains.kotlin.android` plugin is not applied.

The Windows solution targets .NET 10 LTS. The November 10, 2026 servicing release is SDK 10.0.404 / runtime 10.0.15. Keep SDK and runtime servicing patches current.

Official references:
- .NET support policy: https://dotnet.microsoft.com/en-us/platform/support/policy
- Android Gradle Plugin release notes: https://developer.android.com/build/releases/agp-9-5-0-release-notes
- Compose compiler and dependency setup: https://developer.android.com/develop/ui/compose/setup-compose-dependencies-and-compiler
- Kotlin releases: https://kotlinlang.org/docs/releases.html
