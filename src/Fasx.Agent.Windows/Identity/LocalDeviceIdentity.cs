using System.Security.Cryptography;

namespace Fasx.Agent.Windows.Identity;

/// <summary>
/// A local device identity. The private signing key remains inside this object
/// and is never exposed as bytes or a string.
/// </summary>
public sealed class LocalDeviceIdentity : IDisposable
{
    private ECDsa? _signingKey;

    internal LocalDeviceIdentity(
        Guid deviceId,
        string publicKeyBase64,
        DateTimeOffset createdAtUtc,
        ECDsa signingKey)
    {
        DeviceId = deviceId;
        PublicKeyBase64 = publicKeyBase64;
        CreatedAtUtc = createdAtUtc;
        _signingKey = signingKey;
    }

    public Guid DeviceId { get; }

    /// <summary>
    /// SubjectPublicKeyInfo-encoded P-256 public key. This is safe to share
    /// during a locally approved pairing ceremony.
    /// </summary>
    public string PublicKeyBase64 { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>
    /// Signs protocol data using SHA-256 with ECDSA. The caller is responsible
    /// for canonicalizing and versioning the payload before signing.
    /// </summary>
    public byte[] Sign(ReadOnlySpan<byte> data)
    {
        var signingKey = Volatile.Read(ref _signingKey);
        if (signingKey is null)
        {
            throw new ObjectDisposedException(nameof(LocalDeviceIdentity));
        }

        if (data.IsEmpty || data.Length > 64 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(data), "Signing payload must be between 1 byte and 64 KiB.");
        }

        return signingKey.SignData(data, HashAlgorithmName.SHA256);
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _signingKey, null)?.Dispose();
    }
}
