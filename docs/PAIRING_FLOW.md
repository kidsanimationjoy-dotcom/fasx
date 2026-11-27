# FASX Pairing Flow

## Status

The first pairing coordinator is implemented as an in-memory domain service with unit tests. It does not yet expose HTTP endpoints, generate a rendered QR image, persist trusted devices, or connect to an Android client.

## Intended user journey

1. The Windows owner opens FASX and chooses **Pair a phone**.
2. FASX creates a cryptographically random 256-bit, one-time secret with a short expiry (default two minutes, maximum five minutes).
3. The app presents a QR code containing a versioned FASX pairing URI and the signaling endpoint. The QR must never contain the account password or long-lived device credentials.
4. The Android user scans the code and submits the challenge with the phone's public device key and display name.
5. Windows shows the requesting device name and identity details.
6. The local Windows user approves or denies.
7. Approval establishes a trusted-device relationship only. Every later remote session still requires session-specific capability consent.
8. The owner can remove a trusted phone at any time.

## Implemented domain rules

- Offer secret has 256 bits of random entropy.
- Only the hash of the offer secret is kept in the coordinator.
- Secret comparison is constant-time.
- The first valid request consumes the secret.
- Requests after use, denial, or expiry are rejected.
- Only the host that created the invitation may approve or deny it.
- Display names and public-key payload lengths are validated.
- Trusted devices are scoped to their host device.
- Revocation removes the device from the trusted-device registry.
- Automated tests cover success, replay, host approval, expiry, denial, revocation, invalid input, and maximum lifetime.

## Production gaps

The current implementation is in-memory and is intended for domain testing. The next steps are:
- Persist host and trusted-device identity securely on Windows.
- Add an authenticated signaling service and HTTPS endpoints.
- Bind public keys to an actual cryptographic device-authentication protocol.
- Rate-limit pairing attempts by account, host, and network.
- Add Android QR scan and device enrollment.
- Add real network tests for replay, expiry, cross-host approval, and revocation.
- Ensure the backend cannot approve a device on behalf of the Windows host.
