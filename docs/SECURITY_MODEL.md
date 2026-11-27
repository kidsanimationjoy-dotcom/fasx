# FASX Security and Privacy Model

## Principles

1. Explicit host consent: the person at the Windows computer controls pairing and session approval.
2. Visible operation: the companion, capture state, active remote device, and permissions are visible.
3. Least privilege: screen viewing, input control, file transfer, clipboard, diagnostics, audio, peripherals, and power actions are independently granted.
4. Expiring trust: pairing challenges and session approvals are short-lived and revocable.
5. Fail closed: network loss, expired grants, invalid state, or agent failure disables remote input and capture.
6. Minimize data: do not log screen content, typed text, clipboard payloads, or file contents by default.

## Pairing requirements

- Pairing challenge is random, single-use, short-lived, and bound to the expected host device.
- QR codes contain no reusable account password or long-lived bearer secret.
- The Windows user sees the requesting phone/device and approves or denies it.
- A paired device can be removed locally.
- New device pairing always requires fresh local approval.

## Session permissions

A session must explicitly state its requested permissions. The Windows user can approve a smaller subset. A session can never receive more capabilities than it requested and the local user approved.

Separate capabilities:
- View screen.
- Control keyboard/mouse.
- Transfer files.
- Share clipboard.
- Read diagnostics.
- Play remote audio.
- Run power actions.
- Control peripherals.

Sensitive actions may require an additional confirmation at execution time even when the capability is enabled.

## Screen capture

Use the Windows.Graphics.Capture system picker and its visible capture indication. Do not implement hidden capture or attempt to bypass the operating system's capture UI.

## Input control

- Enable only when the current session has the ControlInput grant.
- Display the connected controller identity on Windows.
- Stop input immediately when a grant is revoked or a session becomes unhealthy.
- Do not expose arbitrary command execution through the input channel.

## File access

- Start with an explicit allowlist of user-selected folders.
- Require user confirmation for delete, overwrite, move, or other destructive operations.
- Prevent path traversal, reparse-point escape, and access outside the granted roots.
- Show source, destination, filename, size, and transfer progress.
- Make cancellation available and verify transfer integrity.

## Transport and credentials

- Use a standard authenticated transport (WebRTC DTLS-SRTP and DTLS data channels if WebRTC is selected).
- Never invent a cryptographic protocol.
- Protect Windows secrets with Windows credential/DPAPI facilities and Android secrets with Android Keystore-backed facilities.
- Do not store API keys, relay credentials, private keys, or local secrets in the repository.
- Keep TURN credentials short-lived and scoped.

## Audit and transparency

Record session creation/end, device identity, granted permission categories, approval/revocation events, file-operation metadata, and power-action confirmations. Do not record raw keystrokes, screen captures, or clipboard contents by default.

## Emergency stop

The Windows companion must always expose a local way to terminate the active session. Remote disconnect from the phone is also available. Termination revokes session-scoped keys and closes input, media, and data channels.

## Threats to test

- QR replay and pairing race conditions.
- Stolen paired phone.
- Unauthorized permission escalation.
- Stale session token reuse.
- Signaling server compromise.
- TURN credential leakage.
- Malformed file paths and symlinks/reparse points.
- Interrupted file transfers and duplicate operations.
- Reconnect after local approval expires.
- Local user logs out, locks the workstation, or switches Windows session.
