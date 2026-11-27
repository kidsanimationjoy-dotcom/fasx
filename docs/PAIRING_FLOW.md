# FASX Pairing Flow — Version 1

## Status

The pairing state machine, device-key proof verification, Android invitation parser, Android device-key generation, and Windows protected storage for trusted public identities are implemented. The host/phone network exchange, QR invitation generation on Windows, and a real end-to-end pairing test are not implemented yet.

## Intended flow

1. The user opens the visible FASX Windows companion and chooses **Pair a device**.
2. FASX creates a random 256-bit secret and a pairing invitation that expires after two minutes (bounded to 30 seconds–5 minutes).
3. The Android app creates a P-256 ECDSA device key pair. The private key stays in Android Keystore.
4. The phone scans the invitation and submits its device ID, display name, public key, a random 256-bit nonce, and a signature.
5. FASX verifies the signature and the invitation secret. The invitation is consumed by the first valid claim.
6. The Windows companion shows the device name and ID and asks the local user to approve or deny.
7. Approval stores the public device identity using Windows DPAPI for the current Windows user; denial ends the request.
8. Future sessions still require separate per-session permission approval. Pairing is not permission to control the computer.

## QR payload

The future QR JSON envelope is:

```json
{
  "v": 1,
  "kind": "fasx-pair",
  "pairingId": "uuid",
  "secret": "base64url-256-bit-secret",
  "endpoint": "https://host.example:port/api/pairing/claim",
  "tlsPin": "sha256/base64-sha256-certificate-pin",
  "expiresAtUtc": "2026-11-27T12:00:00.0000000Z"
}
```

The Android parser accepts HTTPS endpoints only, validates the certificate pin shape, checks the invitation expiry, and rejects unsupported QR formats.

## Proof message

The phone signs UTF-8 bytes of the exact newline-separated string:

```text
FASX-PAIRING-V1
{pairingId in lowercase D format}
{deviceId in lowercase D format}
{proofNonce in Base64URL}
```

Use ECDSA P-256 with SHA-256 and RFC 3279 DER sequence signature encoding.

The public key is SubjectPublicKeyInfo DER encoded, then Base64URL encoded. The nonce is 32 cryptographically random bytes encoded as Base64URL.

## Security boundaries

- The 256-bit invitation secret is returned only when the host creates the invitation; persist only its SHA-256 hash.
- Device signature proves key possession. It does not prove that a displayed device name is genuine.
- Host approval is a separate local operation. A phone/network endpoint must never call the local approval method.
- Pairing expires. Rejected, expired, or consumed invitations cannot be reused.
- Pairing does not grant screen, keyboard, file, clipboard, or power capabilities.
- A future network implementation must use authenticated TLS, protect the Android private key in Keystore, persist host state securely, rate-limit failed claims, and include certificate/pinning or another trustworthy host-identity check for direct LAN pairing.

## Current implementation limitation

Pairing invitations and pending claims are currently process-memory only. Trusted paired public-device identities are persisted using Windows DPAPI for the current Windows user. The code is useful for validating the pairing flow, but not yet a complete network pairing implementation. QR generation on Windows, TLS-protected LAN transport, Android claim submission, and end-to-end tests are still required.
