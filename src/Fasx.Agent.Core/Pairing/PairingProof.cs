using System.Text;

namespace Fasx.Agent.Core.Pairing;

/// <summary>
/// Canonical pairing proof payload shared by the Windows and Android clients.
/// The phone signs these bytes with its P-256 private key; the Windows host
/// verifies the signature before showing the local approval prompt.
/// </summary>
public static class PairingProof
{
    public static byte[] CreatePayload(
        Guid pairingId,
        Guid deviceId,
        string displayName,
        ReadOnlySpan<byte> invitationSecret)
    {
        if (pairingId == Guid.Empty)
        {
            throw new ArgumentException("A pairing identifier is required.", nameof(pairingId));
        }

        if (deviceId == Guid.Empty)
        {
            throw new ArgumentException("A device identifier is required.", nameof(deviceId));
        }

        if (string.IsNullOrWhiteSpace(displayName) ||
            displayName != displayName.Trim() ||
            displayName.Length > 64 ||
            displayName.Any(char.IsControl))
        {
            throw new ArgumentException("The display name is not canonical.", nameof(displayName));
        }

        if (invitationSecret.Length != 32)
        {
            throw new ArgumentException("The invitation secret must be exactly 32 bytes.", nameof(invitationSecret));
        }

        var secret = Convert.ToBase64String(invitationSecret)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var canonical = string.Join(
            "\n",
            "FASX-PAIRING-REQUEST-V1",
            pairingId.ToString("D").ToLowerInvariant(),
            deviceId.ToString("D").ToLowerInvariant(),
            displayName,
            secret);

        return Encoding.UTF8.GetBytes(canonical);
    }
}
