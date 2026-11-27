using System.Security.Cryptography;
using Fasx.Agent.Core.Pairing;
using Fasx.Agent.Core.Sessions;
using Xunit;

namespace Fasx.Agent.Core.Tests;

public sealed class SessionProofVerifierTests
{
    [Fact]
    public void ValidProofFromStoredPairedDeviceIsAccepted()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = CreatePairedDevice(key);
        var proof = CreateProof(key, device.DeviceId);

        Assert.True(SessionProofVerifier.Verify(device, proof));
    }

    [Fact]
    public void SignatureFromDifferentPrivateKeyIsRejected()
    {
        using var storedKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = CreatePairedDevice(storedKey);
        var forged = CreateProof(otherKey, device.DeviceId);

        Assert.False(SessionProofVerifier.Verify(device, forged));
    }

    [Fact]
    public void ProofForDifferentDeviceIsRejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = CreatePairedDevice(key);
        var proof = CreateProof(key, Guid.NewGuid());

        Assert.False(SessionProofVerifier.Verify(device, proof));
    }

    [Fact]
    public void ModifiedNonceInvalidatesSignature()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = CreatePairedDevice(key);
        var proof = CreateProof(key, device.DeviceId) with
        {
            Nonce = Base64Url.Encode(RandomNumberGenerator.GetBytes(32))
        };

        Assert.False(SessionProofVerifier.Verify(device, proof));
    }

    private static PairedDevice CreatePairedDevice(ECDsa key) =>
        new(
            Guid.NewGuid(),
            "FASX test phone",
            Base64Url.Encode(key.ExportSubjectPublicKeyInfo()),
            DateTimeOffset.UtcNow);

    private static SessionDeviceProof CreateProof(ECDsa key, Guid deviceId)
    {
        var sessionId = Guid.NewGuid();
        var nonce = Base64Url.Encode(RandomNumberGenerator.GetBytes(32));
        var message = SessionProofVerifier.CreateMessage(sessionId, deviceId, nonce);
        var signature = key.SignData(
            message,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);

        return new SessionDeviceProof(
            sessionId,
            deviceId,
            nonce,
            Base64Url.Encode(signature));
    }
}
