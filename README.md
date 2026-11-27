# FASX

**Your computer. Your command centre.**

FASX is an all-in-one personal computer command centre combining secure remote desktop, computer management, customizable controls, and user-approved automation.

> **Project status: foundation preview.** The repository contains a visible Windows companion shell with a local read-only PC Health panel, an Android Compose interface scaffold, a permission-domain model, and an in-memory pairing ceremony with tests. Device pairing over a network, live screen streaming, remote input, and file transfer are not implemented yet.

## Product vision

FASX is designed for students, professionals, creators, everyday users, and (in a separate managed mode) businesses and IT-support teams.

It combines three experiences:
1. **Personal computer command centre** — a customizable place to manage and operate your computer.
2. **Advanced remote desktop** — view and control an authorized Windows computer from a paired phone or another approved device.
3. **Smart computer companion** — context-aware controls, diagnostics, and workflows that users create and approve.

## Signature systems

| System | Purpose |
|---|---|
| FASX Remote | Remote screen, touch, keyboard, and computer control |
| SmartDeck | Customizable command centre |
| Context Control | Controls adapted to the active application, with user permission |
| FASX Flows | User-created workflows built from approved actions |
| FASX Intelligence | Explainable PC health insights and assistance |
| File Bridge | Two-way file management and transfers |
| Connection Guardian | Connection quality, recovery, and device presence |
| Security Shield | Pairing, permissions, consent, and audit history |
| Deck Gallery | Shareable SmartDeck layouts and safe workflow templates |
| FASX Business | Managed devices, teams, and administration |

## Current platform direction

- **Host:** Windows 10/11 desktop companion, C# / .NET 10 LTS / WPF.
- **Controller:** Android phone client, Kotlin / Jetpack Compose.
- **Control plane:** ASP.NET Core.
- **Media transport:** WebRTC candidate, pending an end-to-end proof of concept.

## Repository layout

- `src/Fasx.Agent.Core` — session permissions and pairing domain rules.
- `src/Fasx.Agent.Windows` — visible Windows companion and local PC health.
- `apps/android` — Android Compose client foundation.
- `services/signaling` — signaling/control-plane design entry point.
- `tests/Fasx.Agent.Core.Tests` — security and domain tests.
- `docs` — product, architecture, pairing, security, protocol, research, and roadmap.

## Build the Windows foundation

Requirements: Windows and the .NET 10 SDK.

```powershell
dotnet restore FASX.sln
dotnet build FASX.sln -c Release
dotnet test FASX.sln -c Release
```

## Build the Android foundation

Open `apps/android` in Android Studio and sync Gradle, or use an installed Gradle 9.6+ distribution:

```bash
gradle -p apps/android :app:assembleDebug
```

Version notes are in [docs/ANDROID_BUILD.md](docs/ANDROID_BUILD.md).

## Current limitations

- The Windows app is a local companion with read-only PC Health; remote pairing and control are not connected.
- The Android app is a UI scaffold with Home, Devices, and Security tabs.
- PairingCoordinator is an in-memory domain implementation for testing; it is not a network service or durable device registry.
- QR rendering/scanning, authenticated signaling endpoints, device-key proof, live remote desktop, remote input, and file transfer remain to be implemented.
- The phone app does not currently connect to the Windows app.

Do not treat this foundation preview as a working remote-control release.

## Security principles

- Remote access must be visible to the person at the Windows computer.
- Pairing requires deliberate local approval.
- Session permissions are independently granted and expire.
- Screen capture must use the Windows system capture picker and visible indicator.
- File access must be limited to approved locations.
- No hidden access, stealth persistence, credential harvesting, or arbitrary remote shell.
- Sensitive actions require clear confirmation.
- The local user can end a session immediately.

Read [the security model](docs/SECURITY_MODEL.md) and [pairing flow](docs/PAIRING_FLOW.md) before implementing network access.

## Development

Work in feature branches. The current production foundation work is on `production/foundation` and is proposed in [PR #1](https://github.com/kidsanimationjoy-dotcom/fasx/pull/1). Build checks must pass before merging to `main`.
