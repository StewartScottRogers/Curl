using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// An RSA user key: <c>ssh-rsa</c> blobs (RFC 4253 section 6.6), signing with PKCS #1
/// v1.5 over SHA-512, SHA-256 or SHA-1 as <c>rsa-sha2-512</c>, <c>rsa-sha2-256</c> (RFC 8332)
/// or <c>ssh-rsa</c>.
/// </summary>
internal sealed class RsaSshPrivateKey : SshPrivateKey
{
    /// <summary>The key type.</summary>
    internal const string RsaKeyType = "ssh-rsa";

    private readonly RSAParameters parameters;

    private RsaSshPrivateKey(RSAParameters parameters, byte[] pkcs1Encoding)
    {
        (this.parameters, Pkcs1Encoding) = (parameters, pkcs1Encoding);
        SshWireWriter blob = new();
        blob.WriteString(Encoding.ASCII.GetBytes(RsaKeyType));
        blob.WriteMpint(parameters.Exponent);
        blob.WriteMpint(parameters.Modulus);
        PublicKeyBlob = blob.ToArray();
    }

    /// <summary>
    /// Gets the signature algorithms an RSA key signs with, in libssh2's order of
    /// preference: the first the server's <c>server-sig-algs</c> names is used.
    /// </summary>
    internal static IReadOnlyList<string> SignatureAlgorithms { get; } = ["rsa-sha2-512", "rsa-sha2-256", RsaKeyType];

    /// <inheritdoc />
    internal override string KeyType => RsaKeyType;

    /// <inheritdoc />
    internal override byte[] PublicKeyBlob { get; }

    /// <summary>
    /// Reads a PKCS #1 <c>RSAPrivateKey</c> (RFC 8017 appendix A.1.2).
    /// </summary>
    /// <param name="der">The DER encoding.</param>
    /// <returns>The key.</returns>
    /// <exception cref="CryptographicException">The encoding is not an RSA private key.</exception>
    internal static RsaSshPrivateKey FromPkcs1(ReadOnlySpan<byte> der)
    {
        using RSA rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(der, out _);
        return new RsaSshPrivateKey(rsa.ExportParameters(includePrivateParameters: true), der.ToArray());
    }

    /// <summary>
    /// Builds the key from the six integers an <c>openssh-key-v1</c> file holds, computing
    /// the two CRT exponents it leaves out.
    /// </summary>
    /// <param name="modulus">n, unsigned big-endian.</param>
    /// <param name="exponent">e.</param>
    /// <param name="privateExponent">d.</param>
    /// <param name="coefficient">q^-1 mod p.</param>
    /// <param name="prime1">p.</param>
    /// <param name="prime2">q.</param>
    /// <returns>The key.</returns>
    /// <exception cref="CryptographicException">The integers do not form an RSA key.</exception>
    internal static RsaSshPrivateKey FromComponents(
        ReadOnlySpan<byte> modulus,
        ReadOnlySpan<byte> exponent,
        ReadOnlySpan<byte> privateExponent,
        ReadOnlySpan<byte> coefficient,
        ReadOnlySpan<byte> prime1,
        ReadOnlySpan<byte> prime2)
    {
        System.Numerics.BigInteger d = Unsigned(privateExponent);
        System.Numerics.BigInteger p = Unsigned(prime1);
        System.Numerics.BigInteger q = Unsigned(prime2);
        if (p <= 1 || q <= 1)
        {
            throw new CryptographicException("An RSA prime must be greater than one.");
        }

        // Windows' RSA import refuses a zero coefficient with CryptographicException, but
        // OpenSSL's throws a subclass of it; refuse it here so every platform throws the same.
        if (Unsigned(coefficient).IsZero)
        {
            throw new CryptographicException("The RSA coefficient must not be zero.");
        }

        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(0);
            writer.WriteIntegerUnsigned(Integer(modulus));
            writer.WriteIntegerUnsigned(Integer(exponent));
            writer.WriteIntegerUnsigned(Integer(privateExponent));
            writer.WriteIntegerUnsigned(Integer(prime1));
            writer.WriteIntegerUnsigned(Integer(prime2));
            writer.WriteInteger(d % (p - 1));
            writer.WriteInteger(d % (q - 1));
            writer.WriteIntegerUnsigned(Integer(coefficient));
        }

        return FromPkcs1(writer.Encode());
    }

    /// <summary>
    /// Gets the PKCS #1 <c>RSAPrivateKey</c> encoding the key was imported from, before the
    /// platform's RSA read it: for a key built by <see cref="FromComponents" />, the CRT
    /// exponents as computed here, which some platforms recompute on import.
    /// </summary>
    internal byte[] Pkcs1Encoding { get; }

    /// <inheritdoc />
    private protected override byte[] SignRaw(string algorithm, byte[] data)
    {
        HashAlgorithmName hash = algorithm switch
        {
            "rsa-sha2-512" => HashAlgorithmName.SHA512,
            "rsa-sha2-256" => HashAlgorithmName.SHA256,
            _ => HashAlgorithmName.SHA1,
        };
        using RSA rsa = RSA.Create(parameters);
        return rsa.SignData(data, hash, RSASignaturePadding.Pkcs1);
    }

    private static System.Numerics.BigInteger Unsigned(ReadOnlySpan<byte> magnitude) => new(magnitude, isUnsigned: true, isBigEndian: true);

    // WriteIntegerUnsigned wants at least one byte and no redundant leading zeros.
    private static byte[] Integer(ReadOnlySpan<byte> magnitude)
    {
        ReadOnlySpan<byte> trimmed = magnitude.TrimStart((byte)0);
        return trimmed.IsEmpty ? [0] : trimmed.ToArray();
    }
}
