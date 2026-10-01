using System.Formats.Asn1;
using System.Security.Cryptography;

using Curl.Cryptography;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Reads the PKCS #8 private keys the BCL's <see cref="System.Security.Cryptography.X509Certificates.X509Certificate2" />
/// cannot hold on every platform - Ed25519 and Ed448 (RFC 8410) and ML-DSA-44, ML-DSA-65 and
/// ML-DSA-87 (RFC 9881, in its <c>seed</c>, <c>expandedKey</c> and <c>both</c> forms) - into
/// the hand-built TLS client's signing keys, and refuses one that is malformed, of another
/// algorithm than the certificate's, or not the private half of the certificate's public key,
/// as OpenSSL does (ADR-0301).
/// </summary>
internal static class HandBuiltPrivateKeyReader
{
    /// <summary>The Ed25519 algorithm identifier, RFC 8410 section 3.</summary>
    public const string Ed25519Oid = "1.3.101.112";

    /// <summary>The Ed448 algorithm identifier, RFC 8410 section 3.</summary>
    public const string Ed448Oid = "1.3.101.113";

    /// <summary>The ML-DSA-44 algorithm identifier, RFC 9881 section 2.</summary>
    public const string MlDsa44Oid = "2.16.840.1.101.3.4.3.17";

    /// <summary>The ML-DSA-65 algorithm identifier, RFC 9881 section 2.</summary>
    public const string MlDsa65Oid = "2.16.840.1.101.3.4.3.18";

    /// <summary>The ML-DSA-87 algorithm identifier, RFC 9881 section 2.</summary>
    public const string MlDsa87Oid = "2.16.840.1.101.3.4.3.19";

    private static readonly Asn1Tag MlDsaSeedTag = new(TagClass.ContextSpecific, 0);

    // Each algorithm's reader of the PrivateKeyInfo's privateKey octets, given the
    // certificate's public key.
    private static readonly Dictionary<string, Func<byte[], byte[], TlsSigningKey>> KeyReaders = new()
    {
        [Ed25519Oid] = (privateKey, publicKey) => ReadEdwards(privateKey, publicKey, Ed25519.PublicKeySize, Ed25519.ComputePublicKey, seed => new Ed25519TlsSigningKey(seed)),
        [Ed448Oid] = (privateKey, publicKey) => ReadEdwards(privateKey, publicKey, Ed448.PublicKeySize, Ed448.ComputePublicKey, seed => new Ed448TlsSigningKey(seed)),
        [MlDsa44Oid] = (privateKey, publicKey) => ReadMlDsa(MlDsaParameterSet.MlDsa44, privateKey, publicKey),
        [MlDsa65Oid] = (privateKey, publicKey) => ReadMlDsa(MlDsaParameterSet.MlDsa65, privateKey, publicKey),
        [MlDsa87Oid] = (privateKey, publicKey) => ReadMlDsa(MlDsaParameterSet.MlDsa87, privateKey, publicKey),
    };

    private delegate void PublicKeyDerivation(ReadOnlySpan<byte> privateKey, Span<byte> publicKey);

    /// <summary>Returns whether a certificate whose public key is of this algorithm has its key read here.</summary>
    /// <param name="keyAlgorithmOid">The certificate's public key algorithm.</param>
    /// <returns><see langword="true" /> for Ed25519, Ed448 and the three ML-DSA parameter sets.</returns>
    public static bool Reads(string? keyAlgorithmOid) =>
        keyAlgorithmOid is not null && KeyReaders.ContainsKey(keyAlgorithmOid);

    /// <summary>Reads a PKCS #8 <c>PrivateKeyInfo</c> into the signing key for a certificate's public key.</summary>
    /// <param name="privateKeyInfo">The DER (or BER) of the <c>PrivateKeyInfo</c>.</param>
    /// <param name="keyAlgorithmOid">The certificate's public key algorithm, one <see cref="Reads" /> accepts.</param>
    /// <param name="certificatePublicKey">The certificate's <c>subjectPublicKey</c> bits.</param>
    /// <returns>The signing key.</returns>
    /// <exception cref="CryptographicException">The key is malformed, of another algorithm, or not the certificate's.</exception>
    public static TlsSigningKey Read(byte[] privateKeyInfo, string keyAlgorithmOid, byte[] certificatePublicKey)
    {
        try
        {
            var (algorithm, privateKey) = ReadPrivateKeyInfo(privateKeyInfo);
            RequireThat(algorithm == keyAlgorithmOid, "The private key is not of the certificate's algorithm.");
            return KeyReaders[algorithm](privateKey, certificatePublicKey);
        }
        catch (Exception exception) when (exception is AsnContentException or ArgumentException)
        {
            throw new CryptographicException("The private key is malformed.", exception);
        }
    }

