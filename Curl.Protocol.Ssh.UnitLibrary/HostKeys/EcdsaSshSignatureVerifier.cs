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
        ECParameters publicKey = ReadPublicKey(SshKeyBlobReader.Open(hostKey, name), curve, name);
        byte[]? fixedWidth = ToFixedWidth(SshKeyBlobReader.ReadSignature(signature, name), curve);
        return fixedWidth is not null && VerifyData(publicKey, exchangeHash, fixedWidth, curve.HashAlgorithm);
    }

    /// <summary>
    /// Reads the curve name and public point an ECDSA key blob carries after its key type.
    /// </summary>
    /// <param name="key">The key blob, positioned after its key type.</param>
    /// <param name="curve">The curve the key must be on.</param>
    /// <param name="keyType">The key type, for the message.</param>
    /// <returns>The public key.</returns>
    /// <exception cref="InvalidDataException">The blob names another curve or is malformed.</exception>
    internal static ECParameters ReadPublicKey(SshWireReader key, SshNistCurve curve, string keyType)
    {
        if (key.ReadName() != curve.Identifier)
        {
            throw new InvalidDataException($"The SSH server's {keyType} host key names another curve.");
        }

        return curve.DecodePublicPoint(key.ReadString().Span);
    }

    /// <summary>
    /// Turns an SSH ECDSA signature, <c>mpint r</c> then <c>mpint s</c>, into the fixed-width
    /// <c>r || s</c> the BCL verifies.
    /// </summary>
    /// <param name="values">The two mpints.</param>
    /// <param name="curve">The curve.</param>
    /// <returns>The fixed-width signature, or <see langword="null" /> when a value is wider than the curve's field, which no valid signature is.</returns>
    /// <exception cref="InvalidDataException">The values are malformed.</exception>
    internal static byte[]? ToFixedWidth(ReadOnlyMemory<byte> values, SshNistCurve curve)
    {
        SshWireReader reader = new(values);
        byte[] r = reader.ReadMpint().ToArray();
        byte[] s = reader.ReadMpint().ToArray();
        if (r.Length > curve.FieldLength || s.Length > curve.FieldLength)
        {
            return null;
        }

        byte[] fixedWidth = new byte[2 * curve.FieldLength];
        r.CopyTo(fixedWidth, curve.FieldLength - r.Length);
        s.CopyTo(fixedWidth, fixedWidth.Length - s.Length);
        return fixedWidth;
    }

    /// <summary>
    /// Verifies a fixed-width ECDSA signature over <paramref name="data" />.
    /// </summary>
    /// <param name="publicKey">The public key.</param>
    /// <param name="data">The signed data.</param>
    /// <param name="fixedWidth">The signature, <c>r || s</c>.</param>
    /// <param name="hashAlgorithm">The hash the signature was made over.</param>
    /// <returns><see langword="true" /> when the signature is valid.</returns>
    internal static bool VerifyData(ECParameters publicKey, byte[] data, byte[] fixedWidth, HashAlgorithmName hashAlgorithm)
    {
        using ECDsa ecdsa = ECDsa.Create(publicKey);
        return ecdsa.VerifyData(data, fixedWidth, hashAlgorithm, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }
}
