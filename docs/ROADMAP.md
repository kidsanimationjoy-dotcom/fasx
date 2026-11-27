# FASX Production Roadmap

## Milestone 0 — Repository and Windows foundation
**Status: source foundation committed; CI and real-device verification pending.**
- [x] Product, architecture, protocol, security, and competitor baseline.
- [x] Visible Windows companion shell.
- [x] Session permission domain model and tests.
- [x] Protected Windows store for paired public-device identities using DPAPI.
- [x] Windows CI workflow.

## Milestone 1 — Same-LAN pairing
**Status: implementation committed to the pairing development branch; validation pending.**
- [x] Short-lived pairing invitation model.
- [x] Single-use claim state machine.
- [x] P-256 ECDSA device proof verification.
- [x] Local-only approve/deny/cancel operations.
- [x] Android QR scanning and invitation validation.
- [x] Android Keystore device signing key generation.
- [x] Windows DPAPI persistence for trusted public device identities.
- [x] Temporary HTTPS listener bound to one RFC1918 IPv4 interface.
- [x] QR invitation includes a certificate SPKI pin and expiry.
- [x] Android pinned HTTPS submission and status polling.
- [x] Windows pending-device approval UI.
- [x] Tests for proof validation, one-time use, expiry, denial, revocation, cancellation, persistence seam, and concurrent claims.
- [ ] CI build pass recorded for Windows and Android.
- [ ] Real Windows + Android same-network test.
- [ ] Windows Defender Firewall behaviour reviewed and documented.
- [ ] Installer/package for the host and debug/release package for Android.

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