    private static (string Algorithm, byte[] PrivateKey) ReadPrivateKeyInfo(byte[] privateKeyInfo)
    {
        var reader = new AsnReader(privateKeyInfo, AsnEncodingRules.BER);
        var sequence = reader.ReadSequence();
        reader.ThrowIfNotEmpty();
        sequence.ReadInteger();
        var algorithmIdentifier = sequence.ReadSequence();
        var algorithm = algorithmIdentifier.ReadObjectIdentifier();
        algorithmIdentifier.ThrowIfNotEmpty();
        return (algorithm, sequence.ReadOctetString());
    }

    // RFC 8410 section 7: the private key is a CurvePrivateKey, an OCTET STRING holding the seed.
    private static TlsSigningKey ReadEdwards(
        byte[] privateKey,
        byte[] certificatePublicKey,
        int publicKeySize,
        PublicKeyDerivation derivePublicKey,
        Func<byte[], TlsSigningKey> createSigningKey)
    {
        var reader = new AsnReader(privateKey, AsnEncodingRules.BER);
        var seed = reader.ReadOctetString();
        reader.ThrowIfNotEmpty();
        var signingKey = createSigningKey(seed);
        var publicKey = new byte[publicKeySize];
        derivePublicKey(seed, publicKey);
        CryptographicOperations.ZeroMemory(seed);
        RequireThat(publicKey.AsSpan().SequenceEqual(certificatePublicKey), "The private key is not the certificate's.");
        return signingKey;
    }

    // RFC 9881 section 6: ML-DSA-PrivateKey ::= CHOICE { seed [0] OCTET STRING,
    // expandedKey OCTET STRING, both SEQUENCE { seed, expandedKey } }.
    private static TlsSigningKey ReadMlDsa(MlDsaParameterSet parameterSet, byte[] privateKey, byte[] certificatePublicKey)
    {
        var reader = new AsnReader(privateKey, AsnEncodingRules.BER);
        var tag = reader.PeekTag();
        var key = tag.HasSameClassAndValue(MlDsaSeedTag) ? MlDsa.GenerateKey(parameterSet, reader.ReadOctetString(MlDsaSeedTag))
            : tag.HasSameClassAndValue(Asn1Tag.PrimitiveOctetString) ? MlDsa.ImportPrivateKey(parameterSet, reader.ReadOctetString())
            : ReadMlDsaBoth(parameterSet, reader.ReadSequence());
        try
        {
            reader.ThrowIfNotEmpty();
            var publicKey = new byte[MlDsa.GetPublicKeySize(parameterSet)];
            key.ExportPublicKey(publicKey);
            RequireThat(publicKey.AsSpan().SequenceEqual(certificatePublicKey), "The private key is not the certificate's.");
            return new MlDsaTlsSigningKey(key);
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    // The expanded key must be the one the seed generates.
    private static MlDsa ReadMlDsaBoth(MlDsaParameterSet parameterSet, AsnReader both)
    {
        var seed = both.ReadOctetString();
        var expandedKey = both.ReadOctetString();
        both.ThrowIfNotEmpty();
        var key = MlDsa.GenerateKey(parameterSet, seed);
        var generated = new byte[MlDsa.GetPrivateKeySize(parameterSet)];
        key.ExportPrivateKey(generated);
        var matches = CryptographicOperations.FixedTimeEquals(generated, expandedKey);
        CryptographicOperations.ZeroMemory(generated);
        CryptographicOperations.ZeroMemory(expandedKey);
        if (!matches)
        {
            key.Dispose();
            throw new CryptographicException("The private key's expandedKey is not the one its seed generates.");
        }

        return key;
    }

    private static void RequireThat(bool condition, string message)
    {
        if (!condition)
        {
            throw new CryptographicException(message);
        }
    }
}
