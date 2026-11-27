# FASX Protocol Contract (Draft)

This is a draft contract for implementation planning. It is not a deployed protocol.

## Message envelope

All control messages should use a versioned envelope:

```json
{
  "protocolVersion": 1,
  "messageId": "uuid",
  "sessionId": "uuid",
  "sentAtUtc": "2026-01-01T00:00:00Z",
  "type": "session.permission.request",
  "payload": {}
}
```

Requirements:
- Reject unknown protocol versions.
- Validate message size and payload schema.
- Bind every message to an authenticated device and session.
- Reject expired, replayed, or out-of-order state-changing messages where applicable.
- Never accept a permission grant from the remote controller alone.

## Pairing lifecycle

`created -> scanned -> awaiting_local_approval -> approved | denied | expired -> paired`

Pairing challenges are single-use and short-lived. Local approval binds the phone's public device identity to the Windows host.

## Session lifecycle

`requested -> awaiting_host_approval -> connecting -> active -> reconnecting -> ended | denied | expired | failed`

A reconnect is not permission renewal. If a grant expires or is revoked, the session must request new local approval.

## Permission vocabulary

- `screen.view`
- `input.control`
- `files.transfer`
- `clipboard.share`
- `diagnostics.read`
- `audio.play`
- `power.execute`
- `peripherals.control`

## Initial typed action model

FASX Flows and SmartDeck cards call registered action identifiers such as:
- `app.open_allowlisted`
- `presentation.next_slide`
- `media.play_pause`
- `file.transfer_request`
- `diagnostics.read_summary`

Every action declares required permissions, input schema, confirmation policy, timeout, and audit category. No free-form shell, script, or executable payload is accepted.
