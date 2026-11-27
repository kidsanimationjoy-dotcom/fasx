# FASX Pairing Flow — Version 1

## Implementation status

The repository contains a first same-LAN pairing implementation. It includes a local Windows WPF pairing screen, short-lived invitations, a temporary HTTPS Kestrel listener, a QR invitation with an SPKI pin, Android QR validation, Android Keystore device signing, host-side ECDSA verification, Windows-local approve/deny/cancel, and DPAPI-protected storage for approved public device identities.

This is still an engineering milestone. The build and a real Windows/Android same-network test must pass before the pairing feature is considered release-ready.

## Host flow

1. The user opens the visible FASX Windows companion and selects **Pair a phone**.
2. The user clicks **Enable local pairing**.
3. FASX selects one active RFC1918 private IPv4 address and starts a temporary HTTPS listener on that address at port 47832.
4. A self-signed host certificate is created for that address. The QR includes its SPKI SHA-256 pin.
5. FASX creates a random 256-bit invitation secret with a two-minute expiry.
6. The Windows UI displays the QR and the address/expiry.
7. The user can approve or deny each phone request.
8. Creating a new QR cancels the previous unapproved invitation. Stopping or closing the pairing window cancels the current invitation and closes the listener.

## Phone flow

1. Android scans the QR with Google Code Scanner.
2. The parser verifies version, invitation type, secret size, private IPv4 address, HTTPS scheme, claim route, fixed port, SPKI pin size, and expiry.
3. The app displays the computer's IP address and waits for the phone owner to confirm.
4. Android generates or loads its P-256 signing key from Android Keystore.
5. The app creates a random 256-bit proof nonce and signs the canonical pairing proof.
6. It submits the claim through HTTPS using the SPKI pin from the QR.
7. It polls the read-only status route until the Windows user approves, denies, cancels, or the invitation expires.

## QR payload

```json
{
  "v": 1,
  "kind": "fasx-pair",
  "pairingId": "uuid",
  "secret": "base64url-256-bit-secret",
  "endpoint": "https://192.168.1.10:47832/api/pairing/claim",
  "tlsPin": "sha256/base64-sha256-SPKI-pin",
  "expiresAtUtc": "UTC ISO-8601 timestamp"
}
```

Only private RFC1918 IPv4 addresses are accepted in this milestone. Internet endpoints, hostnames, HTTP, alternate ports, query strings, and fragments are rejected.

## Device proof

The phone signs UTF-8 bytes of the exact newline-separated string:

```text
FASX-PAIRING-V1
{pairingId in lowercase D format}
{deviceId in lowercase D format}
{proofNonce in Base64URL}
```

Use ECDSA P-256 with SHA-256 and RFC 3279 DER sequence signature encoding.

The public key is SubjectPublicKeyInfo DER encoded, then Base64URL encoded. The nonce is 32 cryptographically random bytes encoded as Base64URL.

## Network routes

- `POST /api/pairing/claim` — accepts a signed device claim; fixed-window rate limit.
- `GET /api/pairing/status/{pairingId}` — returns invitation state only; a separate higher polling limit.
- Local approve, deny, cancel, and paired-device revocation are in-process Windows operations and are not HTTP routes.

The HTTPS listener binds to one selected private IPv4 interface, not all interfaces. It closes when the user stops pairing or closes the pairing window. Response caching is disabled.

## Persistence

- Pairing invitations and pending claims are process-memory only and disappear when the pairing process exits.
- Approved device public identities are stored in the current user's Windows LocalAppData and encrypted with DPAPI.
- Android device signing private key remains in Android Keystore.
- Android stores a local device identifier; no private signing key is exported.

## Security boundaries

- Invitation secret is 256 bits, one-time, short-lived, and stored on Windows only as a SHA-256 hash.
- TLS uses a temporary server certificate; the Android client checks the SPKI pin from the QR and retains hostname verification.
- Device signature proves key possession. It does not authenticate the human-readable device name.
- Host approval is a separate local operation. A phone/network endpoint cannot call the local approval method.
- Pairing does not grant screen, keyboard, file, clipboard, or power capabilities.
- No screen-capture or remote-input endpoint is present in the pairing server.
- Claim endpoint and status endpoint have separate rate limits; requests have a size cap.

## Remaining validation and limitations

- Windows and Android CI build results must be recorded.
- Test with a Windows host and Android phone on the same trusted Wi-Fi network.
- Verify Windows Defender Firewall prompt/allow behaviour without silently changing firewall settings.
- Test failed TLS pin, expired certificate, expired invitation, replay, wrong signature, duplicate claim, host cancellation, and restart.
- Public internet access, relay infrastructure, remote screen streaming, remote input, audio, and File Bridge are not part of this milestone.
