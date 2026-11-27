using System.Security.Cryptography;
using System.Text.Json;
using Fasx.Agent.Core.Pairing;

namespace Fasx.Agent.Windows.Storage;

/// <summary>
/// Stores trusted device public identities encrypted for the current Windows user.
/// Private device keys never leave the phone. Pairing invitations are not persisted.
/// </summary>
public sealed class DpapiPairedDeviceStore : IPairedDeviceStore
{
    private const int MaximumStoreBytes = 1024 * 1024;
    private static readonly byte[] Entropy = "FASX-PAIRED-DEVICES-V1"u8.ToArray();
    private readonly string _storePath;

    public DpapiPairedDeviceStore(string? storePath = null)
    {
        _storePath = storePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FASX",
            "paired-devices.dat");
    }

    public IReadOnlyList<PairedDevice> Load()
    {
        if (!File.Exists(_storePath))
        {
            return [];
        }

        var encrypted = File.ReadAllBytes(_storePath);
        if (encrypted.Length is 0 or > MaximumStoreBytes)
        {
            throw new InvalidDataException("The FASX paired-device store has an invalid size.");
        }

        byte[]? plaintext = null;
        try
        {
            plaintext = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            var devices = JsonSerializer.Deserialize<List<PairedDevice>>(plaintext);
            return devices ?? [];
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encrypted);
            if (plaintext is not null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
    }

    public void Save(IReadOnlyCollection<PairedDevice> devices)
    {
        ArgumentNullException.ThrowIfNull(devices);

        var directory = Path.GetDirectoryName(_storePath)
            ?? throw new InvalidOperationException("FASX device store directory is unavailable.");
        Directory.CreateDirectory(directory);

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(devices);
        byte[]? encrypted = null;
        var temporaryPath = _storePath + ".tmp";

        try
        {
            encrypted = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(temporaryPath, encrypted);
            File.Move(temporaryPath, _storePath, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            if (encrypted is not null)
            {
                CryptographicOperations.ZeroMemory(encrypted);
            }

            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
