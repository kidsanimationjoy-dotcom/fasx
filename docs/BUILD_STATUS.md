# FASX Build Status

## Verified foundation build

- Code commit: `5d6ccee6d0e7516df45e5e2718831f62c98753c4`
- Branch: `production/foundation`
- Date: 2026-11-27
- Windows CI: passed (restore, build, unit tests, self-contained win-x64 publish, artifact upload).
- Android CI: passed (Gradle setup, debug APK assembly, artifact upload).

Workflow runs:
- Windows: https://github.com/kidsanimationjoy-dotcom/fasx/actions/runs/46053683297
- Android: https://github.com/kidsanimationjoy-dotcom/fasx/actions/runs/46053678041

## Artifacts

- Windows: `FASX-Windows-Foundation-win-x64`
- Android: `FASX-Android-Foundation-debug`

These are development preview builds, not production releases. GitHub Actions artifacts are temporary and expire according to the expiry shown by GitHub.

## What can be tested now

### Windows
- Launch the visible FASX Windows companion.
- Inspect read-only local PC Health fields: Windows version, processor count, physical memory, system-drive capacity/free space, battery state and uptime.
- Refresh local health values.

### Android
- Open the Home, Devices and Security tabs.
- Select one of the five SmartDeck experience profiles.
- Open Customize to show/hide cards and move them up/down.
- Close and reopen the app to verify that the selected profile and layout remain saved locally.
- Open the pairing information dialog. It clearly reports that pairing is not connected yet.

## Not implemented yet

- QR pairing in the actual apps.
- Authenticated signaling or device-key proof.
- Durable trusted-device registry.
- Phone-to-Windows connection.
- Live screen streaming.
- Remote mouse/keyboard.
- File transfer.
- Remote power actions or audio.
- Physical-device end-to-end test.

The Windows and Android builds are successfully compiled foundation previews. Passing CI does not mean the remote-control system is complete.
