using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Fasx.Agent.Windows.Identity;

/// <summary>
/// Creates and loads a persistent P-256 device identity.
/// The private key is protected at rest by Windows DPAPI for the current user.
/// This store performs no network activity and does not enable remote access.
/// </summary>
public sealed class WindowsDeviceIdentityStore
{
    private const int FileFormatVersion = 1;
    private const int MaximumPrivateKeyLength = 4096;
    private const string IdentityFileName = "device-identity.dpapi";

    private static readonly object ProcessLock = new();
    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes("FASX.Windows.DeviceIdentity.v1");

    private readonly string _identityDirectory;
    private readonly string _identityPath;

    public WindowsDeviceIdentityStore(string? identityDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(identityDirectory))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                throw new InvalidOperationException("The Windows local application-data folder is unavailable.");
            }

            identityDirectory = Path.Combine(localAppData, "FASX", "Identity");
        }

        _identityDirectory = identityDirectory;
        _identityPath = Path.Combine(_identityDirectory, IdentityFileName);
    }

    /// <summary>
    /// Loads the existing identity or creates it once. If the existing identity
    /// cannot be decrypted or parsed, this method throws instead of silently
    /// generating a replacement that would invalidate future device trust.
    /// </summary>
    public LocalDeviceIdentity LoadOrCreate()
    {
        lock (ProcessLock)
        {
            Directory.CreateDirectory(_identityDirectory);

            if (File.Exists(_identityPath))
            {
                return LoadExisting();
            }

            return CreateNew();
        }
    }

    private LocalDeviceIdentity CreateNew()
    {
        var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[]? privateKey = null;
        byte[]? publicKey = null;
        byte[]? plaintext = null;
        byte[]? protectedBytes = null;
        var ownershipTransferred = false;

        try
        {
            privateKey = signingKey.ExportPkcs8PrivateKey();
            publicKey = signingKey.ExportSubjectPublicKeyInfo();

            var deviceId = Guid.NewGuid();
            var createdAtUtc = DateTimeOffset.UtcNow;
            plaintext = Serialize(deviceId, createdAtUtc, privateKey);
            protectedBytes = ProtectedData.Protect(
                plaintext,
                Entropy,
                DataProtectionScope.CurrentUser);

            // Another FASX process may have created the identity while this
            // process was generating its key. Use the persisted identity.
            if (!TryWriteNewIdentityFile(protectedBytes))
            {
                return LoadExisting();
            }

            var identity = new LocalDeviceIdentity(
                deviceId,
                Convert.ToBase64String(publicKey),
                createdAtUtc,
                signingKey);

            ownershipTransferred = true;
            return identity;
        }
        finally
        {
            if (!ownershipTransferred)
            {
                signingKey.Dispose();
            }

            Clear(privateKey);
            Clear(publicKey);
            Clear(plaintext);
            Clear(protectedBytes);
        }
    }

    private LocalDeviceIdentity LoadExisting()
    {
        byte[] encrypted;
        try
        {
            encrypted = File.ReadAllBytes(_identityPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                "The FASX device identity file could not be read. No replacement identity was created.",
                exception);
        }

        if (encrypted.Length is < 32 or > 16_384)
        {
            Clear(encrypted);
            throw new InvalidDataException(
                "The FASX device identity file has an invalid size. No replacement identity was created.");
        }

        byte[] plaintext;
        try
        {
            plaintext = ProtectedData.Unprotect(
                encrypted,
                Entropy,
                DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException(
                "FASX could not decrypt its device identity for this Windows account. " +
                "The existing identity was left untouched.",
                exception);
        }
        finally
        {
            Clear(encrypted);
        }

        try
        {
            return Deserialize(plaintext);
        }
        catch (Exception exception) when (
            exception is IOException or
            ArgumentException or
            CryptographicException or
            OverflowException)
        {
            throw new InvalidDataException(
                "The FASX device identity data is invalid. No replacement identity was created.",
                exception);
        }
        finally
        {
            Clear(plaintext);
        }
    }

    private bool TryWriteNewIdentityFile(byte[] protectedBytes)
    {
        var temporaryPath = $"{_identityPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(protectedBytes);
                stream.Flush(flushToDisk: true);
            }

            try
            {
                File.Move(temporaryPath, _identityPath, overwrite: false);
                return true;
            }
            catch (IOException) when (File.Exists(_identityPath))
            {
                return false;
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static byte[] Serialize(
        Guid deviceId,
        DateTimeOffset createdAtUtc,
        byte[] privateKey)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(FileFormatVersion);
            writer.Write(deviceId.ToByteArray());
            writer.Write(createdAtUtc.UtcDateTime.Ticks);
            writer.Write(privateKey.Length);
            writer.Write(privateKey);
        }

        return stream.ToArray();
    }

    private static LocalDeviceIdentity Deserialize(byte[] plaintext)
    {
        using var stream = new MemoryStream(plaintext, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        var version = reader.ReadInt32();
        if (version != FileFormatVersion)
        {
            throw new InvalidDataException($"Unsupported FASX device identity format: {version}.");
        }

        var deviceIdBytes = reader.ReadBytes(16);
        if (deviceIdBytes.Length != 16)
        {
            throw new InvalidDataException("The device identifier is incomplete.");
        }

        var deviceId = new Guid(deviceIdBytes);
        if (deviceId == Guid.Empty)
        {
            throw new InvalidDataException("The device identifier is invalid.");
        }

        var createdAtTicks = reader.ReadInt64();
        var createdAtUtc = new DateTimeOffset(createdAtTicks, TimeSpan.Zero);

        var privateKeyLength = reader.ReadInt32();
        if (privateKeyLength is <= 0 or > MaximumPrivateKeyLength ||
            stream.Length - stream.Position != privateKeyLength)
        {
            throw new InvalidDataException("The private-key payload has an invalid length.");
        }

        var privateKey = reader.ReadBytes(privateKeyLength);
        if (privateKey.Length != privateKeyLength)
        {
            Clear(privateKey);
            throw new InvalidDataException("The private-key payload is incomplete.");
        }

        var signingKey = ECDsa.Create();
        byte[]? publicKey = null;
        var ownershipTransferred = false;

        try
        {
            signingKey.ImportPkcs8PrivateKey(privateKey, out var bytesRead);
            if (bytesRead != privateKey.Length || signingKey.KeySize != 256)
            {
                throw new InvalidDataException("The stored device key is not a valid P-256 key.");
            }

            var parameters = signingKey.ExportParameters(includePrivateParameters: false);
            if (!string.Equals(
                    parameters.Curve.Oid.Value,
                    "1.2.840.10045.3.1.7",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("The stored device key is not on the P-256 curve.");
            }

            publicKey = signingKey.ExportSubjectPublicKeyInfo();
            var identity = new LocalDeviceIdentity(
                deviceId,
                Convert.ToBase64String(publicKey),
                createdAtUtc,
                signingKey);

            ownershipTransferred = true;
            return identity;
        }
        finally
        {
            if (!ownershipTransferred)
            {
                signingKey.Dispose();
            }

            Clear(privateKey);
            Clear(publicKey);
        }
    }

    private static void Clear(byte[]? value)
    {
        if (value is not null)
        {
            CryptographicOperations.ZeroMemory(value);
        }
    }
}
