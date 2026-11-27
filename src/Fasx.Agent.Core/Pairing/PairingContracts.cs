namespace Fasx.Agent.Core.Pairing;

/// <summary>
/// A short-lived invitation created by the Windows host. The secret is a one-time
/// pairing capability and must never be logged or persisted in plaintext.
/// </summary>
public sealed record PairingOffer(
    Guid PairingId,
    Guid HostDeviceId,
    string Secret,
    DateTimeOffset ExpiresAtUtc);

/// <summary>Identity details supplied by a phone requesting to pair.</summary>
public sealed record PairingDeviceRequest(
    Guid PairingId,
    Guid DeviceId,
    string DisplayName,
    string PublicKeyBase64,
    DateTimeOffset RequestedAtUtc);

/// <summary>A trusted controller device after explicit local host approval.</summary>
public sealed record PairedDevice(
    Guid HostDeviceId,
    Guid DeviceId,
    string DisplayName,
    string PublicKeyBase64,
    DateTimeOffset PairedAtUtc);

public enum PairingStatus
{
    Pending,
    AwaitingHostApproval,
    Approved,
    Denied,
    Expired
}
