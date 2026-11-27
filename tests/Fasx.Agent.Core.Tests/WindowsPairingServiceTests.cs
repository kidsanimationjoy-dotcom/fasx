using System.Security.Cryptography;
using Fasx.Agent.Core.Pairing;
using Fasx.Agent.Windows.Identity;
using Fasx.Agent.Windows.Pairing;
using Xunit;

namespace Fasx.Agent.Core.Tests;

public sealed class WindowsPairingServiceTests
{
    [Fact]
    public void ApprovedPhoneIsPersistedAndCanBeRevokedAfterCoordinatorRestart()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            using var phoneKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var publicKey = Convert.ToBase64String(phoneKey.ExportSubjectPublicKeyInfo());
            var hostId = Guid.NewGuid();
            var phoneId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;

            var service = new WindowsPairingService(
                new PairingCoordinator(),
                new WindowsTrustedDeviceStore(directory));

            var offer = service.CreateOffer(hostId, now);
            var secretBytes = DecodeBase64Url(offer.Secret);
            var payload = PairingProof.CreatePayload(offer.PairingId, phoneId, "Test Phone", secretBytes);
            string proof;
            try
            {
                proof = Convert.ToBase64String(phoneKey.SignData(payload, HashAlgorithmName.SHA256));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secretBytes);
                CryptographicOperations.ZeroMemory(payload);
            }

            Assert.True(service.TryRequestPairing(
                offer.PairingId,
                offer.Secret,
                phoneId,
                "Test Phone",
                publicKey,
                proof,
                now,
                out _));

            var approved = service.Approve(offer.PairingId, hostId, now);
            Assert.NotNull(approved);

            // Simulate closing FASX and creating a new Windows service instance.
            var restartedService = new WindowsPairingService(
                new PairingCoordinator(),
                new WindowsTrustedDeviceStore(directory));

            var restored = Assert.Single(restartedService.GetTrustedDevices(hostId));
            Assert.Equal(phoneId, restored.DeviceId);
            Assert.Equal(publicKey, restored.PublicKeyBase64);

            Assert.True(restartedService.Revoke(hostId, phoneId));
            Assert.Empty(new WindowsTrustedDeviceStore(directory).GetForHost(hostId));
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        var padding = (4 - normalized.Length % 4) % 4;
        return Convert.FromBase64String(normalized + new string('=', padding));
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "FASX-Pairing-Service-Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
