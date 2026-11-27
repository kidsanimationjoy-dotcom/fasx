# FASX Signaling and Control Plane

Planned stack: ASP.NET Core on .NET 10.

Responsibilities:
- Authenticated device registration and presence.
- Short-lived pairing challenge exchange.
- WebRTC offer/answer and ICE candidate signaling.
- Session metadata, expiry, and revocation.
- Rate limiting and audit events.

Out of scope:
- Screen recording or media storage.
- Arbitrary remote command execution.
- Long-lived secrets embedded in clients.
- Silent or hidden device access.

The signaling service must not be treated as a trusted authority for permission grants. The Windows host makes the final local-consent decision.
