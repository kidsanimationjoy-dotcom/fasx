using System.Security.Cryptography;
using Fasx.Agent.Core.Pairing;
using Xunit;

namespace Fasx.Agent.Core.Tests;

public sealed class PairingCoordinatorTests
{
    [Fact]
    public void ValidDeviceProofRequiresLocalApprovalBeforeItIsPaired()
    {
        var now = DateTimeOffset.UtcNow;
        var coordinator = new PairingCoordinator();
        using var phoneKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var invitation = coordinator.CreateInvitation(now);
        var claim = CreateClaim(invitation, phoneKey, "FASX phone");

        var result = coordinator.SubmitClaim(claim, now);

        Assert.True(result.Accepted);
        Assert.NotNull(result.Request);
        Assert.Equal(PairingStatus.AwaitingLocalApproval, coordinator.GetStatus(invitation.PairingId, now));
        Assert.Empty(coordinator.GetPairedDevices());

        var paired = coordinator.ApproveLocally(result.Request!.RequestId, now.AddSeconds(2));

        Assert.Equal(claim.DeviceId, paired.DeviceId);
        Assert.Single(coordinator.GetPairedDevices());
        Assert.Equal(PairingStatus.Approved, coordinator.GetStatus(invitation.PairingId, now.AddSeconds(2)));
    }

    [Fact]
    public void InvitationSecretCanOnlyBeClaimedOnce()
    {
        var now = DateTimeOffset.UtcNow;
        var coordinator = new PairingCoordinator();
        using var firstKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var secondKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var invitation = coordinator.CreateInvitation(now);

        var first = coordinator.SubmitClaim(CreateClaim(invitation, firstKey, "First phone"), now);
        var second = coordinator.SubmitClaim(CreateClaim(invitation, secondKey, "Second phone"), now);

        Assert.True(first.Accepted);
        Assert.False(second.Accepted);
        Assert.Contains("already been used", second.Error);
    }

    [Fact]
    public void WrongInvitationSecretIsRejected()
    {
        var now = DateTimeOffset.UtcNow;
        var coordinator = new PairingCoordinator();
        using var phoneKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var invitation = coordinator.CreateInvitation(now);
        var claim = CreateClaim(invitation, phoneKey, "Phone") with
        {
            Secret = Base64Url.Encode(RandomNumberGenerator.GetBytes(32))
        };

        var result = coordinator.SubmitClaim(claim, now);

        Assert.False(result.Accepted);
        Assert.Equal("Pairing secret is incorrect.", result.Error);
    }

    [Fact]
    public void ExpiredInvitationCannotBeClaimed()
    {
        var now = DateTimeOffset.UtcNow;
        var coordinator = new PairingCoordinator();
        using var phoneKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var invitation = coordinator.CreateInvitation(now, TimeSpan.FromSeconds(30));

        var result = coordinator.SubmitClaim(CreateClaim(invitation, phoneKey, "Phone"), now.AddSeconds(31));

        Assert.False(result.Accepted);
        Assert.Equal(PairingStatus.Expired, coordinator.GetStatus(invitation.PairingId, now.AddSeconds(31)));
    }

    [Fact]
    public void ExpiredPendingClaimIsNotPresentedForLocalApproval()
    {
        var now = DateTimeOffset.UtcNow;
        var coordinator = new PairingCoordinator();
        using var phoneKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var invitation = coordinator.CreateInvitation(now, TimeSpan.FromSeconds(30));
        var result = coordinator.SubmitClaim(CreateClaim(invitation, phoneKey, "Phone"), now);

        Assert.True(result.Accepted);
        Assert.Empty(coordinator.GetPendingRequests(now.AddSeconds(31)));
        Assert.Equal(PairingStatus.Expired, coordinator.GetStatus(invitation.PairingId, now.AddSeconds(31)));
    }

    [Fact]
    public void InvalidDeviceSignatureIsRejected()
    {
        var now = DateTimeOffset.UtcNow;
        var coordinator = new PairingCoordinator();
        using var phoneKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var invitation = coordinator.CreateInvitation(now);
        var validClaim = CreateClaim(invitation, phoneKey, "Phone");
        var forgedSignature = otherKey.SignData(
            PairingProof.CreateMessage(validClaim.PairingId, validClaim.DeviceId, validClaim.ProofNonce),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);
        var forgedClaim = validClaim with { ProofSignature = Base64Url.Encode(forgedSignature) };

        var result = coordinator.SubmitClaim(forgedClaim, now);

        Assert.False(result.Accepted);
        Assert.Equal("Device key proof could not be verified.", result.Error);
    }

