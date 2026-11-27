# FASX Production Roadmap

## Milestone 0 — Repository and client foundations
**Status: completed for the foundation preview.** Windows and Android CI passed on code commit 5d6ccee6d0e7516df45e5e2718831f62c98753c4.
- Product and technical baseline documented.
- Visible Windows companion shell and local read-only PC Health created.
- Shared session-permission model and tests created.
- Android Compose app created with Home, Devices and Security tabs.
- SmartDeck experience profiles added: Everyday, Student, Professional, Creator, and Business & IT.
- SmartDeck card visibility and ordering are customizable and saved on the phone.
- Windows build, unit tests, and portable x64 publishing passed.
- Android debug APK assembly and artifact upload passed.

## Milestone 1 — Secure device pairing
**Status: partial implementation; no network pairing yet.**
- Windows P-256 device identity is created once and protected with DPAPI CurrentUser.
- Identity exposes a local device ID and public key; private key signs SHA-256 ECDSA challenges.
- Damaged or undecryptable identity data fails closed rather than silently replacing the identity.
- In-memory coordinator creates short-lived, single-use pairing invitations.
- Pairing requests must present a valid P-256 public key and proof-of-possession signature.
- Invalid proof does not consume the invitation; valid proof consumes it before local host approval.
- Tests cover identity persistence, signing after reload, corrupt storage, disposal, pairing proof, replay, expiry, host approval, denial and revocation.

Still required:
- Persist trusted phone records.
- Authenticated HTTPS signaling endpoints.
- Android Keystore-backed P-256 identity and QR scanning.
- Windows QR generation and local approval UI.
- End-to-end phone-to-Windows pairing tests.
- Rate limiting and security integration tests.

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
- Expand CPU, memory, disk, battery, and OS information.
- Health summaries with read-only default behavior.

## Milestone 5 — SmartDeck and Context Control
- Add user-created control cards and drag-and-drop editing.
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
