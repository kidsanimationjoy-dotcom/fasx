# FASX Production Roadmap

## Milestone 0 — Repository and Windows foundation
**Status: source foundation and Windows CI build/test/publish validated.**
- [x] Product, architecture, protocol, security, and competitor baseline.
- [x] Visible Windows companion shell.
- [x] Session permission domain model and tests.
- [x] Protected Windows store for paired public-device identities using DPAPI.
- [x] Windows CI workflow, Release build, unit tests, and self-contained x64 publish.

## Milestone 1 — Same-LAN pairing
**Status: implementation and CI package build complete; physical-device validation pending.**
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
- [x] GitHub Actions Windows Release build, unit tests, self-contained artifact upload.
- [x] GitHub Actions Android debug APK assembly and artifact upload.
- [ ] Real Windows + Android same-network test, including separately approved input.
- [ ] Windows Defender Firewall behaviour reviewed on the target setup.
- [ ] Signed installer/package for the host and signed release package for Android.

## Milestone 2 — First real remote session
**Status: view-only WebRTC source implementation and CI package builds complete; physical-device validation pending.**
- [x] WebView2 screen capture through `getDisplayMedia()` and the Windows-owned picker.
- [x] Separate session request signed by the paired Android device key.
- [x] Windows-local approval for `ViewScreen` only, with a 10-minute maximum lease.
- [x] Token-protected offer/answer signaling over a pinned temporary HTTPS endpoint.
- [x] Android WebView receiver page using a receive-only video transceiver.
- [x] Windows WebView2 sender page; screen sharing begins only after a local Start click and picker selection.
- [x] Local Stop and permission revocation on capture end, explicit stop, session expiry or phone disconnect.
- [x] Latest Windows and Android GitHub Actions build/package workflows passed on the current branch head.
- [ ] Real-device same-Wi-Fi pairing and screen-stream test on the target Windows laptop and Android phone.
- [ ] Validate candidate gathering and video quality across supported Wi-Fi routers.
- [ ] Test app backgrounding, phone sleep, Wi-Fi drop and fast reconnect.
- [ ] Document frame rate, resolution, thermal and network usage on target devices.
- [ ] Add TURN relay only in a later internet-access milestone after security, hosting and cost review.

## Milestone 3 — Input and connection quality
**Status: limited input-control source implementation added; CI and physical-device validation pending.**
- [x] Optional `ControlInput` request, disabled by default.
- [x] Separate Windows-local checkbox approval, independent of `ViewScreen` approval.
- [x] WebRTC data channel created only when the Windows owner grants `ControlInput`.
- [x] Pointer move, left/right click, bounded scroll, allowlisted navigation keys, and short printable text entry.
- [x] Windows-side permission check and strict typed input validation on every event.
- [x] Unit tests for pointer bounds, scroll bounds, key allowlist, and text bounds.
- [ ] Build and test on Windows/Android CI for this feature branch.
- [ ] Physical-device input tests on the target Windows laptop and Android phone.
- [ ] Verify pointer mapping on primary monitor, rotation, scaling, and supported screen dimensions.
- [ ] Test immediate input revocation, disconnect and expiry.
- [ ] Connection Guardian: latency, stream state, and reconnect behaviour.

## Milestone 4 — File Bridge and device health
**Status: local read-only PC Health foundation added; file transfer and richer health intelligence remain.**
- [x] Local PC Health window with Windows version, logical processor count, physical memory, system-drive free/total capacity, .NET runtime and uptime.
- [x] Diagnostics refresh only on opening the panel or pressing Refresh; no telemetry upload.
- [ ] User-selected folder roots.
- [ ] Two-way file transfer with integrity checks and cancellation.
- [ ] Battery, disk health, CPU utilization and additional hardware information.
- [ ] Health summaries with read-only default behaviour.

## Milestone 5 — SmartDeck and Context Control
**Status: Android local SmartDeck customization foundation added; app-aware context and sharing remain.**
- [x] Show/hide built-in SmartDeck cards.
- [x] Reorder cards with up/down controls.
- [x] Save the layout locally on the Android phone.
- [x] Label planned modules clearly and prevent them from appearing as working capabilities.
- [ ] User-created custom cards with typed action selection.
- [ ] Application-aware panels (opt-in).
- [ ] Deck export/import with validation.
- [ ] Deck Gallery templates.

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
