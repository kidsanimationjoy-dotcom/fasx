# FASX Build Status

## Last verified client foundation

- Code commit: 5d6ccee6d0e7516df45e5e2718831f62c98753c4
- Windows CI: passed (restore, build, unit tests, self-contained win-x64 publish, artifact upload).
- Android CI: passed (Gradle setup, debug APK assembly, artifact upload).

## New Windows pairing-security work

The branch now includes additional Windows identity and pairing code after the last published client artifact:
- DPAPI CurrentUser-protected P-256 host identity.
- In-memory pairing coordinator with P-256 proof-of-possession verification.
- DPAPI-protected trusted-device registry and Windows pairing service.
- New Windows-only unit tests for identity, registry persistence, and pairing-service restart/revocation.

These newest changes are awaiting their own CI run. Do not use the earlier Windows artifact as if it contained the new identity/registry code.

## Current artifacts

The earlier foundation artifacts remain development previews:
- Windows: FASX-Windows-Foundation-win-x64.
- Android: FASX-Android-Foundation-debug.

They do not yet implement a live network pairing or remote desktop connection.

## What can be tested in the earlier foundation build

### Windows
- Visible FASX companion window.
- Read-only local PC Health: Windows version, processor count, memory, system-drive space, battery and uptime.
- Refresh local health values.

### Android
- Home, Devices and Security tabs.
- SmartDeck experience profiles.
- Hide/show cards and reorder cards.
- Layout saved locally on the phone.
- Pairing dialog clearly states that no connection is active.

## Not implemented yet

- HTTP signaling service or authenticated network pairing.
- QR rendering/scanning.
- Android Keystore-backed device identity.
- Phone-to-Windows connection.
- Live screen streaming.
- Remote mouse/keyboard.
- File transfer.
- Remote power actions or audio.
- Physical-device end-to-end test.

CI passing means the code compiled and tests passed on the runner. It does not mean the remote-control system is complete.
