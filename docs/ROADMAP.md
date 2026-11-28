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
**Status: limited input-control and live local quality metrics implemented; latest Windows/Android CI passed on branch commit `b26c815ec5f808f68cbcd87f93e7d919aba3a29e`; physical-device validation pending.**
- [x] Optional `ControlInput` request, disabled by default.
- [x] Separate Windows-local checkbox approval, independent of `ViewScreen` approval.
- [x] WebRTC data channel created only when the Windows owner grants `ControlInput`.
- [x] Pointer move, left/right click, bounded scroll, allowlisted navigation keys, and short printable text entry.
- [x] Windows-side permission check and strict typed input validation on every event.
- [x] Unit tests for pointer bounds, scroll bounds, key allowlist, and text bounds.
- [x] Windows Release build, unit tests, self-contained publish and artifact upload.
- [x] Android debug APK assembly and artifact upload.
- [ ] Physical-device input tests on the target Windows laptop and Android phone.
- [ ] Verify pointer mapping on primary monitor, rotation, scaling, and supported screen dimensions.
- [ ] Test immediate input revocation, disconnect and expiry.
- [x] Connection Guardian displays stream resolution, approximate decoded FPS, received bitrate and WebRTC RTT locally in the Android viewer.
- [ ] Connection Guardian: reconnect behaviour, recovery state and network history.

## Milestone 4 — File Bridge and device health
**Status: local PC Health and phone-to-PC File Bridge v1 source added; latest Windows/Android CI passed on branch commit `b26c815ec5f808f68cbcd87f93e7d919aba3a29e`; physical-device transfer test pending.**
- [x] Local PC Health window with Windows version, logical processor count, physical memory, system-drive free/total capacity, .NET runtime and uptime.
- [x] Diagnostics refresh only on opening the panel or pressing Refresh; no telemetry upload.
- [x] Windows owner selects one destination folder for a session; phone cannot choose or change the host path.
- [x] Phone user selects individual files through Android's system document picker.
- [x] Phone-to-PC upload endpoint with a 20 MB per-file limit, content-length enforcement, SHA-256 integrity verification, safe filename validation, unique no-overwrite save, cancellation cleanup, rate limiting and permission rechecks during upload.
- [x] Windows Release build, unit tests, self-contained publish and artifact upload with File Bridge source.
- [x] Android debug APK assembly and artifact upload with File Bridge UI and upload client.
- [ ] Physical-device transfer test between target Android phone and Windows laptop.
- [ ] Windows-to-phone transfers and two-way folder browsing.
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
**Status: local Windows Flows v1 implemented; Windows Release build/tests/publish and Android debug APK assembly passed on commit `a59722d49cccae7b7e3927d189b0e286ae998979`; physical-device tests for remote features remain pending.**
- [x] Local Windows flow builder with registered typed actions.
- [x] Local JSON storage with limits of 20 flows, 8 steps per flow and 64 KB total.
- [x] Open a fixed allowlist of built-in apps, open HTTPS websites, and choose/open a folder at run time.
- [x] Preview and explicit Windows confirmation before every run.
- [x] Unit tests reject arbitrary executables, scripts, shell commands, non-HTTPS websites and invalid steps.
- [ ] Edit saved flows and add run history.
- [ ] Import/export with strict schema validation.
- [ ] Remote flow execution from the phone only after a separate permission and local approval design.
- [ ] Explainable system-health assistant and AI diagnostics.
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
