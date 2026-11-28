using System.Globalization;
using System.Text;

namespace Fasx.Agent.Core.Pairing;

/// <summary>
/// Defines the exact bytes a mobile client signs to prove that it owns the
/// private key corresponding to the public key included in a pairing claim.
/// </summary>
public static class PairingProof
{
    public const string ProtocolLabel = "FASX-PAIRING-V1";

    public static byte[] CreateMessage(
        Guid pairingId,
        Guid deviceId,
        string nonce)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nonce);

        var canonical = string.Join(
            "\n",
            ProtocolLabel,
            pairingId.ToString("D", CultureInfo.InvariantCulture),
            deviceId.ToString("D", CultureInfo.InvariantCulture),
            nonce);

        return Encoding.UTF8.GetBytes(canonical);
    }
}
