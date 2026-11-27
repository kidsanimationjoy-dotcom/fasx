using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fasx.Agent.Core.Pairing;

namespace Fasx.Agent.Windows.Identity;

/// <summary>
/// Stores public metadata for trusted controller devices in a DPAPI-protected file.
/// Phone private keys never leave Android Keystore.
/// </summary>
public sealed class WindowsTrustedDeviceStore
{
    private const int FormatVersion = 1;
    private const int MaximumRegistryBytes = 1024 * 1024;
    private const int MaximumDevices = 256;
    private static readonly object Sync = new();
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FASX.Windows.TrustedDevices.v1");
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly string _directory;
    private readonly string _path;

    public WindowsTrustedDeviceStore(string? directory = null)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localData))
            {
                throw new InvalidOperationException("The Windows local application-data folder is unavailable.");
            }

            directory = Path.Combine(localData, "FASX", "TrustedDevices");
        }

        _directory = directory;
        _path = Path.Combine(directory, "trusted-devices.dpapi");
    }

    public IReadOnlyList<PairedDevice> GetForHost(Guid hostDeviceId)
    {
        if (hostDeviceId == Guid.Empty)
        {
            throw new ArgumentException("A host device ID is required.", nameof(hostDeviceId));
        }

        lock (Sync)
        {
            return LoadAll()
                .Where(device => device.HostDeviceId == hostDeviceId)
                .OrderBy(device => device.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public void AddOrUpdate(PairedDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        ValidateDevice(device);

        lock (Sync)
        {
            var devices = LoadAll();
            var index = devices.FindIndex(existing =>
                existing.HostDeviceId == device.HostDeviceId &&
                existing.DeviceId == device.DeviceId);

            if (index >= 0)
            {
                devices[index] = device;
            }
            else
            {
                if (devices.Count >= MaximumDevices)
                {
                    throw new InvalidOperationException($"FASX supports up to {MaximumDevices} trusted devices.");
                }

                devices.Add(device);
            }

            SaveAll(devices);
        }
    }

    public bool Revoke(Guid hostDeviceId, Guid deviceId)
    {
        if (hostDeviceId == Guid.Empty || deviceId == Guid.Empty)
        {
            return false;
        }

        lock (Sync)
        {
            var devices = LoadAll();
            var removed = devices.RemoveAll(device =>
                device.HostDeviceId == hostDeviceId &&
                device.DeviceId == deviceId);

            if (removed == 0)
            {
                return false;
            }

            SaveAll(devices);
            return true;
        }
    }

    private List<PairedDevice> LoadAll()
    {
        if (!File.Exists(_path))
        {
            return new List<PairedDevice>();
        }

        byte[] encrypted;
        try
        {
            encrypted = File.ReadAllBytes(_path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("FASX could not read the trusted-device registry.", exception);
        }

        if (encrypted.Length is < 32 or > MaximumRegistryBytes)
        {
            CryptographicOperations.ZeroMemory(encrypted);
            throw new InvalidDataException("The trusted-device registry size is invalid; the original file was not replaced.");
        }

        byte[] plaintext;
        try
        {
            plaintext = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException(
                "FASX could not decrypt the trusted-device registry for this Windows account. The original file was left untouched.",
                exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encrypted);
        }

        try
        {
            var document = JsonSerializer.Deserialize<StoreDocument>(plaintext, JsonOptions);
            if (document is null || document.Version != FormatVersion || document.Devices is null)
            {
                throw new InvalidDataException("The trusted-device registry format is invalid.");
            }

            if (document.Devices.Count > MaximumDevices)
            {
                throw new InvalidDataException("The trusted-device registry exceeds the supported device limit.");
            }

            var devices = document.Devices.Select(ToPairedDevice).ToList();
            if (devices.GroupBy(device => (device.HostDeviceId, device.DeviceId)).Any(group => group.Count() > 1))
            {
                throw new InvalidDataException("The trusted-device registry contains duplicate entries.");
            }

            return devices;
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or CryptographicException or InvalidOperationException)
        {
            throw new InvalidDataException("The trusted-device registry is invalid; the original file was left untouched.", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private void SaveAll(IReadOnlyList<PairedDevice> devices)
    {
        Directory.CreateDirectory(_directory);

        var document = new StoreDocument
        {
            Version = FormatVersion,
            Devices = devices.Select(FromPairedDevice).ToList()
        };

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        byte[]? encrypted = null;
        var temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";

        try
        {
            encrypted = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);
            if (encrypted.Length > MaximumRegistryBytes)
            {
                throw new InvalidOperationException("The trusted-device registry is too large to save.");
            }

            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            {
                stream.Write(encrypted);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _path, overwrite: true);
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

    private static void ValidateDevice(PairedDevice device)
    {
        if (device.HostDeviceId == Guid.Empty || device.DeviceId == Guid.Empty)
        {
            throw new ArgumentException("Host and controller device IDs are required.", nameof(device));
        }

        var name = device.DisplayName?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > 64 || name.Any(char.IsControl) ||
            !string.Equals(name, device.DisplayName, StringComparison.Ordinal))
        {
            throw new ArgumentException("The trusted-device display name is invalid or not trimmed.", nameof(device));
        }

        if (!IsP256PublicKey(device.PublicKeyBase64))
        {
            throw new ArgumentException("The trusted-device public key must be a P-256 SPKI key.", nameof(device));
        }
    }

    private static bool IsP256PublicKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 512)
        {
            return false;
        }

        byte[]? keyBytes = null;
        try
        {
            keyBytes = Convert.FromBase64String(value);
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(keyBytes, out var bytesRead);
            if (bytesRead != keyBytes.Length || key.KeySize != 256)
            {
                return false;
            }

            return string.Equals(
                key.ExportParameters(false).Curve.Oid.Value,
                "1.2.840.10045.3.1.7",
                StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException or ArgumentException)
        {
            return false;
        }
        finally
        {
            if (keyBytes is not null)
            {
                CryptographicOperations.ZeroMemory(keyBytes);
            }
        }
    }

    private static PairedDevice ToPairedDevice(StoredDevice device)
    {
        var paired = new PairedDevice(
            device.HostDeviceId,
            device.DeviceId,
            device.DisplayName,
            device.PublicKeyBase64,
            device.PairedAtUtc);
        ValidateDevice(paired);
        return paired;
    }

    private static StoredDevice FromPairedDevice(PairedDevice device) => new()
    {
        HostDeviceId = device.HostDeviceId,
        DeviceId = device.DeviceId,
        DisplayName = device.DisplayName,
        PublicKeyBase64 = device.PublicKeyBase64,
        PairedAtUtc = device.PairedAtUtc
    };

    private sealed class StoreDocument
    {
        public StoreDocument() { }
        public int Version { get; set; }
        public List<StoredDevice> Devices { get; set; } = new();
    }

    private sealed class StoredDevice
    {
        public StoredDevice() { }
        public Guid HostDeviceId { get; set; }
        public Guid DeviceId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string PublicKeyBase64 { get; set; } = string.Empty;
        public DateTimeOffset PairedAtUtc { get; set; }
    }
}
