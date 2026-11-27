# FASX Pairing Flow

## Status

The pairing domain coordinator and Windows-side pairing service are implemented with unit tests. The Windows companion also creates a stable P-256 device identity protected with DPAPI for the current Windows account. Approved controller public keys can be stored in a DPAPI-protected trusted-device registry and remain available after the coordinator and application are restarted.

This is not yet connected pairing: HTTP endpoints, QR rendering/scanning, Android key enrollment, and a live phone-to-Windows connection are not implemented. The pairing service is currently an in-process application service with no network listener.

## Intended journey

1. The Windows owner opens FASX and chooses Pair a phone.
2. FASX creates a cryptographically random 256-bit, one-time secret with a short expiry (default two minutes, maximum five minutes).
3. The app presents a QR code containing a versioned FASX pairing URI and signaling endpoint. The QR must never contain an account password or long-lived device credentials.
4. Android creates or loads a P-256 device key in Android Keystore and signs the canonical proof payload using ECDSA with SHA-256.
5. Android submits the invitation secret, device ID, display name, public key and signature.
6. Windows validates the key and proof, then displays the requesting device identity.
7. The local Windows user approves or denies.
8. The Windows pairing service persists the approved phone's public key and metadata in the DPAPI-protected trusted-device registry.
9. Approval establishes device trust only. Every later remote session still requires its own capability approval.
10. The owner can revoke a paired phone; revocation is persisted.

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

## Implemented domain and persistence rules

- Offer secret has 256 bits of random entropy.
- Only the secret hash is kept in the coordinator.
- Secret comparison is constant-time.
- Public keys must parse as P-256 SubjectPublicKeyInfo.
- Requesting device must prove possession of its private key.
- Invalid proofs do not consume the invitation.
- The first valid request consumes the secret; reuse, denial or expiry is rejected.
- Only the host that created the invitation can approve or deny it.
- Display names and signature lengths are validated.
- Windows host private key is protected with DPAPI CurrentUser; damaged identity data is not silently replaced.
- Approved controller public keys and device metadata are stored in a DPAPI CurrentUser-protected registry.
- Trusted-device records are scoped by host ID, validated on load, and can be revoked persistently.
- The Windows pairing service rolls back its in-memory trusted-device entry if persistence fails.
- Automated tests cover identity persistence/signing, damaged storage, proof verification, replay, expiry, approval, denial, persistence across service restart, and revocation.

## Production gaps

- Add authenticated HTTPS signaling endpoints.
- Add Android Keystore-backed P-256 identity and enrollment.
- Add Windows QR rendering and Android QR scanning.
- Add physical phone-to-Windows pairing tests.
- Rate-limit pairing attempts.
- Ensure the backend cannot approve a device on behalf of the Windows user.
- Keep session approval separate from pairing.
