using System.Security.Cryptography;
using Fasx.Agent.Core.Pairing;

namespace Fasx.Agent.Core.Sessions;

/// <summary>
/// Verifies proof of possession of the signing key held by an already-paired phone.
/// The proof authenticates a device for a session request; it does not authorize access.
/// </summary>
public sealed record SessionDeviceProof(
    Guid SessionId,
    Guid DeviceId,
    string Nonce,
    string Signature);

public static class SessionProofVerifier
{
    public const string ProtocolLabel = "FASX-SESSION-V1";

    public static byte[] CreateMessage(Guid sessionId, Guid deviceId, string nonce)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nonce);

        if (sessionId == Guid.Empty || deviceId == Guid.Empty || nonce.Length > 128)
        {
            throw new ArgumentException("Session proof fields are invalid.");
        }

        var canonical = string.Join(
            "\n",
            ProtocolLabel,
            sessionId.ToString("D"),
            deviceId.ToString("D"),
            nonce);

        return System.Text.Encoding.UTF8.GetBytes(canonical);
    }

    public static bool Verify(PairedDevice pairedDevice, SessionDeviceProof proof)
    {
        ArgumentNullException.ThrowIfNull(pairedDevice);
        ArgumentNullException.ThrowIfNull(proof);

        if (pairedDevice.DeviceId == Guid.Empty ||
            proof.DeviceId != pairedDevice.DeviceId ||
            proof.SessionId == Guid.Empty ||
            proof.Nonce.Length is 0 or > 128 ||
            proof.Signature.Length is 0 or > 256 ||
            !Base64Url.TryDecode(proof.Nonce, out var nonceBytes) ||
            nonceBytes.Length != 32 ||
            !Pairing.Base64Url.TryDecode(proof.Signature, out var signatureBytes) ||
            !Pairing.Base64Url.TryDecode(pairedDevice.PublicKeySpki, out var publicKeyBytes))
        {
            return false;
        }

        try
        {
            using var verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(publicKeyBytes, out var bytesRead);
            if (bytesRead != publicKeyBytes.Length || verifier.KeySize != 256)
            {
                return false;
            }

            var message = CreateMessage(proof.SessionId, proof.DeviceId, proof.Nonce);
            return verifier.VerifyData(
                message,
                signatureBytes,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (System.Formats.Asn1.AsnContentException)
        {
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nonceBytes);
            CryptographicOperations.ZeroMemory(signatureBytes);
        }
    }
}
