using System.Security.Cryptography;

namespace Fasx.Agent.Core.Pairing;

/// <summary>
/// In-process pairing state machine. The Windows companion is the only caller
/// of local approval and device-revocation methods; network handlers must never
/// expose those methods to a phone.
///
/// Pairing invitations and pending claims are short-lived and memory-only.
/// Trusted public device identities can be persisted through the injected
/// protected store.
/// </summary>
public sealed class PairingCoordinator
{
    private static readonly TimeSpan DefaultInvitationLifetime = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MinimumInvitationLifetime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaximumInvitationLifetime = TimeSpan.FromMinutes(5);

    private readonly object _sync = new();
    private readonly IPairedDeviceStore? _deviceStore;
    private readonly Dictionary<Guid, InvitationRecord> _invitations = [];
    private readonly Dictionary<Guid, PendingRecord> _requests = [];
    private readonly Dictionary<Guid, Guid> _requestByInvitation = [];
    private readonly Dictionary<Guid, PairedDevice> _devices = [];

    public PairingCoordinator(IPairedDeviceStore? deviceStore = null)
    {
        _deviceStore = deviceStore;
        if (_deviceStore is null) return;

        foreach (var device in _deviceStore.Load())
        {
            if (device is null || device.DeviceId == Guid.Empty || !_devices.TryAdd(device.DeviceId, device))
            {
                throw new InvalidOperationException("The paired-device store contains an invalid or duplicate device identity.");
            }
        }
    }

