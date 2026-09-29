using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// The public key of an X.509 certificate, read straight from its DER
/// <c>SubjectPublicKeyInfo</c> so that RSA-PSS and Ed25519 keys read the same on every
/// platform, and the CertificateVerify check against it (RFC 8446 section 4.4.3).
/// </summary>
/// <param name="AlgorithmOid">The key's algorithm OID.</param>
/// <param name="CurveOid">The named curve OID of an EC key, otherwise <see langword="null" />.</param>
/// <param name="KeyBits">The contents of the <c>subjectPublicKey</c> bit string.</param>
/// <param name="SubjectPublicKeyInfo">The whole DER <c>SubjectPublicKeyInfo</c>.</param>
public sealed record TlsCertificatePublicKey(string AlgorithmOid, string? CurveOid, byte[] KeyBits, byte[] SubjectPublicKeyInfo)
{
    private const int MinimumLegacyRsaModulusLength = 36 + 11;
    private const int MaximumRsaModulusLength = 16384 / 8;
    private const int MaximumRsaExponentBits = 64;

    private static readonly Asn1Tag VersionTag = new(TagClass.ContextSpecific, 0, true);

    /// <summary>Reads the public key of the DER certificate <paramref name="certificate" />.</summary>
    /// <param name="certificate">The DER certificate.</param>
    /// <returns>The key, or <see langword="null" /> when the certificate does not parse.</returns>
    public static TlsCertificatePublicKey? Read(byte[] certificate)
    {
        try
        {
            AsnReader tbs = new AsnReader(certificate, AsnEncodingRules.DER).ReadSequence().ReadSequence();
            if (tbs.PeekTag().HasSameClassAndValue(VersionTag))
            {
                tbs.ReadEncodedValue();
            }

            for (int field = 0; field < 5; field++)
            {
                tbs.ReadEncodedValue();
            }

            return ReadSubjectPublicKeyInfo(tbs.ReadEncodedValue().ToArray());
        }
        catch (AsnContentException)
        {
            return null;
        }
    }

    /// <summary>Reads a DER <c>SubjectPublicKeyInfo</c>.</summary>
    /// <param name="subjectPublicKeyInfo">The DER <c>SubjectPublicKeyInfo</c>.</param>
    /// <returns>The key.</returns>
    /// <exception cref="AsnContentException">The bytes are not a <c>SubjectPublicKeyInfo</c>.</exception>
    internal static TlsCertificatePublicKey ReadSubjectPublicKeyInfo(byte[] subjectPublicKeyInfo)
    {
        AsnReader spki = new AsnReader(subjectPublicKeyInfo, AsnEncodingRules.DER).ReadSequence();
        AsnReader algorithm = spki.ReadSequence();
        string algorithmOid = algorithm.ReadObjectIdentifier();
        string? curveOid = algorithm.HasData && algorithm.PeekTag().HasSameClassAndValue(Asn1Tag.ObjectIdentifier)
            ? algorithm.ReadObjectIdentifier()
            : null;
        return new TlsCertificatePublicKey(algorithmOid, curveOid, spki.ReadBitString(out _), subjectPublicKeyInfo);
    }

    /// <summary>Checks a TLS 1.3 CertificateVerify signature made with this key.</summary>
    /// <param name="scheme">The signature scheme the CertificateVerify names.</param>
    /// <param name="content">The signed content (<see cref="TlsSignatureScheme.BuildCertificateVerifyContent" />).</param>
    /// <param name="signature">The signature.</param>
    /// <returns>
    /// <see langword="null" /> when the signature verifies;
    /// <see cref="TlsAlertDescription.IllegalParameter" /> when the scheme is not a TLS 1.3
    /// CertificateVerify scheme or does not fit this key;
    /// <see cref="TlsAlertDescription.BadCertificate" /> when the key does not import; or
    /// <see cref="TlsAlertDescription.DecryptError" /> when the signature is wrong.
    /// </returns>
    public TlsAlertDescription? VerifySignature(ushort scheme, byte[] content, byte[] signature) =>
        VerifySignature(TlsSignatureScheme.FindRule(scheme), content, signature);

