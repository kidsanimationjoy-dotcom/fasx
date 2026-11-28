# FASX Security and Privacy Model

## Principles

1. Explicit host consent: the person at the Windows computer controls pairing and each remote session.
2. Visible operation: companion, active listener, capture state, active remote device, and permissions are visible.
3. Least privilege: screen viewing, input control, file transfer, clipboard, diagnostics, audio, peripherals, and power actions are independently granted.
4. Expiring trust: pairing challenges and session approvals are short-lived and revocable.
5. Fail closed: network loss, expired grants, invalid state, or agent failure disables remote input and capture.
6. Minimize data: do not log screen content, typed text, clipboard payloads, or file contents by default.

## Pairing

- Pairing invitation uses 256 bits of cryptographic randomness and has a bounded lifetime.
- QR contains a one-time secret, pairing ID, HTTPS endpoint, expiry, and public-key SPKI pin.
- Windows listener is started only after an explicit local button press.
- Listener binds only one selected RFC1918 private IPv4 address, not all network interfaces.
- Android validates endpoint, route, port, TLS pin, secret format, and expiry before sending.
- Android uses an ECDSA P-256 key in Android Keystore to prove device-key possession.
- Windows stores only approved public-device identity records using DPAPI for the current Windows user.
- Local approval/denial/cancellation is never exposed as a network API.
- Pairing is not permission to view or control the screen.
- Each view-only session uses a separate signed challenge from the paired phone.
- Windows verifies the paired public key and creates a ViewScreen request, optionally including ControlInput only when the Windows owner selected that option before generating the invitation.
- The user must locally approve the session before any WebRTC offer is returned.
- A short session token is required for status, offer, and answer signaling.
- A ViewScreen approval has a separate, bounded 10-minute maximum lease.

## Session permissions

Each session explicitly requests capabilities. Windows user can approve a smaller subset. A remote peer cannot widen the grant.

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

## Screen capture and input

- Use the WebView2 Screen Capture API and Windows system picker with a visible capture indication.
- Only a user-selected video track is sent. A data channel is created only when the Windows owner requested and locally approved ControlInput.
- Stop capture and revoke the view permission on local stop, capture end, lease expiry, or peer disconnect.
- Do not implement hidden capture or bypass the operating system's capture UI.
- Input is enabled only with a current ControlInput grant, separate from ViewScreen.
- The phone's pointer/keyboard panel is hidden until the approved data channel is open.
- Every received message is validated again by the Windows host against a strict typed allowlist.
- Current allowlist: normalized pointer movement, left/right click, bounded wheel events, a small navigation-key set, and short printable text entry.
- Reject system hotkeys, arbitrary key combinations, shell or script requests, and commands outside the allowlist.
- Check ControlInput authorization for every message and drop input immediately when the grant is revoked or session expires.
- Keep the Windows Stop session button visible while the session is active.

## File access

- Start with explicit allowlist of user-selected folders.
- Require confirmation for delete, overwrite, move, or other destructive operations.
- Prevent path traversal and reparse-point escape.
- Show source, destination, filename, size, and transfer progress.
- Make cancellation available and verify transfer integrity.

## Transport and credentials

- Use standard WebRTC transport security (DTLS-SRTP) for screen media; do not invent media encryption.
- The current same-LAN prototype uses browser-native RTCPeerConnection and no public STUN/TURN servers. The optional input data channel uses the negotiated WebRTC peer connection and its standard transport security.
- Authenticate the HTTPS signaling request using the QR-pinned host certificate and a one-time session token.
- Never invent a cryptographic protocol.
- Protect Windows secrets with DPAPI/Windows credential facilities and Android secrets with Android Keystore.
- Do not store API keys, relay credentials, private keys, or secrets in repository.
- Keep TURN credentials short-lived and scoped.

## Audit and transparency

Record session creation/end, device identity, permission categories, approval/revocation events, file-operation metadata, and power-action confirmations. Do not record raw keystrokes, screen captures, or clipboard contents by default.

## Emergency stop

Windows companion must always expose a local way to terminate the active session. Remote disconnect from phone is also available. Termination revokes session-scoped keys and closes input, media, and data channels.

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
- Local user logs out, locks workstation, or switches Windows session.
- Local-LAN hostile peer, certificate substitution, and stale QR reuse.
