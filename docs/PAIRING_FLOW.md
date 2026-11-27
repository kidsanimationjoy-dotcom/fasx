# FASX Pairing Flow

## Status

The pairing coordinator is an in-memory domain service with unit tests. The Windows companion creates and persists a stable P-256 device identity protected by DPAPI for the current Windows account. The pairing coordinator verifies proof-of-possession signatures from a requesting device.

This is not yet connected pairing: HTTP endpoints, QR rendering/scanning, Android key enrollment, durable trusted-device records, and a live phone-to-Windows link are not implemented.

## Intended journey

1. The Windows owner opens FASX and chooses Pair a phone.
2. FASX creates a cryptographically random 256-bit, one-time secret with a short expiry (default two minutes, maximum five minutes).
3. The app presents a QR code containing a versioned FASX pairing URI and signaling endpoint. The QR must never contain an account password or long-lived device credentials.
4. Android creates or loads a P-256 device key in Android Keystore and signs the canonical proof payload using ECDSA with SHA-256.
5. Android submits the invitation secret, device ID, display name, public key and signature.
6. Windows validates the key and proof, then displays the requesting device identity.
7. The local Windows user approves or denies.
8. Approval establishes device trust only. Every later remote session still requires its own capability approval.
9. The owner can revoke a paired phone.

## Canonical proof-of-possession payload

Both clients sign these exact UTF-8 bytes, using LF line separators and no trailing newline:

FASX-PAIRING-REQUEST-V1
{pairingId:D lower-case}
{deviceId:D lower-case}
{trimmed display name}
{base64url invitation secret}

- IDs use lowercase, hyphenated GUID format.
- Display name is trimmed, 1–64 characters, and has no control characters.
- Invitation secret is exactly 32 bytes, re-encoded as unpadded base64url.
- Phone signs with ECDSA P-256 and SHA-256.
- Signature is standard Base64 containing a DER-encoded ECDSA signature.
- Public key is standard Base64-encoded X.509 SubjectPublicKeyInfo.
- Windows checks the P-256 key and signature before consuming the invitation.
- A failed proof does not consume the invitation. A valid request consumes it before host approval.

The shared .NET implementation is PairingProof.CreatePayload.

## Implemented domain rules

- Invitation secret has 256 bits of random entropy.
- Only the secret hash is retained by the coordinator.
- Secret comparison is constant-time.
- Public keys must parse as P-256 SubjectPublicKeyInfo.
- Requesting device must prove possession of its private key.
- Invalid proofs do not consume the invitation.
- The first valid request consumes the secret; reuse, denial or expiry is rejected.
- Only the host that created the invitation can approve or deny it.
- Display names and signature lengths are validated.
- Trusted devices are scoped to their host in the in-memory coordinator.
- Windows host private key is protected with DPAPI CurrentUser. Damaged identity data is not silently replaced.
- Automated tests cover replay, expiry, host approval, denial, revocation, invalid keys, invalid proofs and identity persistence.

## Production gaps

- Persist trusted phone records in protected storage.
- Add authenticated HTTPS signaling endpoints.
- Add Android Keystore-backed P-256 identity and enrollment.
- Add Windows QR rendering and Android QR scanning.
- Add physical phone-to-Windows pairing tests.
- Rate-limit pairing attempts.
- Ensure the backend cannot approve a device on behalf of the Windows user.
- Keep session approval separate from pairing.
