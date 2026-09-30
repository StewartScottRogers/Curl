using System.Formats.Asn1;
using System.Security.Cryptography;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Decodes the DER private keys of PEM key files: PKCS #8 <c>PrivateKeyInfo</c>
/// (<c>PRIVATE KEY</c>, RFC 5958) for RSA, ECDSA, DSA and Ed25519 (RFC 8410, the seed as
/// <c>CurvePrivateKey</c>), OpenSSL's traditional DSA key
/// (<c>DSA PRIVATE KEY</c>) and SEC 1's <c>ECPrivateKey</c> (<c>EC PRIVATE KEY</c>, RFC
/// 5915). PKCS #1 RSA is <see cref="RsaSshPrivateKey.FromPkcs1" />'s.
/// </summary>
internal static class Asn1PrivateKeyDecoder
{
    private const string RsaEncryption = "1.2.840.113549.1.1.1";

    private const string EcPublicKey = "1.2.840.10045.2.1";

    private const string Dsa = "1.2.840.10040.4.1";

    private const string Ed25519 = "1.3.101.112";

    private static readonly Asn1Tag CurveParametersTag = new(TagClass.ContextSpecific, 0);

    private static readonly Asn1Tag PublicKeyTag = new(TagClass.ContextSpecific, 1);

    /// <summary>
    /// Decodes a PKCS #8 <c>PrivateKeyInfo</c>.
    /// </summary>
    /// <param name="der">The DER encoding.</param>
    /// <returns>
    /// The key, or <see langword="null" /> for an algorithm other than RSA, ECDSA, DSA and
    /// Ed25519 (RFC 8410), or a curve other than the three NIST curves.
    /// </returns>
    /// <exception cref="AsnContentException">The structure is malformed.</exception>
    /// <exception cref="CryptographicException">The key's values are invalid.</exception>
    /// <exception cref="ArgumentException">A DSA key's values are outside what the signer accepts.</exception>
    internal static SshPrivateKey? ReadPkcs8(byte[] der)
    {
        AsnReader info = new AsnReader(der, AsnEncodingRules.DER).ReadSequence();
        info.ReadInteger();
        AsnReader algorithm = info.ReadSequence();
        string algorithmOid = algorithm.ReadObjectIdentifier();
        byte[] privateKey = info.ReadOctetString();
        return algorithmOid switch
        {
            RsaEncryption => RsaSshPrivateKey.FromPkcs1(privateKey),
            EcPublicKey => ReadEcPrivateKey(privateKey, algorithm.ReadObjectIdentifier()),
            Dsa => ReadPkcs8Dsa(algorithm.ReadSequence(), privateKey),
            Ed25519 => Ed25519SshPrivateKey.FromSeed(new AsnReader(privateKey, AsnEncodingRules.DER).ReadOctetString()),
            _ => null,
        };
    }

    /// <summary>
    /// Decodes OpenSSL's traditional DSA key: version, p, q, g, y and x.
    /// </summary>
    /// <param name="der">The DER encoding.</param>
    /// <returns>The key.</returns>
    /// <exception cref="AsnContentException">The structure is malformed.</exception>
    /// <exception cref="ArgumentException">The values are outside what the signer accepts.</exception>
    internal static DsaSshPrivateKey ReadDsa(byte[] der)
    {
        AsnReader key = new AsnReader(der, AsnEncodingRules.DER).ReadSequence();
        key.ReadInteger();
        byte[] prime = ReadUnsigned(key);
        byte[] subprime = ReadUnsigned(key);
        byte[] generator = ReadUnsigned(key);
        byte[] publicKey = ReadUnsigned(key);
        return DsaSshPrivateKey.Create(prime, subprime, generator, publicKey, ReadUnsigned(key));
    }

    /// <summary>
    /// Decodes a SEC 1 <c>ECPrivateKey</c>: version, d, then the optional curve
    /// <c>[0]</c> and public point <c>[1]</c>.
    /// </summary>
    /// <param name="der">The DER encoding.</param>
    /// <param name="curveOid">The curve PKCS #8's algorithm names, or <see langword="null" /> when the key must name its own.</param>
    /// <returns>The key, or <see langword="null" /> for a curve other than the three NIST curves.</returns>
    /// <exception cref="AsnContentException">The structure is malformed.</exception>
    /// <exception cref="CryptographicException">No curve is named, or the values are invalid.</exception>
    internal static EcdsaSshPrivateKey? ReadEcPrivateKey(byte[] der, string? curveOid)
    {
        AsnReader key = new AsnReader(der, AsnEncodingRules.DER).ReadSequence();
        key.ReadInteger();
        byte[] privateKey = key.ReadOctetString();
        string? namedCurve = key.HasData && key.PeekTag().HasSameClassAndValue(CurveParametersTag)
            ? key.ReadSequence(CurveParametersTag).ReadObjectIdentifier()
            : null;
        byte[]? publicPoint = key.HasData ? key.ReadSequence(PublicKeyTag).ReadBitString(out _) : null;
        string curve = namedCurve ?? curveOid ?? throw new CryptographicException("The EC private key names no curve.");
        return EcdsaSshPrivateKey.FromCurveOid(curve, privateKey, publicPoint);
    }

    // Dss-Parms (p, q, g) in the algorithm; the private key is x alone.
    private static DsaSshPrivateKey ReadPkcs8Dsa(AsnReader parameters, byte[] privateKey)
    {
        byte[] prime = ReadUnsigned(parameters);
        byte[] subprime = ReadUnsigned(parameters);
        byte[] generator = ReadUnsigned(parameters);
        return DsaSshPrivateKey.Create(prime, subprime, generator, null, ReadUnsigned(new AsnReader(privateKey, AsnEncodingRules.DER)));
    }

    private static byte[] ReadUnsigned(AsnReader reader) => reader.ReadIntegerBytes().Span.TrimStart((byte)0).ToArray();
}
