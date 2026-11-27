# FASX

**Your computer. Your command centre.**

FASX is an all-in-one personal computer command centre combining secure remote desktop, device management, customizable controls, and user-approved automation.

> **Project status: early foundation.** The repository now contains a Windows companion shell, testable pairing/security domain logic, protected Windows storage for trusted device public identities, and an Android Compose preview with QR scanning and device-key generation. The phone and host are not yet connected over a network.

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

## First target platforms

- **Host:** Windows 10/11 desktop companion.
- **Controller:** Android phone app.
- **Future:** Additional desktop and mobile platforms after Windows/Android stability.

## Repository layout

- `src/Fasx.Agent.Core` — platform-independent session, permission, and pairing rules.
- `src/Fasx.Agent.Windows` — visible Windows companion shell and protected device storage.
- `apps/android` — Android Compose client foundation and QR/device identity code.
- `services/signaling` — planned signaling/control plane.
- `tests/Fasx.Agent.Core.Tests` — tests for shared security/domain rules.
- `docs` — product, architecture, security, protocol, pairing, research, and roadmap.

## Build the Windows foundation

Requirements: Windows, Visual Studio 2026 or the .NET 10 SDK.

```powershell
dotnet restore FASX.sln
dotnet build FASX.sln -c Release
dotnet test FASX.sln -c Release
```

The Windows companion is currently a visible shell. It loads paired public-device identities from a DPAPI-protected store. It does not yet create QR invitations, accept network claims, share a screen, or accept remote input.

## Build the Android foundation

Requirements: Android Studio with Android SDK 37, Java 17, and Gradle 9.6.

```bash
cd apps/android
gradle --no-daemon :app:assembleDebug
```

The Android app currently has a Compose SmartDeck preview, QR scanner, strict invitation parser, and a P-256 signing key stored in Android Keystore. It validates an invitation and creates a local proof but does not yet send the claim to Windows.

## Architecture direction

- Windows companion: C# / .NET 10 LTS / WPF.
- Android client: Kotlin and Jetpack Compose.
- Control plane: ASP.NET Core for pairing, signaling, and device presence.
- Media and low-latency input: evaluate WebRTC; use established WebRTC security rather than inventing cryptography.
- Direct peer-to-peer connection where possible, with an authenticated TURN relay fallback.

Read [the architecture](docs/ARCHITECTURE.md), [security model](docs/SECURITY_MODEL.md), and [pairing flow](docs/PAIRING_FLOW.md).

## Non-negotiable safety principles

- Remote access is visible to the person at the Windows computer.
- Pairing requires deliberate local approval.
- Each session receives only the permissions approved for that session.
- Screen capture uses the Windows system capture picker and its visible capture indicator.
- File access is limited to approved locations and explicit operations.
- No hidden access, stealth persistence, credential harvesting, or arbitrary remote shell.
- Sensitive actions such as shutdown, restart, and file deletion require clear confirmation.
- The local user can end a session immediately.

## Current milestone

See [the roadmap](docs/ROADMAP.md). The next milestone is TLS-protected LAN pairing with QR generation on Windows, phone-to-host claim submission, local approval UI, and real-device tests.
