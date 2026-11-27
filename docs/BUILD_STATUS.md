# FASX Build Status

## Verified foundation build

- Commit: `5089c40d9999a04467ca6ecc0bc4b0471b9a3698`
- Branch: `production/foundation`
- Date: 2026-11-27
- Windows CI: passed (restore, build, tests, self-contained win-x64 publish, artifact upload).
- Android CI: passed (Gradle setup, debug APK assembly, artifact upload).

## Artifacts

- Windows: `FASX-Windows-Foundation-win-x64`
- Android: `FASX-Android-Foundation-debug`

The workflow artifacts are temporary GitHub Actions artifacts and expire according to the expiry shown by GitHub. They are development preview builds, not production releases.

## What can be tested now

### Windows
- Launch the visible FASX Windows companion.
- Inspect read-only local PC Health fields: Windows version, processor count, physical memory, system-drive capacity/free space, battery state and uptime.
- Refresh local health values.

### Android
- Open the Home, Devices and Security tabs.
- Review the SmartDeck module layout.
- Open the pairing information dialog.
- The interface explicitly reports that no computer is paired and no remote action is sent.

## Not implemented yet

- QR pairing in the actual apps.
- Authenticated signaling or device-key proof.
- Durable device registry.
- Phone-to-Windows connection.
- Live screen streaming.
- Remote mouse/keyboard.
- File transfer.
- Remote power actions or audio.

The Windows and Android builds are successfully compiled foundation previews. No physical-device end-to-end test has yet been completed.
