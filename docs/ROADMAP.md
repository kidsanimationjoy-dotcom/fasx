# FASX Production Roadmap

## Milestone 0 — Repository and Windows foundation
**Status: implemented in production/foundation; CI verification pending.**
- Product, architecture, protocol, security, and competitor baseline documented.
- Visible Windows companion shell created.
- Shared session permission model and tests created.
- Protected Windows store for paired public-device identities created using DPAPI.
- Windows CI workflow added.

## Milestone 1 — Pairing and local consent
**Status: pairing domain + Android QR/key foundations implemented; network integration remains open.**
- [x] Short-lived pairing invitation model.
- [x] Single-use claim state machine.
- [x] Phone device-key proof verification (P-256 ECDSA / SHA-256).
- [x] Local-only approve/deny domain operations.
- [x] Android QR scanning and invitation validation.
- [x] Android Keystore device signing key generation.
- [x] Windows DPAPI persistence for trusted public device identities.
- [x] Unit tests for proof validation, one-time use, expiry, denial, revocation and concurrent claims.
- [ ] QR invitation creation/rendering in the Windows UI.
- [ ] TLS-protected LAN host endpoint and certificate pin generation.
- [ ] Android claim submission and status polling.
- [ ] Windows pending-device approval screen.
- [ ] End-to-end test using a real Windows host and Android phone.

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
