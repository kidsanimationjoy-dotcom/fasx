# ADR 0001 — FASX Platform Foundation

- Status: Accepted for the first implementation milestone; revisit after proof-of-concept.
- Date: 2026-11-27.

## Context

FASX needs a Windows host companion, Android controller, signaling/control plane, and a secure low-latency transport. The first deliverable must be understandable and testable without committing the product to an unreviewed third-party codebase.

## Decision

- Windows companion and shared domain rules: C# / .NET 10 LTS.
- Initial Windows UI shell: WPF.
- Android client: Kotlin / Jetpack Compose.
- Signaling/control plane: ASP.NET Core.
- Streaming/input transport: evaluate standard WebRTC with ICE and TURN fallback.
- Screen capture: Windows.Graphics.Capture user picker and visible system indication.
- Shared protocol: versioned typed messages and capability-specific permissions.

## Why .NET 10

.NET 10 is an active LTS release supported through November 2028 according to Microsoft's current support policy.

## Why Kotlin / Compose

The first controller target is Android. Compose is Google's modern declarative Android UI toolkit and supports reusable, state-driven controls suitable for SmartDeck.

## Why WebRTC is a candidate

WebRTC provides standardized secure media and data transport. The project will validate latency, NAT traversal, Android/Windows interoperability, and TURN cost before locking the implementation.

## RustDesk licensing note

RustDesk is AGPL-3.0. FASX will not copy or embed its code until the team has reviewed the license obligations and distribution model. It remains a valuable technical reference and possible separately evaluated integration.

## Consequences

- Two native client stacks are maintained.
- .NET core rules can be tested independently from WPF.
- Android protocol implementation must follow versioned contracts.
- A WebRTC prototype and network test matrix are required before productionizing media.
- This ADR does not claim that remote sessions are already implemented.
