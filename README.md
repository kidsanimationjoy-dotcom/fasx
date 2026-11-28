# FASX

**Your computer. Your command centre.**

FASX is an all-in-one personal computer command centre combining secure remote desktop, device management, customizable controls, and user-approved automation.

> **Project status: early development.** The repository contains same-LAN phone pairing, local Windows session approval, WebRTC screen viewing, and an optional permission-gated input data channel. Windows and Android CI plus real-device checks are still required; this is not a production remote-desktop release.

## Product vision

FASX is designed for students, professionals, creators, everyday users, and (in a separate managed mode) businesses and IT-support teams.

It combines three experiences:
1. **Personal computer command centre** — a customizable place to manage and operate your computer.
2. **Advanced remote desktop** — view and control an authorized Windows computer from a paired phone or another approved device.
3. **Smart computer companion** — context-aware controls, diagnostics, and workflows that users create and approve.

## Signature FASX systems

| System | Purpose |
|---|---|
| FASX Remote | Remote screen, touch, keyboard, and computer control |
| SmartDeck | A customizable command centre |
| Context Control | Offers relevant controls for the active application, with user permission |
| FASX Flows | User-created workflows built from approved actions |
| FASX Intelligence | Explainable PC health insights and assistance |
| File Bridge | Two-way file management and transfers |
| Connection Guardian | Connection quality, recovery, and device presence |
| Security Shield | Pairing, permissions, consent, and audit history |
| Deck Gallery | Shareable SmartDeck layouts and safe workflow templates |
| FASX Business | Managed devices, teams, and administration |

## Platforms

- **Host:** Windows 10/11 desktop companion.
- **Controller:** Android phone app.
- **Future:** Additional desktop and mobile platforms after Windows/Android stability.

## Repository layout

- `src/Fasx.Agent.Core` — platform-independent session, permission, and pairing rules.
- `src/Fasx.Agent.Windows` — visible Windows companion, temporary LAN pairing listener, and protected device storage.
- `apps/android` — Android Compose client, QR parser, pinned HTTPS client, and device identity.
- `services/signaling` — planned internet signaling/control plane.
- `tests/Fasx.Agent.Core.Tests` — tests for shared security/domain rules.
- `docs` — product, architecture, security, protocol, pairing, research, and roadmap.

## Build Windows companion

Requirements: Windows 10/11, Visual Studio 2026 or the .NET 10 SDK, and the Microsoft Edge WebView2 Runtime for the local screen preview.

```powershell
dotnet restore FASX.sln
dotnet build FASX.sln -c Release
dotnet test FASX.sln -c Release
dotnet run --project src/Fasx.Agent.Windows/Fasx.Agent.Windows.csproj
```

The pairing window can start a temporary HTTPS listener only when the user selects **Pair a phone**. It binds to one private IPv4 adapter on port 47832. Pairing requests require local approval. The invitation expires and the listener closes when pairing is stopped or its window is closed.

## Build Android app

Requirements: Android Studio with Android SDK 37, Java 17, and Gradle 9.6.

```bash
cd apps/android
gradle --no-daemon :app:assembleDebug
```

The Android app scans QR pairing or view-session invitations, asks the phone owner to confirm the computer address, creates its P-256 signing key in Android Keystore, pins the temporary Windows host certificate, and submits signed requests over HTTPS. A remote-session request requires Windows-local approval before a WebRTC offer can be retrieved. Optional input must be separately requested by the Windows owner and separately approved on the Windows computer.

## Current capabilities and limitations

**Implemented in source (latest build and real-device verification still pending):**
- Local permission policy and tests.
- Short-lived, single-use pairing invitations.
- Device proof verification using ECDSA P-256 / SHA-256.
- Local Windows approve/deny flow.
- DPAPI-protected persistence of paired public-device identities.
- Temporary same-LAN IPv4 HTTPS pairing endpoint.
- Android QR scanner, strict invitation parsing, Android Keystore device identity, pinned HTTPS claim and status polling.
- Local Windows screen-capture preview using WebView2 and the Windows-owned display/window picker.
- A signed remote-session request from an already-paired Android device.
- Windows-local approval for `ViewScreen` with a short, separate session lease.
- Optional `ControlInput` request, followed by a separate explicit checkbox approval on Windows.
- Token-protected, same-LAN WebRTC offer/answer signaling over the pinned HTTPS session endpoint.
- Windows WebView2 video sender and Android WebView video receiver. The Windows owner must press Start and choose the screen/window.
- Optional WebRTC data channel for pointer movement/clicks/scrolling, a limited navigation-key allowlist, and short printable text entry. The Windows host validates every message again and drops input unless `ControlInput` is currently approved.
- Local PC Health panel with read-only Windows version, processor count, physical memory, system-drive capacity/free space, .NET version, and uptime. It refreshes only when opened or manually refreshed and does not upload telemetry.

**Not implemented yet:**
- Verified CI build or signed installer.
- Real phone-to-Windows pairing test.
- Verified real-device screen streaming (the source implementation is present but not yet tested on the target laptop/phone).
- Physical-device verification of pairing, screen streaming and optional input on the target laptop/phone.
- Full keyboard shortcuts, arbitrary keys, app automation, file transfer and clipboard synchronization.
- Internet signaling/relay and remote-from-anywhere connections.
- SmartDeck customization, FASX Flows execution and AI diagnostics.
- Signed production installers/packages.

## Architecture and security

- Windows companion: C# / .NET 10 LTS / WPF.
- Android client: Kotlin and Jetpack Compose.
- Local view-session signaling: temporary ASP.NET Core HTTPS endpoint bound to one private IPv4 address.
- Local media and input transport: browser-native WebRTC with no STUN/TURN relay in this first LAN-only prototype.
- Internet signaling and relay infrastructure are not implemented.
- Local pairing uses HTTPS with an invitation-bound SHA-256 SPKI pin.
- Windows device records are protected with DPAPI for the current Windows user.
- Android device signing keys are held in Android Keystore.

Read [the architecture](docs/ARCHITECTURE.md), [security model](docs/SECURITY_MODEL.md), [pairing flow](docs/PAIRING_FLOW.md), [remote screen design](docs/REMOTE_SCREEN_DESIGN.md), and [local device test checklist](docs/LOCAL_PAIRING_TEST.md).

## Non-negotiable safety principles

Remote access is visible and deliberately enabled by the person at the Windows computer. Pairing does not grant remote-control permissions. Every session must request only the capabilities it needs and receive local approval. No hidden access, stealth persistence, credential harvesting, arbitrary remote shell, or capture outside a clear, user-started Windows capture flow.