    [Fact]
    public void DenyingClaimPreventsPairing()
    {
        var now = DateTimeOffset.UtcNow;
        var coordinator = new PairingCoordinator();
        using var phoneKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var invitation = coordinator.CreateInvitation(now);
        var result = coordinator.SubmitClaim(CreateClaim(invitation, phoneKey, "Phone"), now);

        Assert.True(result.Accepted);
        coordinator.DenyLocally(result.Request!.RequestId);

        Assert.Equal(PairingStatus.Denied, coordinator.GetStatus(invitation.PairingId, now));
        Assert.Empty(coordinator.GetPairedDevices());
    }

    [Fact]
    public void LocalCancellationInvalidatesInvitationAndPendingRequest()
    {
        var now = DateTimeOffset.UtcNow;
        var coordinator = new PairingCoordinator();
        using var phoneKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var invitation = coordinator.CreateInvitation(now);
        var result = coordinator.SubmitClaim(CreateClaim(invitation, phoneKey, "Phone"), now);

        Assert.True(result.Accepted);
        Assert.True(coordinator.CancelInvitationLocally(invitation.PairingId));
        Assert.Equal(PairingStatus.Cancelled, coordinator.GetStatus(invitation.PairingId, now));
        Assert.Empty(coordinator.GetPendingRequests(now));
        Assert.False(coordinator.CancelInvitationLocally(invitation.PairingId));
        Assert.Empty(coordinator.GetPairedDevices());
    }

    [Fact]
    public void ApprovedDeviceCanBeRevoked()
    {
        var now = DateTimeOffset.UtcNow;
        var coordinator = new PairingCoordinator();
        using var phoneKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var invitation = coordinator.CreateInvitation(now);
        var result = coordinator.SubmitClaim(CreateClaim(invitation, phoneKey, "Phone"), now);
        var paired = coordinator.ApproveLocally(result.Request!.RequestId, now);

        Assert.True(coordinator.RemovePairedDevice(paired.DeviceId));
        Assert.Empty(coordinator.GetPairedDevices());
        Assert.False(coordinator.RemovePairedDevice(paired.DeviceId));
    }

    [Fact]
    public void InvitationLifetimeIsBounded()
    {
        var coordinator = new PairingCoordinator();
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            coordinator.CreateInvitation(now, TimeSpan.FromSeconds(5)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            coordinator.CreateInvitation(now, TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public void ApprovedDevicesAreSavedAndReloadedFromStore()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new RecordingDeviceStore();
        using var phoneKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var coordinator = new PairingCoordinator(store);
        var invitation = coordinator.CreateInvitation(now);
        var result = coordinator.SubmitClaim(CreateClaim(invitation, phoneKey, "Phone"), now);

        coordinator.ApproveLocally(result.Request!.RequestId, now);
        var restartedCoordinator = new PairingCoordinator(store);

        Assert.Single(restartedCoordinator.GetPairedDevices());
        Assert.Equal(result.Request.DeviceId, restartedCoordinator.GetPairedDevices()[0].DeviceId);
    }

    [Fact]
    public async Task OnlyOneOfTwoConcurrentClaimsCanConsumeInvitation()
    {
        var now = DateTimeOffset.UtcNow;
        var coordinator = new PairingCoordinator();
        using var firstKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var secondKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var invitation = coordinator.CreateInvitation(now);
        var firstClaim = CreateClaim(invitation, firstKey, "First phone");
        var secondClaim = CreateClaim(invitation, secondKey, "Second phone");

        var results = await Task.WhenAll(
            Task.Run(() => coordinator.SubmitClaim(firstClaim, now)),
            Task.Run(() => coordinator.SubmitClaim(secondClaim, now)));

        Assert.Single(results.Where(result => result.Accepted));
        Assert.Single(coordinator.GetPendingRequests(now));
    }

    private static PairingClaim CreateClaim(
        PairingInvitation invitation,
        ECDsa phoneKey,
        string name)
    {
        var deviceId = Guid.NewGuid();
        var nonce = Base64Url.Encode(RandomNumberGenerator.GetBytes(32));
        var message = PairingProof.CreateMessage(invitation.PairingId, deviceId, nonce);
        var signature = phoneKey.SignData(
            message,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);

        return new PairingClaim(
            invitation.PairingId,
            invitation.Secret,
            deviceId,
            name,
            Base64Url.Encode(phoneKey.ExportSubjectPublicKeyInfo()),
            nonce,
            Base64Url.Encode(signature));
    }

    private sealed class RecordingDeviceStore : IPairedDeviceStore
    {
        private List<PairedDevice> _devices = [];

        public IReadOnlyList<PairedDevice> Load() => _devices.ToArray();

        public void Save(IReadOnlyCollection<PairedDevice> devices) =>
            _devices = devices.ToList();
    }
}
