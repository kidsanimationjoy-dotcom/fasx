# FASX

**Your computer. Your command centre.**

FASX is an all-in-one personal computer command centre combining secure remote desktop, device management, customizable controls, and user-approved automation.

> **Project status: early development.** The repository contains a visible Windows companion, testable session/pairing security logic, DPAPI-protected Windows storage for trusted device identities, a temporary same-LAN HTTPS pairing service, and an Android Compose client with QR scanning and Android Keystore device identity. This is an engineering milestone, not yet a remote desktop release.

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

The Android app scans and validates a QR invitation, asks the phone owner to confirm the displayed computer address, creates a P-256 signing key in Android Keystore, pins the temporary Windows host certificate, submits a claim over HTTPS, and polls for local approval.

## Current capabilities and limitations

**Implemented in source (build and device verification pending):**
- Local permission policy and tests.
- Short-lived, single-use pairing invitations.
- Device proof verification using ECDSA P-256 / SHA-256.
- Local Windows approve/deny flow.
- DPAPI-protected persistence of paired public-device identities.
- Temporary same-LAN IPv4 HTTPS pairing endpoint.
- Android QR scanner, strict invitation parsing, Android Keystore device identity, pinned HTTPS claim and status polling.
- Local Windows screen-capture preview using WebView2 and the Windows-owned display/window picker.

**Not implemented yet:**
- Verified CI build or signed installer.
- Real phone-to-Windows pairing test.
- Live screen streaming from Windows to the phone (the current screen feature is local preview only).
- Remote keyboard/mouse input.
- File transfer and clipboard synchronization.
- Internet relay / remote-from-anywhere connections.
- SmartDeck customization, Flows execution, and AI diagnostics.

## Architecture and security

- Windows companion: C# / .NET 10 LTS / WPF.
- Android client: Kotlin and Jetpack Compose.
- Control plane: ASP.NET Core for internet signaling and device presence.
- Candidate media transport: WebRTC, pending an end-to-end proof of concept.
- Local pairing uses HTTPS with an invitation-bound SHA-256 SPKI pin.
- Windows device records are protected with DPAPI for the current Windows user.
- Android device signing keys are held in Android Keystore.

Read [the architecture](docs/ARCHITECTURE.md), [security model](docs/SECURITY_MODEL.md), [pairing flow](docs/PAIRING_FLOW.md), and [remote screen design](docs/REMOTE_SCREEN_DESIGN.md).

## Non-negotiable safety principles

Remote access is visible and deliberately enabled by the person at the Windows computer. Pairing does not grant remote-control permissions. Every session must request only the capabilities it needs and receive local approval. No hidden access, stealth persistence, credential harvesting, arbitrary remote shell, or capture outside a clear, user-started Windows capture flow.
