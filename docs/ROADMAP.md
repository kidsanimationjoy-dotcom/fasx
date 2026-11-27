# FASX Production Roadmap

## Milestone 0 — Repository and client foundations
**Status: committed to production/foundation; CI validation pending.**
- Product and technical baseline documented.
- Visible Windows companion shell created.
- Shared session-permission model and tests created.
- In-memory pairing ceremony domain and tests created.
- Android Compose foundation UI created.
- Windows and Android build workflows added.

The Windows and Android screens are foundation previews only. They do not yet pair devices, stream a screen, transfer files, or accept remote input.

## Milestone 1 — Secure device pairing
- Windows-protected host identity and phone key storage.
- Short-lived single-use QR pairing.
- Authenticated HTTPS signaling endpoints.
- Host-side local approval screen and device details.
- Paired-device list, revocation and audit history.
- Android QR scanning and key enrollment.
- Persist device trust safely and prove device-key possession.

## Milestone 2 — First real remote session
- WebRTC proof of concept.
- Windows system capture picker integration.
- Android session viewer.
- Screen viewing first, before remote input.
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
