using System.Formats.Asn1;
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

    /// <summary>Checks a CertificateVerify signature made with this key.</summary>
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
    public TlsAlertDescription? VerifySignature(ushort scheme, byte[] content, byte[] signature)
    {
        TlsSignatureRule? rule = TlsSignatureScheme.FindRule(scheme);
        if (rule is null || rule.KeyOid != AlgorithmOid || rule.CurveOid != CurveOid)
        {
            return TlsAlertDescription.IllegalParameter;
        }

        try
        {
            return Verify(rule, content, signature) ? null : TlsAlertDescription.DecryptError;
        }
        catch (CryptographicException)
        {
            return TlsAlertDescription.BadCertificate;
        }
    }

    private bool Verify(TlsSignatureRule rule, byte[] content, byte[] signature) => rule.Kind switch
    {
        TlsSignatureKind.RsaPss => VerifyRsaPss(rule.Hash, content, signature),
        TlsSignatureKind.Ecdsa => VerifyEcdsa(rule.Hash, content, signature),
        _ => VerifyEd25519(content, signature),
    };

    private bool VerifyRsaPss(HashAlgorithmName hash, byte[] content, byte[] signature)
    {
        using RSA rsa = RSA.Create();
        rsa.ImportRSAPublicKey(KeyBits, out _);
        return rsa.VerifyData(content, signature, hash, RSASignaturePadding.Pss);
    }

    private bool VerifyEcdsa(HashAlgorithmName hash, byte[] content, byte[] signature)
    {
        using ECDsa ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(SubjectPublicKeyInfo, out _);
        return ecdsa.VerifyData(content, signature, hash, DSASignatureFormat.Rfc3279DerSequence);
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
