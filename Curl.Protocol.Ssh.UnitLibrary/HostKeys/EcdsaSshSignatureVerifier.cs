using System.Security.Cryptography;
using Curl.Protocol.Ssh.KeyExchange;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// <c>ecdsa-sha2-nistp256</c>, <c>-nistp384</c> and <c>-nistp521</c> (RFC 5656 section
/// 3.1): ECDSA over H on a NIST curve, from the BCL's <see cref="ECDsa" />.
/// </summary>
/// <param name="curve">The curve and its hash.</param>
internal sealed class EcdsaSshSignatureVerifier(SshNistCurve curve) : ISshSignatureVerifier
{
    private readonly string name = "ecdsa-sha2-" + curve.Identifier;

    /// <inheritdoc />
    public bool Verify(ReadOnlyMemory<byte> hostKey, ReadOnlyMemory<byte> signature, byte[] exchangeHash)
    {
        SshWireReader key = SshKeyBlobReader.Open(hostKey, name);
        if (key.ReadName() != curve.Identifier)
        {
            throw new InvalidDataException($"The SSH server's {name} host key names another curve.");
        }

        ECParameters publicKey = curve.DecodePublicPoint(key.ReadString().Span);
        SshWireReader signatureValues = new(SshKeyBlobReader.ReadSignature(signature, name));
        byte[] r = signatureValues.ReadMpint().ToArray();
        byte[] s = signatureValues.ReadMpint().ToArray();
        if (r.Length > curve.FieldLength || s.Length > curve.FieldLength)
        {
            return false;
        }

        byte[] fixedWidth = new byte[2 * curve.FieldLength];
        r.CopyTo(fixedWidth, curve.FieldLength - r.Length);
        s.CopyTo(fixedWidth, fixedWidth.Length - s.Length);
        using ECDsa ecdsa = ECDsa.Create(publicKey);
        return ecdsa.VerifyData(exchangeHash, fixedWidth, curve.HashAlgorithm, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }
}
