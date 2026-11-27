using System.IO;
using System.Security.Cryptography;
using Fasx.Agent.Core.Pairing;
using Fasx.Agent.Windows.Identity;
using Xunit;

namespace Fasx.Agent.Core.Tests;

public sealed class WindowsTrustedDeviceStoreTests
{
    [Fact]
    public void TrustedDevicesPersistAcrossStoreInstances()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var hostId = Guid.NewGuid();
            var device = CreateDevice(hostId, Guid.NewGuid(), "My Phone");

            new WindowsTrustedDeviceStore(directory).AddOrUpdate(device);

            var restored = Assert.Single(new WindowsTrustedDeviceStore(directory).GetForHost(hostId));
            Assert.Equal(device.DeviceId, restored.DeviceId);
            Assert.Equal(device.PublicKeyBase64, restored.PublicKeyBase64);
            Assert.Equal(device.DisplayName, restored.DisplayName);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [Fact]
    public void DeviceRevocationPersistsAcrossStoreInstances()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var hostId = Guid.NewGuid();
            var device = CreateDevice(hostId, Guid.NewGuid(), "My Phone");
            var store = new WindowsTrustedDeviceStore(directory);
            store.AddOrUpdate(device);

            Assert.True(store.Revoke(hostId, device.DeviceId));
            Assert.Empty(new WindowsTrustedDeviceStore(directory).GetForHost(hostId));
            Assert.False(store.Revoke(hostId, device.DeviceId));
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [Fact]
    public void TrustedDevicesAreScopedToTheirHost()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var firstHost = Guid.NewGuid();
            var secondHost = Guid.NewGuid();
            var first = CreateDevice(firstHost, Guid.NewGuid(), "First");
            var second = CreateDevice(secondHost, Guid.NewGuid(), "Second");
            var store = new WindowsTrustedDeviceStore(directory);

            store.AddOrUpdate(first);
            store.AddOrUpdate(second);

            Assert.Equal(first.DeviceId, Assert.Single(store.GetForHost(firstHost)).DeviceId);
            Assert.Equal(second.DeviceId, Assert.Single(store.GetForHost(secondHost)).DeviceId);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [Fact]
    public void CorruptRegistryIsNotSilentlyOverwritten()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "trusted-devices.dpapi");
        var corrupted = new byte[] { 9, 8, 7, 6, 5, 4 };

        try
        {
            File.WriteAllBytes(path, corrupted);
            Assert.Throws<InvalidDataException>(
                () => new WindowsTrustedDeviceStore(directory).GetForHost(Guid.NewGuid()));
            Assert.Equal(corrupted, File.ReadAllBytes(path));
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [Fact]
    public void StoreRejectsNonP256KeysAndInvalidDisplayNames()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
            var invalidKey = Convert.ToBase64String(p384.ExportSubjectPublicKeyInfo());
            var badKeyDevice = new PairedDevice(
                Guid.NewGuid(), Guid.NewGuid(), "Phone", invalidKey, DateTimeOffset.UtcNow);

            Assert.Throws<ArgumentException>(
                () => new WindowsTrustedDeviceStore(directory).AddOrUpdate(badKeyDevice));

            using var p256 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var validKey = Convert.ToBase64String(p256.ExportSubjectPublicKeyInfo());
            var badNameDevice = new PairedDevice(
                Guid.NewGuid(), Guid.NewGuid(), "Bad\nName", validKey, DateTimeOffset.UtcNow);

            Assert.Throws<ArgumentException>(
                () => new WindowsTrustedDeviceStore(directory).AddOrUpdate(badNameDevice));
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    private static PairedDevice CreateDevice(Guid hostId, Guid deviceId, string name)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return new PairedDevice(
            hostId,
            deviceId,
            name,
            Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            DateTimeOffset.UtcNow);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "FASX-TrustedDevice-Tests", Guid.NewGuid().ToString("N"));
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
