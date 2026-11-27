using System.Security.Cryptography;
using System.Text;
using Fasx.Agent.Windows.Identity;
using Xunit;

namespace Fasx.Agent.Core.Tests;

public sealed class WindowsDeviceIdentityStoreTests
{
    [Fact]
    public void IdentityPersistsAndCanSignDataAfterReload()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var store = new WindowsDeviceIdentityStore(directory);
            using var first = store.LoadOrCreate();

            var payload = Encoding.UTF8.GetBytes("FASX device identity proof v1");
            var signature = first.Sign(payload);

            using var second = new WindowsDeviceIdentityStore(directory).LoadOrCreate();
            Assert.Equal(first.DeviceId, second.DeviceId);
            Assert.Equal(first.PublicKeyBase64, second.PublicKeyBase64);
            Assert.Equal(first.CreatedAtUtc, second.CreatedAtUtc);

            using var verifier = ECDsa.Create();
            var publicKey = Convert.FromBase64String(second.PublicKeyBase64);
            verifier.ImportSubjectPublicKeyInfo(publicKey, out var bytesRead);

            Assert.Equal(publicKey.Length, bytesRead);
            Assert.True(verifier.VerifyData(payload, signature, HashAlgorithmName.SHA256));
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [Fact]
    public void CorruptIdentityIsNotSilentlyReplaced()
    {
        var directory = CreateTemporaryDirectory();
        var identityPath = Path.Combine(directory, "device-identity.dpapi");
        var corruptBytes = new byte[] { 1, 2, 3, 4, 5, 6 };

        try
        {
            File.WriteAllBytes(identityPath, corruptBytes);

            var exception = Assert.Throws<InvalidDataException>(
                () => new WindowsDeviceIdentityStore(directory).LoadOrCreate());

            Assert.Contains("No replacement identity was created", exception.Message);
            Assert.Equal(corruptBytes, File.ReadAllBytes(identityPath));
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [Fact]
    public void DisposedIdentityCannotCreateNewSignatures()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var identity = new WindowsDeviceIdentityStore(directory).LoadOrCreate();
            identity.Dispose();

            Assert.Throws<ObjectDisposedException>(() =>
                identity.Sign(Encoding.UTF8.GetBytes("test")));
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "FASX-Identity-Tests", Guid.NewGuid().ToString("N"));
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
