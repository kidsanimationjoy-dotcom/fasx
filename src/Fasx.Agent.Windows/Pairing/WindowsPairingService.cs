using Fasx.Agent.Core.Pairing;
using Fasx.Agent.Windows.Identity;

namespace Fasx.Agent.Windows.Pairing;

/// <summary>
/// Windows application service joining the pairing-domain ceremony to the
/// persistent DPAPI-protected trusted-device registry.
/// This service has no network listener; callers must already be inside the
/// authenticated host-side pairing flow.
/// </summary>
public sealed class WindowsPairingService
{
    private readonly PairingCoordinator _coordinator;
    private readonly WindowsTrustedDeviceStore _trustedDevices;

    public WindowsPairingService(
        PairingCoordinator coordinator,
        WindowsTrustedDeviceStore trustedDevices)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _trustedDevices = trustedDevices ?? throw new ArgumentNullException(nameof(trustedDevices));
    }

    public PairingOffer CreateOffer(
        Guid hostDeviceId,
        DateTimeOffset nowUtc,
        TimeSpan? lifetime = null) =>
        _coordinator.CreateOffer(hostDeviceId, nowUtc, lifetime);

    public bool TryRequestPairing(
        Guid pairingId,
        string? secret,
        Guid deviceId,
        string? displayName,
        string? publicKeyBase64,
        string? proofSignatureBase64,
        DateTimeOffset nowUtc,
        out PairingDeviceRequest? request) =>
        _coordinator.TryRequestPairing(
            pairingId,
            secret,
            deviceId,
            displayName,
            publicKeyBase64,
            proofSignatureBase64,
            nowUtc,
            out request);

    public PairingDeviceRequest? GetPendingRequest(
        Guid pairingId,
        Guid hostDeviceId,
        DateTimeOffset nowUtc) =>
        _coordinator.GetPendingRequest(pairingId, hostDeviceId, nowUtc);

    public bool Deny(Guid pairingId, Guid hostDeviceId, DateTimeOffset nowUtc) =>
        _coordinator.TryDeny(pairingId, hostDeviceId, nowUtc);

    /// <summary>
    /// Persists a locally approved phone. If persistence fails, the in-memory
    /// coordinator's trusted-device entry is rolled back and the exception is
    /// returned to the caller; the host must not report pairing as successful.
    /// </summary>
    public PairedDevice? Approve(Guid pairingId, Guid hostDeviceId, DateTimeOffset nowUtc)
    {
        var pairedDevice = _coordinator.TryApprove(pairingId, hostDeviceId, nowUtc);
        if (pairedDevice is null)
        {
            return null;
        }

        try
        {
            _trustedDevices.AddOrUpdate(pairedDevice);
            return pairedDevice;
        }
        catch
        {
            _coordinator.RevokeDevice(hostDeviceId, pairedDevice.DeviceId);
            throw;
        }
    }

    public IReadOnlyList<PairedDevice> GetTrustedDevices(Guid hostDeviceId) =>
        _trustedDevices.GetForHost(hostDeviceId);

    public bool Revoke(Guid hostDeviceId, Guid deviceId)
    {
        var removed = _trustedDevices.Revoke(hostDeviceId, deviceId);
        _coordinator.RevokeDevice(hostDeviceId, deviceId);
        return removed;
    }
}
