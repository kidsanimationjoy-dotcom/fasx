# FASX Production Roadmap

## Milestone 0 — Repository and Windows foundation
**Status: foundation branch created; initial commit pending.**
- Product and technical baseline documented.
- Windows companion WPF shell created.
- Shared session permission model created.
- Unit tests for permission boundaries added.
- Windows CI workflow added.

## Milestone 1 — Pairing and local consent
- Device identity and secure local storage.
- Short-lived single-use QR pairing.
- Local host approval screen.
- Paired-device list and revoke action.
- Session permission chooser.
- Audit events for pairing and approval.

## Milestone 2 — First real remote session
- WebRTC proof of concept.
- Windows system capture picker integration.
- Android session UI.
- Screen view only, before remote input.
- Clear active-session indicator and local stop.
- Direct LAN test plus relay fallback test.

## Milestone 3 — Input and connection quality
- Remote touchpad and keyboard under separate permission.
- Input mapping tests.
- Connection Guardian: latency, stream state, reconnect behavior.
- Permission revocation and fail-closed testing.

## Milestone 4 — File Bridge and device health
- User-selected folder roots.
- Two-way file transfer with integrity checks and cancellation.
- CPU, memory, disk, battery, and OS information.
- Health summaries with read-only default behavior.

## Milestone 5 — SmartDeck and Context Control
- Customizable control cards.
- Application-aware panels (opt-in).
- Deck export/import with validation.
- Deck Gallery templates.

## Milestone 6 — FASX Flows and Intelligence
- Visual workflow builder based on registered typed actions.
- Explicit confirmation gates and run history.
- Explainable system-health assistant.
- No arbitrary scripts or hidden commands.

## Milestone 7 — Audio, advanced devices, and business
- Remote audio.
- Multi-monitor support.
- Peripheral/printing experiments.
- Business device groups, roles, and audit export.

## Release gates

A milestone is not complete because code exists. It must have:
- Build passing on supported CI.
- Unit tests for domain/security logic.
- End-to-end tests on a real Windows host and Android device where relevant.
- Permission and revocation tests.
- Network interruption/reconnection tests.
- Clear known limitations and user instructions.