    public PairingInvitation CreateInvitation(DateTimeOffset nowUtc, TimeSpan? lifetime = null)
    {
        var actualLifetime = lifetime ?? DefaultInvitationLifetime;
        if (actualLifetime < MinimumInvitationLifetime || actualLifetime > MaximumInvitationLifetime)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                $"Invitation lifetime must be between {MinimumInvitationLifetime} and {MaximumInvitationLifetime}.");
        }

        var secretBytes = RandomNumberGenerator.GetBytes(32);
        var secret = Base64Url.Encode(secretBytes);
        var pairingId = Guid.NewGuid();
        var expiresAt = nowUtc.Add(actualLifetime);
        var secretHash = SHA256.HashData(secretBytes);

        lock (_sync)
        {
            _invitations.Add(pairingId, new InvitationRecord(secretHash, expiresAt));
        }

        CryptographicOperations.ZeroMemory(secretBytes);
        return new PairingInvitation(pairingId, secret, expiresAt);
    }

    public PairingClaimResult SubmitClaim(PairingClaim claim, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(claim);

        if (claim.PairingId == Guid.Empty || claim.DeviceId == Guid.Empty)
        {
            return PairingClaimResult.Failure("Pairing and device identifiers are required.");
        }

        if (string.IsNullOrWhiteSpace(claim.DeviceName) || claim.DeviceName.Trim().Length > 64)
        {
            return PairingClaimResult.Failure("Device name must contain 1 to 64 characters.");
        }

        if (string.IsNullOrEmpty(claim.Secret) ||
            string.IsNullOrEmpty(claim.PublicKeySpki) ||
            string.IsNullOrEmpty(claim.ProofNonce) ||
            string.IsNullOrEmpty(claim.ProofSignature) ||
            claim.Secret.Length > 128 ||
            claim.PublicKeySpki.Length > 512 ||
            claim.ProofNonce.Length > 128 ||
            claim.ProofSignature.Length > 256)
        {
            return PairingClaimResult.Failure("Pairing claim contains an invalid field length.");
        }

        if (!Base64Url.TryDecode(claim.Secret, out var secretBytes) || secretBytes.Length != 32 ||
            !Base64Url.TryDecode(claim.ProofNonce, out var nonceBytes) || nonceBytes.Length != 32 ||
            !Base64Url.TryDecode(claim.ProofSignature, out var signatureBytes))
        {
            CryptographicOperations.ZeroMemory(secretBytes);
            return PairingClaimResult.Failure("Pairing proof is malformed.");
        }

        lock (_sync)
        {
            if (!_invitations.TryGetValue(claim.PairingId, out var invitation))
            {
                ClearSensitiveBuffers(secretBytes, nonceBytes, signatureBytes);
                return PairingClaimResult.Failure("Pairing invitation was not found.");
            }

            if (nowUtc >= invitation.ExpiresAtUtc)
            {
                invitation.Status = PairingStatus.Expired;
                CryptographicOperations.ZeroMemory(invitation.SecretHash);
                ClearSensitiveBuffers(secretBytes, nonceBytes, signatureBytes);
                return PairingClaimResult.Failure("Pairing invitation has expired.");
            }

            if (invitation.Status != PairingStatus.WaitingForDevice)
            {
                ClearSensitiveBuffers(secretBytes, nonceBytes, signatureBytes);
                return PairingClaimResult.Failure("Pairing invitation has already been used.");
            }

            var suppliedHash = SHA256.HashData(secretBytes);
            var secretMatches = CryptographicOperations.FixedTimeEquals(invitation.SecretHash, suppliedHash);
            CryptographicOperations.ZeroMemory(suppliedHash);
            CryptographicOperations.ZeroMemory(secretBytes);

            if (!secretMatches)
            {
                ClearSensitiveBuffers(nonceBytes, signatureBytes);
                return PairingClaimResult.Failure("Pairing secret is incorrect.");
            }
        }

        var proofValid = VerifyDeviceProof(claim, signatureBytes);
        ClearSensitiveBuffers(nonceBytes, signatureBytes);

        if (!proofValid)
        {
            return PairingClaimResult.Failure("Device key proof could not be verified.");
        }

        lock (_sync)
        {
            // Re-check under the lock so concurrent claims cannot both consume the same invitation.
            if (!_invitations.TryGetValue(claim.PairingId, out var invitation) ||
                invitation.Status != PairingStatus.WaitingForDevice ||
                nowUtc >= invitation.ExpiresAtUtc)
            {
                return PairingClaimResult.Failure("Pairing invitation is no longer available.");
            }

            if (_devices.ContainsKey(claim.DeviceId))
            {
                return PairingClaimResult.Failure("This device is already paired.");
            }

            invitation.Status = PairingStatus.AwaitingLocalApproval;
            CryptographicOperations.ZeroMemory(invitation.SecretHash);

            var request = new PendingPairingRequest(
                Guid.NewGuid(),
                claim.PairingId,
                claim.DeviceId,
                claim.DeviceName.Trim(),
                claim.PublicKeySpki,
                nowUtc);

            _requests.Add(request.RequestId, new PendingRecord(request));
            _requestByInvitation.Add(claim.PairingId, request.RequestId);
            return PairingClaimResult.Success(request);
        }
    }

    public IReadOnlyList<PendingPairingRequest> GetPendingRequests(DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            foreach (var entry in _requests.Values.Where(record => record.Status == PairingStatus.AwaitingLocalApproval))
            {
                var invitation = _invitations[entry.Request.PairingId];
                if (nowUtc >= invitation.ExpiresAtUtc)
                {
                    entry.Status = PairingStatus.Expired;
                    invitation.Status = PairingStatus.Expired;
                    CryptographicOperations.ZeroMemory(invitation.SecretHash);
                }
            }

            return _requests.Values
                .Where(record => record.Status == PairingStatus.AwaitingLocalApproval)
                .Select(record => record.Request)
                .OrderBy(record => record.RequestedAtUtc)
                .ToArray();
        }
    }

    /// <summary>Approve only from the trusted local Windows UI after a human confirms the device.</summary>
    public PairedDevice ApproveLocally(Guid requestId, DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            if (!_requests.TryGetValue(requestId, out var pending) ||
                pending.Status != PairingStatus.AwaitingLocalApproval)
            {
                throw new InvalidOperationException("Pairing request is not awaiting local approval.");
            }

            var invitation = _invitations[pending.Request.PairingId];
            if (nowUtc >= invitation.ExpiresAtUtc)
            {
                invitation.Status = PairingStatus.Expired;
                pending.Status = PairingStatus.Expired;
                CryptographicOperations.ZeroMemory(invitation.SecretHash);
                throw new InvalidOperationException("Pairing invitation expired before approval.");
            }

            var pairedDevice = new PairedDevice(
                pending.Request.DeviceId,
                pending.Request.DeviceName,
                pending.Request.PublicKeySpki,
                nowUtc);

            var newDeviceSet = _devices.Values.Append(pairedDevice).ToArray();
            _deviceStore?.Save(newDeviceSet);
            _devices.Add(pairedDevice.DeviceId, pairedDevice);
            pending.Status = PairingStatus.Approved;
            invitation.Status = PairingStatus.Approved;
            return pairedDevice;
        }
    }

    public void DenyLocally(Guid requestId)
    {
        lock (_sync)
        {
            if (!_requests.TryGetValue(requestId, out var pending) ||
                pending.Status != PairingStatus.AwaitingLocalApproval)
            {
                throw new InvalidOperationException("Pairing request is not awaiting local approval.");
            }

            pending.Status = PairingStatus.Denied;
            var invitation = _invitations[pending.Request.PairingId];
            invitation.Status = PairingStatus.Denied;
            CryptographicOperations.ZeroMemory(invitation.SecretHash);
        }
    }

    /// <summary>Cancel an unapproved invitation from the local Windows pairing UI.</summary>
    public bool CancelInvitationLocally(Guid pairingId)
    {
        lock (_sync)
        {
            if (!_invitations.TryGetValue(pairingId, out var invitation) ||
                invitation.Status is not (PairingStatus.WaitingForDevice or PairingStatus.AwaitingLocalApproval))
            {
                return false;
            }

            invitation.Status = PairingStatus.Cancelled;
            CryptographicOperations.ZeroMemory(invitation.SecretHash);

            if (_requestByInvitation.TryGetValue(pairingId, out var requestId) &&
                _requests.TryGetValue(requestId, out var pending))
            {
                pending.Status = PairingStatus.Cancelled;
            }

            return true;
        }
    }

    public PairingStatus GetStatus(Guid pairingId, DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            if (!_invitations.TryGetValue(pairingId, out var invitation))
            {
                return PairingStatus.NotFound;
            }

            if ((invitation.Status is PairingStatus.WaitingForDevice or PairingStatus.AwaitingLocalApproval) &&
                nowUtc >= invitation.ExpiresAtUtc)
            {
                invitation.Status = PairingStatus.Expired;
                CryptographicOperations.ZeroMemory(invitation.SecretHash);
                if (_requestByInvitation.TryGetValue(pairingId, out var requestId))
                {
                    _requests[requestId].Status = PairingStatus.Expired;
                }
            }

            return invitation.Status;
        }
    }

    public IReadOnlyList<PairedDevice> GetPairedDevices()
    {
        lock (_sync)
        {
            return _devices.Values.OrderBy(device => device.DeviceName).ToArray();
        }
    }

    public bool RemovePairedDevice(Guid deviceId)
    {
        lock (_sync)
        {
            if (!_devices.ContainsKey(deviceId)) return false;

            var remaining = _devices.Values.Where(device => device.DeviceId != deviceId).ToArray();
            _deviceStore?.Save(remaining);
            return _devices.Remove(deviceId);
        }
    }

    private static bool VerifyDeviceProof(PairingClaim claim, byte[] signatureBytes)
    {
        try
        {
            if (!Base64Url.TryDecode(claim.PublicKeySpki, out var publicKeyBytes))
            {
                return false;
            }

            using var verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(publicKeyBytes, out var bytesRead);

            if (bytesRead != publicKeyBytes.Length || verifier.KeySize != 256)
            {
                return false;
            }

            var message = PairingProof.CreateMessage(
                claim.PairingId,
                claim.DeviceId,
                claim.ProofNonce);

            return verifier.VerifyData(
                message,
                signatureBytes,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (System.Formats.Asn1.AsnContentException)
        {
            return false;
        }
    }

    private static void ClearSensitiveBuffers(params byte[][] buffers)
    {
        foreach (var buffer in buffers)
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    private sealed class InvitationRecord(byte[] secretHash, DateTimeOffset expiresAtUtc)
    {
        public byte[] SecretHash { get; } = secretHash;
        public DateTimeOffset ExpiresAtUtc { get; } = expiresAtUtc;
        public PairingStatus Status { get; set; } = PairingStatus.WaitingForDevice;
    }

    private sealed class PendingRecord(PendingPairingRequest request)
    {
        public PendingPairingRequest Request { get; } = request;
        public PairingStatus Status { get; set; } = PairingStatus.AwaitingLocalApproval;
    }
}
