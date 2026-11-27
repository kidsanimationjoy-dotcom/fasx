using System.Security.Cryptography;
using Fasx.Agent.Core.Pairing;
using Xunit;

namespace Fasx.Agent.Core.Tests;

public sealed class PairingCoordinatorTests
{
    private static readonly ECDsa TestPhoneKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private static readonly string TestPublicKeyBase64 =
        Convert.ToBase64String(TestPhoneKey.ExportSubjectPublicKeyInfo());

    private static string CreateProof(PairingOffer offer, Guid deviceId, string displayName)
    {
        var normalizedSecret = offer.Secret.Replace('-', '+').Replace('_', '/');
        var padding = (4 - normalizedSecret.Length % 4) % 4;
        var secretBytes = Convert.FromBase64String(normalizedSecret + new string('=', padding));
        var payload = PairingProof.CreatePayload(offer.PairingId, deviceId, displayName, secretBytes);

        try
        {
            return Convert.ToBase64String(TestPhoneKey.SignData(payload, HashAlgorithmName.SHA256));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secretBytes);
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    [Fact]
    public void PairingRequiresOneTimeSecretAndDevicePrivateKeyProof()
    {
        var coordinator = new PairingCoordinator();
        var now = DateTimeOffset.UtcNow;
        var hostId = Guid.NewGuid();
        var phoneId = Guid.NewGuid();
        var offer = coordinator.CreateOffer(hostId, now);
        var proof = CreateProof(offer, phoneId, "My Phone");

        Assert.False(coordinator.TryRequestPairing(
            offer.PairingId, "wrong-secret", phoneId, "My Phone",
            TestPublicKeyBase64, proof, now, out _));

        Assert.True(coordinator.TryRequestPairing(
            offer.PairingId, offer.Secret, phoneId, "My Phone",
            TestPublicKeyBase64, proof, now, out var request));
        Assert.NotNull(request);
        Assert.Equal(phoneId, request.DeviceId);

        Assert.False(coordinator.TryRequestPairing(
            offer.PairingId, offer.Secret, Guid.NewGuid(), "Another Phone",
            TestPublicKeyBase64, proof, now, out _));
    }

    [Fact]
    public void OnlyTheHostThatCreatedTheOfferCanApproveIt()
    {
        var coordinator = new PairingCoordinator();
        var now = DateTimeOffset.UtcNow;
        var hostId = Guid.NewGuid();
        var offer = coordinator.CreateOffer(hostId, now);
        var phoneId = Guid.NewGuid();

        Assert.True(coordinator.TryRequestPairing(
            offer.PairingId, offer.Secret, phoneId, "Phone",
            TestPublicKeyBase64, CreateProof(offer, phoneId, "Phone"), now, out _));

        Assert.Null(coordinator.TryApprove(offer.PairingId, Guid.NewGuid(), now));
        Assert.Equal(PairingStatus.AwaitingHostApproval, coordinator.GetStatus(offer.PairingId, now));

        var paired = coordinator.TryApprove(offer.PairingId, hostId, now);
        Assert.NotNull(paired);
        Assert.Equal(phoneId, paired.DeviceId);
        Assert.Equal(PairingStatus.Approved, coordinator.GetStatus(offer.PairingId, now));
    }

    [Fact]
    public void PairingOfferExpiresAndCannotBeUsed()
    {
        var coordinator = new PairingCoordinator();
        var now = DateTimeOffset.UtcNow;
        var offer = coordinator.CreateOffer(Guid.NewGuid(), now, TimeSpan.FromSeconds(30));
        var phoneId = Guid.NewGuid();

        Assert.False(coordinator.TryRequestPairing(
            offer.PairingId, offer.Secret, phoneId, "Phone",
            TestPublicKeyBase64, CreateProof(offer, phoneId, "Phone"), now.AddSeconds(31), out _));
        Assert.Equal(PairingStatus.Expired, coordinator.GetStatus(offer.PairingId, now.AddSeconds(31)));
    }

    [Fact]
    public void HostCanDenyAndRevocationRemovesTrustedDevice()
    {
        var coordinator = new PairingCoordinator();
        var now = DateTimeOffset.UtcNow;
        var hostId = Guid.NewGuid();
        var phoneId = Guid.NewGuid();
        var offer = coordinator.CreateOffer(hostId, now);

        Assert.True(coordinator.TryRequestPairing(
            offer.PairingId, offer.Secret, phoneId, "Phone",
            TestPublicKeyBase64, CreateProof(offer, phoneId, "Phone"), now, out _));
        Assert.True(coordinator.TryDeny(offer.PairingId, hostId, now));
        Assert.Equal(PairingStatus.Denied, coordinator.GetStatus(offer.PairingId, now));
        Assert.Empty(coordinator.GetTrustedDevices(hostId));

        var secondOffer = coordinator.CreateOffer(hostId, now);
        Assert.True(coordinator.TryRequestPairing(
            secondOffer.PairingId, secondOffer.Secret, phoneId, "Phone",
            TestPublicKeyBase64, CreateProof(secondOffer, phoneId, "Phone"), now, out _));
        Assert.NotNull(coordinator.TryApprove(secondOffer.PairingId, hostId, now));

        Assert.Single(coordinator.GetTrustedDevices(hostId));
        Assert.True(coordinator.RevokeDevice(hostId, phoneId));
        Assert.Empty(coordinator.GetTrustedDevices(hostId));
    }

    [Fact]
    public void PairingRejectsMalformedPublicKeysAndDeviceNames()
    {
        var coordinator = new PairingCoordinator();
        var now = DateTimeOffset.UtcNow;
        var offer = coordinator.CreateOffer(Guid.NewGuid(), now);
        var phoneId = Guid.NewGuid();

        Assert.False(coordinator.TryRequestPairing(
            offer.PairingId, offer.Secret, phoneId, "Phone",
            "not-base64", "not-a-signature", now, out _));

        var nextOffer = coordinator.CreateOffer(Guid.NewGuid(), now);
        Assert.False(coordinator.TryRequestPairing(
            nextOffer.PairingId, nextOffer.Secret, phoneId, "Bad\nName",
            TestPublicKeyBase64, "not-a-signature", now, out _));
    }

    [Fact]
    public void InvalidProofDoesNotConsumePairingOffer()
    {
        var coordinator = new PairingCoordinator();
        var now = DateTimeOffset.UtcNow;
        var hostId = Guid.NewGuid();
        var phoneId = Guid.NewGuid();
        var offer = coordinator.CreateOffer(hostId, now);
        var validProof = CreateProof(offer, phoneId, "Phone");

        Assert.False(coordinator.TryRequestPairing(
            offer.PairingId, offer.Secret, phoneId, "Phone",
            TestPublicKeyBase64, "invalid-signature", now, out _));

        Assert.True(coordinator.TryRequestPairing(
            offer.PairingId, offer.Secret, phoneId, "Phone",
            TestPublicKeyBase64, validProof, now, out _));
    }

    [Fact]
    public void PairingRejectsNonP256PublicKey()
    {
        var coordinator = new PairingCoordinator();
        var now = DateTimeOffset.UtcNow;
        var offer = coordinator.CreateOffer(Guid.NewGuid(), now);
        var phoneId = Guid.NewGuid();

        using var unrelatedKey = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var unrelatedPublicKey = Convert.ToBase64String(unrelatedKey.ExportSubjectPublicKeyInfo());

        Assert.False(coordinator.TryRequestPairing(
            offer.PairingId, offer.Secret, phoneId, "Phone",
            unrelatedPublicKey, "invalid-signature", now, out _));
    }

    [Fact]
    public void PairingLifetimeCannotExceedFiveMinutes()
    {
        var coordinator = new PairingCoordinator();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            coordinator.CreateOffer(Guid.NewGuid(), DateTimeOffset.UtcNow, TimeSpan.FromMinutes(6)));
    }
}
