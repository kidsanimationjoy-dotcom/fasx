# FASX Protocol Contract (Draft)

This is a draft contract for implementation planning. It is not a deployed protocol.

## Message envelope

All control messages use a versioned envelope containing protocolVersion, messageId, sessionId, sentAtUtc, type, and payload.

Requirements:
- Reject unknown protocol versions.
- Validate message size and payload schema.
- Bind every message to an authenticated device and session.
- Reject expired, replayed, or out-of-order state-changing messages where applicable.
- Never accept a permission grant from the remote controller alone.

## Device identity

- Windows uses a stable P-256 signing key protected by DPAPI CurrentUser.
- Android will use a P-256 signing key protected by Android Keystore.
- Public keys are exchanged as Base64-encoded X.509 SubjectPublicKeyInfo.
- Private keys never leave their platform keystore/protected store.
- Device identity proves key possession; it does not grant a remote session.

## Pairing lifecycle

created -> scanned -> awaiting_local_approval -> approved | denied | expired -> paired

Pairing challenges are single-use and short-lived. Local approval binds the phone's public device identity to the Windows host.

### Proof-of-possession format

The controller signs the exact UTF-8 bytes of five lines, joined with LF and without a trailing newline:

FASX-PAIRING-REQUEST-V1
{pairingId:D lower-case}
{deviceId:D lower-case}
{trimmed display name}
{base64url invitation secret}

The invitation secret is 32 random bytes encoded as unpadded base64url. The controller signs using ECDSA P-256 with SHA-256. The DER signature is transferred as standard Base64; the public key is standard Base64 SPKI.

The Windows host validates the public key's P-256 curve and verifies the signature before consuming the one-time invitation secret. Failed proof does not consume the invitation. The host still requires local approval after successful proof.

The shared .NET implementation is PairingProof.CreatePayload. Android must reproduce the canonical bytes exactly and should pass cross-platform test vectors before connecting to the signaling service.

## Session lifecycle

requested -> awaiting_host_approval -> connecting -> active -> reconnecting -> ended | denied | expired | failed

A reconnect is not permission renewal. If a grant expires or is revoked, the session must request new local approval.

## Permission vocabulary

- screen.view
- input.control
- files.transfer
- clipboard.share
- diagnostics.read
- audio.play
- power.execute
- peripherals.control

## Initial typed action model

FASX Flows and SmartDeck cards call registered action identifiers such as app.open_allowlisted, presentation.next_slide, media.play_pause, file.transfer_request, and diagnostics.read_summary.

Every action declares required permissions, input schema, confirmation policy, timeout, and audit category. No free-form shell, script, or executable payload is accepted.