    /// <summary>
    /// Checks a signature by <paramref name="rule" />, with the alerts of
    /// <see cref="VerifySignature(ushort, byte[], byte[])" />: a missing rule, or one that
    /// does not fit this key, is <see cref="TlsAlertDescription.IllegalParameter" />. A rule
    /// with no curve fits an EC key on any curve.
    /// </summary>
    internal TlsAlertDescription? VerifySignature(TlsSignatureRule? rule, byte[] content, byte[] signature)
    {
        if (rule is null || rule.KeyOid != AlgorithmOid || (rule.CurveOid is not null && rule.CurveOid != CurveOid))
        {
            return TlsAlertDescription.IllegalParameter;
        }

        try
        {
            return Verify(rule, content, signature) ? null : TlsAlertDescription.DecryptError;
        }
        catch (Exception exception) when (exception is CryptographicException or AsnContentException)
        {
            return TlsAlertDescription.BadCertificate;
        }
    }

    /// <summary>
    /// Encrypts a TLS 1.2 and below RSA pre-master secret to this key with PKCS #1 v1.5
    /// (RFC 5246 section 7.4.7.1).
    /// </summary>
    /// <returns>The encrypted pre-master secret, or <see langword="null" /> when this is not an <c>rsaEncryption</c> key or it does not import.</returns>
    internal byte[]? EncryptPkcs1(byte[] preMasterSecret)
    {
        if (AlgorithmOid != TlsSignatureScheme.RsaEncryptionOid)
        {
            return null;
        }

        try
        {
            using RSA rsa = RSA.Create();
            rsa.ImportRSAPublicKey(KeyBits, out _);
            return rsa.Encrypt(preMasterSecret, RSAEncryptionPadding.Pkcs1);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private bool Verify(TlsSignatureRule rule, byte[] content, byte[] signature) => rule.Kind switch
    {
        TlsSignatureKind.Ecdsa => VerifyEcdsa(rule.Hash, content, signature),
        TlsSignatureKind.Ed25519 => VerifyEd25519(content, signature),
        TlsSignatureKind.Dsa => VerifyDsa(rule.Hash, content, signature),
        _ => VerifyRsa(rule, content, signature),
    };

    private bool VerifyRsa(TlsSignatureRule rule, byte[] content, byte[] signature) => rule.Kind == TlsSignatureKind.RsaMd5Sha1
        ? VerifyRsaMd5Sha1(content, signature)
        : VerifyRsa(rule.Hash, rule.Kind == TlsSignatureKind.RsaPss ? RSASignaturePadding.Pss : RSASignaturePadding.Pkcs1, content, signature);

    /// <summary>
    /// The BCL verifies PKCS #1 v1.5 only with a DigestInfo, so TLS 1.0 and 1.1's bare MD5
    /// and SHA-1 block is checked with the public operation s^e mod n, which involves no secret.
    /// </summary>
    private bool VerifyRsaMd5Sha1(byte[] content, byte[] signature)
    {
        AsnReader key = new AsnReader(KeyBits, AsnEncodingRules.DER).ReadSequence();
        BigInteger modulus = key.ReadInteger();
        BigInteger exponent = key.ReadInteger();
        key.ThrowIfNotEmpty();
        var value = new BigInteger(signature, isUnsigned: true, isBigEndian: true);
        if (!IsUsableLegacyRsaKey(modulus, exponent) || signature.Length != modulus.GetByteCount(isUnsigned: true) || value >= modulus)
        {
            return false;
        }

        BigInteger recovered = BigInteger.ModPow(value, exponent, modulus);
        return recovered == new BigInteger(TlsSignatureScheme.BuildMd5Sha1Block(content, signature.Length), isUnsigned: true, isBigEndian: true);
    }

    /// <summary>
    /// Whether a server's RSA key can carry the MD5 and SHA-1 block (36 bytes and 11 of
    /// padding) within OpenSSL's limits on the public operation: a modulus of at most
    /// 16384 bits and an exponent of at most 64 bits.
    /// </summary>
    private static bool IsUsableLegacyRsaKey(BigInteger modulus, BigInteger exponent) =>
        modulus.Sign > 0
        && modulus.GetByteCount(isUnsigned: true) is >= MinimumLegacyRsaModulusLength and <= MaximumRsaModulusLength
        && exponent.Sign > 0
        && exponent.GetBitLength() <= MaximumRsaExponentBits;

    private bool VerifyRsa(HashAlgorithmName hash, RSASignaturePadding padding, byte[] content, byte[] signature)
    {
        using RSA rsa = RSA.Create();
        rsa.ImportRSAPublicKey(KeyBits, out _);
        return rsa.VerifyData(content, signature, hash, padding);
    }

    private bool VerifyEcdsa(HashAlgorithmName hash, byte[] content, byte[] signature)
    {
        using ECDsa ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(SubjectPublicKeyInfo, out _);
        return ecdsa.VerifyData(content, signature, hash, DSASignatureFormat.Rfc3279DerSequence);
    }

    /// <summary>
    /// Checks a DSA signature with the hand-built DSA of <c>Curl.Cryptography</c>, which
    /// reads the same on every platform where the BCL's does not (ADR-0118). The domain
    /// parameters p, q and g are the key's <c>Dss-Parms</c> and y the INTEGER in its bit
    /// string (RFC 3279 section 2.3.2); a key that does not read so is a bad certificate. The
    /// signature is the DER <c>Dss-Sig-Value</c> of r and s (RFC 5246 section 4.7), and one
    /// that does not decode does not verify.
    /// </summary>
    private bool VerifyDsa(HashAlgorithmName hash, byte[] content, byte[] signature)
    {
        AsnReader algorithm = new AsnReader(SubjectPublicKeyInfo, AsnEncodingRules.DER).ReadSequence().ReadSequence();
        algorithm.ReadObjectIdentifier();
        AsnReader domain = algorithm.ReadSequence();
        byte[] prime = ReadPositiveInteger(domain);
        byte[] subprime = ReadPositiveInteger(domain);
        byte[] generator = ReadPositiveInteger(domain);
        domain.ThrowIfNotEmpty();
        AsnReader keyBits = new(KeyBits, AsnEncodingRules.DER);
        byte[] publicKey = ReadPositiveInteger(keyBits);
        keyBits.ThrowIfNotEmpty();
        byte[]? rs = DecodeDssSignature(signature, subprime.Length);
        return rs is not null && Cryptography.DsaSignature.VerifyHash(prime, subprime, generator, publicKey, Cryptography.DsaSignature.HashData(content, hash), rs);
    }

    private static byte[] ReadPositiveInteger(AsnReader reader)
    {
        BigInteger value = reader.ReadInteger();
        return value.Sign > 0
            ? value.ToByteArray(isUnsigned: true, isBigEndian: true)
            : throw new CryptographicException("A DSA key's p, q, g and y are positive.");
    }

    /// <summary>Returns r || s, each <paramref name="subprimeLength" /> bytes, from a DER <c>Dss-Sig-Value</c>, or <see langword="null" /> when it does not decode or a value is negative or longer than q.</summary>
    private static byte[]? DecodeDssSignature(byte[] signature, int subprimeLength)
    {
        try
        {
            AsnReader reader = new(signature, AsnEncodingRules.DER);
            AsnReader values = reader.ReadSequence();
            reader.ThrowIfNotEmpty();
            byte[] rs = new byte[2 * subprimeLength];
            bool fits = TryWriteFixedLength(values.ReadInteger(), rs.AsSpan(0, subprimeLength))
                & TryWriteFixedLength(values.ReadInteger(), rs.AsSpan(subprimeLength));
            values.ThrowIfNotEmpty();
            return fits ? rs : null;
        }
        catch (AsnContentException)
        {
            return null;
        }
    }

    private static bool TryWriteFixedLength(BigInteger value, Span<byte> destination)
    {
        if (value.Sign < 0 || value.GetByteCount(isUnsigned: true) > destination.Length)
        {
            return false;
        }

        _ = value.TryWriteBytes(destination[^value.GetByteCount(isUnsigned: true)..], out _, isUnsigned: true, isBigEndian: true);
        return true;
    }

    private bool VerifyEd25519(byte[] content, byte[] signature)
    {
        if (KeyBits.Length != Cryptography.Ed25519.PublicKeySize)
        {
            throw new CryptographicException("An Ed25519 public key is 32 bytes.");
        }

        return signature.Length == Cryptography.Ed25519.SignatureSize && Cryptography.Ed25519.Verify(KeyBits, content, signature);
    }
}
