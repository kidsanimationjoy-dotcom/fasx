# FASX

**Your computer. Your command centre.**

FASX is an all-in-one personal computer command centre combining secure remote desktop, device management, customizable controls, and user-approved automation.

> **Project status: Foundation / early development.** The current build contains the initial Windows companion shell and permission-domain model. Remote streaming, pairing, file transfer, and phone connectivity are not implemented yet.

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

## First target platform

- **Host:** Windows 10/11 desktop companion.
- **Controller:** Android phone client.
- **Future:** Additional desktop and mobile platforms after the Windows/Android experience is stable.

## Repository layout

- `src/Fasx.Agent.Core` — platform-independent session and permission rules.
- `src/Fasx.Agent.Windows` — visible Windows companion shell.
- `apps/android` — Android client plan and implementation entry point.
- `services/signaling` — signaling/control-plane plan.
- `tests/Fasx.Agent.Core.Tests` — tests for shared security/domain rules.
- `docs` — product, architecture, security, protocol, research, and roadmap.

## Build the Windows foundation

Requirements: Windows, Visual Studio 2026 or the .NET 10 SDK.

```powershell
dotnet restore FASX.sln
dotnet build FASX.sln -c Release
dotnet test FASX.sln -c Release
```

The current companion is an early visible shell. It does not yet establish remote connections or capture/control the desktop.

## Architecture direction

- Windows companion: C# / .NET 10 LTS / WPF for the first desktop shell.
- Android client: Kotlin and Jetpack Compose.
- Control plane: ASP.NET Core for pairing, signaling, and device presence.
- Media and low-latency input: evaluate WebRTC; use established WebRTC security rather than inventing cryptography.
- Direct peer-to-peer connection where possible, with an authenticated TURN relay fallback.
- Shared contracts and permission rules live in `Fasx.Agent.Core` initially and will be extracted into a versioned protocol package when the mobile client is added.

The selected stack and trade-offs are recorded in [ADR 0001](docs/decisions/0001-platform-foundation.md).

## Non-negotiable safety principles

- Remote access is visible to the person at the Windows computer.
- Pairing requires deliberate local approval.
- Each session receives only the permissions approved for that session.
- Screen capture uses the Windows system capture picker and its visible capture indicator.
- File access is limited to approved locations and explicit operations.
- No hidden access, stealth persistence, credential harvesting, or arbitrary remote shell.
- Sensitive actions such as shutdown, restart, and file deletion require clear confirmation.
- The local user can end a session immediately.

Read [the security model](docs/SECURITY_MODEL.md) before implementing any remote-control capability.

## Current milestone

See [the roadmap](docs/ROADMAP.md). The first milestone is the visible Windows companion, testable permission rules, and the development pipeline. Pairing and real screen streaming are subsequent milestones and must not be represented as working until tested end-to-end.
