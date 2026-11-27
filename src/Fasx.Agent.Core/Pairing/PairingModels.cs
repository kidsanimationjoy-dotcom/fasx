namespace Fasx.Agent.Core.Pairing;

/// <summary>One-time invitation data returned only to the Windows user creating the invitation.</summary>
public sealed record PairingInvitation(
    Guid PairingId,
    string Secret,
    DateTimeOffset ExpiresAtUtc);

/// <summary>Device claim submitted by a phone after scanning/entering an invitation.</summary>
public sealed record PairingClaim(
    Guid PairingId,
    string Secret,
    Guid DeviceId,
    string DeviceName,
    string PublicKeySpki,
    string ProofNonce,
    string ProofSignature);

/// <summary>Information shown to the local Windows user before approving a new device.</summary>
public sealed record PendingPairingRequest(
    Guid RequestId,
    Guid PairingId,
    Guid DeviceId,
    string DeviceName,
    string PublicKeySpki,
    DateTimeOffset RequestedAtUtc);

public sealed record PairedDevice(
    Guid DeviceId,
    string DeviceName,
    string PublicKeySpki,
    DateTimeOffset PairedAtUtc);

public enum PairingStatus
{
    NotFound,
    WaitingForDevice,
    AwaitingLocalApproval,
    Approved,
    Denied,
    Expired,
    Cancelled
}

public sealed record PairingClaimResult(
    bool Accepted,
    PendingPairingRequest? Request,
    string? Error)
{
    public static PairingClaimResult Success(PendingPairingRequest request) =>
        new(true, request, null);

    public static PairingClaimResult Failure(string error) =>
        new(false, null, error);
}
