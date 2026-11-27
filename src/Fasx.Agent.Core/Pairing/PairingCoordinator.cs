using System.Security.Cryptography;

namespace Fasx.Agent.Core.Pairing;

/// <summary>
/// In-memory pairing ceremony used to validate FASX's host-consent flow.
/// This is deliberately not a production persistence layer: pairings and trusted
/// devices are lost when the process exits. Production storage must use protected
/// Windows storage and a separately authenticated signaling service.
/// </summary>
public sealed class PairingCoordinator
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MaximumLifetime = TimeSpan.FromMinutes(5);
    private const int MinimumPublicKeyBytes = 32;
    private const int MaximumPublicKeyBytes = 256;
    private const int MaximumDisplayNameLength = 64;

    private readonly object _sync = new();
    private readonly Dictionary<Guid, PendingPairing> _pairings = new();
    private readonly Dictionary<(Guid HostId, Guid DeviceId), PairedDevice> _trustedDevices = new();

    public PairingOffer CreateOffer(Guid hostDeviceId, DateTimeOffset nowUtc, TimeSpan? lifetime = null)
    {
        if (hostDeviceId == Guid.Empty)
        {
            throw new ArgumentException("A host device ID is required.", nameof(hostDeviceId));
        }

        var effectiveLifetime = lifetime ?? DefaultLifetime;
        if (effectiveLifetime <= TimeSpan.Zero || effectiveLifetime > MaximumLifetime)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                $"Pairing offers must expire within {MaximumLifetime.TotalMinutes:0} minutes.");
        }

        var secretBytes = RandomNumberGenerator.GetBytes(32);
        var secret = ToBase64Url(secretBytes);
        var secretHash = SHA256.HashData(secretBytes);
        CryptographicOperations.ZeroMemory(secretBytes);

        var offer = new PairingOffer(
            Guid.NewGuid(),
            hostDeviceId,
            secret,
            nowUtc.ToUniversalTime().Add(effectiveLifetime));

        lock (_sync)
        {
            _pairings.Add(offer.PairingId, new PendingPairing(
                offer.PairingId,
                hostDeviceId,
                secretHash,
                offer.ExpiresAtUtc));
        }

        return offer;
    }

    /// <summary>
    /// Accepts a device request only when its one-time secret is correct and the
    /// invitation is still pending. The secret is consumed on the first valid request.
    /// </summary>
    public bool TryRequestPairing(
        Guid pairingId,
        string? secret,
        Guid deviceId,
        string? displayName,
        string? publicKeyBase64,
        DateTimeOffset nowUtc,
        out PairingDeviceRequest? request)
    {
        request = null;

        if (pairingId == Guid.Empty ||
            deviceId == Guid.Empty ||
            !TryNormalizeDisplayName(displayName, out var normalizedName) ||
            !TryNormalizePublicKey(publicKeyBase64, out var normalizedPublicKey) ||
            !TryDecodeSecret(secret, out var secretBytes))
        {
            return false;
        }

        var candidateHash = SHA256.HashData(secretBytes);
        CryptographicOperations.ZeroMemory(secretBytes);

        lock (_sync)
        {
            if (!_pairings.TryGetValue(pairingId, out var pairing) ||
                !RefreshExpiry(pairing, nowUtc) ||
                pairing.Status != PairingStatus.Pending ||
                pairing.SecretHash is null)
            {
                CryptographicOperations.ZeroMemory(candidateHash);
                return false;
            }

            var secretMatches = CryptographicOperations.FixedTimeEquals(
                candidateHash,
                pairing.SecretHash);
            CryptographicOperations.ZeroMemory(candidateHash);

            if (!secretMatches)
            {
                return false;
            }

            CryptographicOperations.ZeroMemory(pairing.SecretHash);
            pairing.SecretHash = null;

            request = new PairingDeviceRequest(
                pairing.PairingId,
                deviceId,
                normalizedName,
                normalizedPublicKey,
                nowUtc.ToUniversalTime());

            pairing.Request = request;
            pairing.Status = PairingStatus.AwaitingHostApproval;
            return true;
        }
    }

    public PairingDeviceRequest? GetPendingRequest(
        Guid pairingId,
        Guid hostDeviceId,
        DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            if (!_pairings.TryGetValue(pairingId, out var pairing) ||
                pairing.HostDeviceId != hostDeviceId ||
                !RefreshExpiry(pairing, nowUtc) ||
                pairing.Status != PairingStatus.AwaitingHostApproval)
            {
                return null;
            }

            return pairing.Request;
        }
    }

    /// <summary>
    /// Approves a pairing only when the approving host matches the host that
    /// created the invitation. This establishes device trust, not a remote session.
    /// Session capabilities must still be approved separately.
    /// </summary>
    public PairedDevice? TryApprove(
        Guid pairingId,
        Guid hostDeviceId,
        DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            if (!_pairings.TryGetValue(pairingId, out var pairing) ||
                pairing.HostDeviceId != hostDeviceId ||
                !RefreshExpiry(pairing, nowUtc) ||
                pairing.Status != PairingStatus.AwaitingHostApproval ||
                pairing.Request is null)
            {
                return null;
            }

            var pairedDevice = new PairedDevice(
                hostDeviceId,
                pairing.Request.DeviceId,
                pairing.Request.DisplayName,
                pairing.Request.PublicKeyBase64,
                nowUtc.ToUniversalTime());

            _trustedDevices[(hostDeviceId, pairedDevice.DeviceId)] = pairedDevice;
            pairing.Status = PairingStatus.Approved;
            return pairedDevice;
        }
    }

    public bool TryDeny(Guid pairingId, Guid hostDeviceId, DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            if (!_pairings.TryGetValue(pairingId, out var pairing) ||
                pairing.HostDeviceId != hostDeviceId ||
                !RefreshExpiry(pairing, nowUtc) ||
                pairing.Status != PairingStatus.AwaitingHostApproval)
            {
                return false;
            }

            pairing.Status = PairingStatus.Denied;
            return true;
        }
    }

    public IReadOnlyList<PairedDevice> GetTrustedDevices(Guid hostDeviceId)
    {
        lock (_sync)
        {
            return _trustedDevices
                .Where(entry => entry.Key.HostId == hostDeviceId)
                .Select(entry => entry.Value)
                .OrderBy(device => device.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public bool RevokeDevice(Guid hostDeviceId, Guid deviceId)
    {
        if (hostDeviceId == Guid.Empty || deviceId == Guid.Empty)
        {
            return false;
        }

        lock (_sync)
        {
            return _trustedDevices.Remove((hostDeviceId, deviceId));
        }
    }

    public PairingStatus? GetStatus(Guid pairingId, DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            if (!_pairings.TryGetValue(pairingId, out var pairing))
            {
                return null;
            }

            RefreshExpiry(pairing, nowUtc);
            return pairing.Status;
        }
    }

    private static bool RefreshExpiry(PendingPairing pairing, DateTimeOffset nowUtc)
    {
        if ((pairing.Status is PairingStatus.Pending or PairingStatus.AwaitingHostApproval) &&
            pairing.ExpiresAtUtc <= nowUtc.ToUniversalTime())
        {
            pairing.Status = PairingStatus.Expired;
            if (pairing.SecretHash is not null)
            {
                CryptographicOperations.ZeroMemory(pairing.SecretHash);
                pairing.SecretHash = null;
            }

            return false;
        }

        return true;
    }

    private static bool TryNormalizeDisplayName(string? value, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <= MaximumDisplayNameLength &&
               !normalized.Any(char.IsControl);
    }

    private static bool TryNormalizePublicKey(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(value);
            if (bytes.Length is < MinimumPublicKeyBytes or > MaximumPublicKeyBytes)
            {
                return false;
            }

            normalized = Convert.ToBase64String(bytes);
            CryptographicOperations.ZeroMemory(bytes);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryDecodeSecret(string? value, out byte[] secretBytes)
    {
        secretBytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
        {
            return false;
        }

        var normalized = value.Replace('-', '+').Replace('_', '/');
        var padding = (4 - normalized.Length % 4) % 4;

        try
        {
            var decoded = Convert.FromBase64String(normalized + new string('=', padding));
            if (decoded.Length != 32)
            {
                CryptographicOperations.ZeroMemory(decoded);
                return false;
            }

            secretBytes = decoded;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string ToBase64Url(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private sealed class PendingPairing(
        Guid pairingId,
        Guid hostDeviceId,
        byte[] secretHash,
        DateTimeOffset expiresAtUtc)
    {
        public Guid PairingId { get; } = pairingId;
        public Guid HostDeviceId { get; } = hostDeviceId;
        public byte[]? SecretHash { get; set; } = secretHash;
        public DateTimeOffset ExpiresAtUtc { get; } = expiresAtUtc;
        public PairingStatus Status { get; set; } = PairingStatus.Pending;
        public PairingDeviceRequest? Request { get; set; }
    }
}
