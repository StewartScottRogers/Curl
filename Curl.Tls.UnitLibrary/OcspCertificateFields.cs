using System.Formats.Asn1;

namespace Curl.Tls;

/// <summary>
/// The parts of a DER X.509 certificate that checking an OCSP response needs (RFC 5280
/// section 4.1): the signed body and its signature, the serial number, the issuer and
/// subject names as DER, the public key, and whether the extended key usage allows OCSP
/// signing (RFC 6960 section 4.2.2.2).
/// </summary>
/// <param name="TbsCertificate">The DER <c>tbsCertificate</c>, the bytes the issuer signed.</param>
/// <param name="SignatureAlgorithm">The DER <c>AlgorithmIdentifier</c> of the issuer's signature.</param>
/// <param name="Signature">The contents of the <c>signatureValue</c> bit string.</param>
/// <param name="SerialNumber">The contents of the <c>serialNumber</c> integer.</param>
/// <param name="Issuer">The DER issuer <c>Name</c>.</param>
/// <param name="Subject">The DER subject <c>Name</c>.</param>
/// <param name="PublicKey">The subject's public key.</param>
/// <param name="CanSignOcspResponses">Whether the extended key usage names <c>id-kp-OCSPSigning</c>.</param>
internal sealed record OcspCertificateFields(
    byte[] TbsCertificate,
    byte[] SignatureAlgorithm,
    byte[] Signature,
    byte[] SerialNumber,
    byte[] Issuer,
    byte[] Subject,
    TlsCertificatePublicKey PublicKey,
    bool CanSignOcspResponses)
{
    private const string ExtendedKeyUsageOid = "2.5.29.37";
    private const string OcspSigningOid = "1.3.6.1.5.5.7.3.9";

    private static readonly Asn1Tag VersionTag = new(TagClass.ContextSpecific, 0, true);
    private static readonly Asn1Tag ExtensionsTag = new(TagClass.ContextSpecific, 3, true);

    /// <summary>Reads the DER certificate <paramref name="certificate" />.</summary>
    /// <param name="certificate">The DER certificate.</param>
    /// <returns>Its fields.</returns>
    /// <exception cref="AsnContentException">The bytes are not a DER certificate.</exception>
    public static OcspCertificateFields Read(byte[] certificate)
    {
        AsnReader outer = new(certificate, AsnEncodingRules.DER);
        AsnReader sequence = outer.ReadSequence();
        outer.ThrowIfNotEmpty();
        byte[] tbsCertificate = sequence.ReadEncodedValue().ToArray();
        byte[] signatureAlgorithm = sequence.ReadEncodedValue().ToArray();
        byte[] signature = sequence.ReadBitString(out _);
        sequence.ThrowIfNotEmpty();

        AsnReader tbs = new AsnReader(tbsCertificate, AsnEncodingRules.DER).ReadSequence();
        if (tbs.PeekTag().HasSameClassAndValue(VersionTag))
        {
            tbs.ReadEncodedValue();
        }

        byte[] serialNumber = tbs.ReadIntegerBytes().ToArray();
        tbs.ReadEncodedValue();
        byte[] issuer = tbs.ReadEncodedValue().ToArray();
        tbs.ReadEncodedValue();
        byte[] subject = tbs.ReadEncodedValue().ToArray();
        TlsCertificatePublicKey publicKey = TlsCertificatePublicKey.ReadSubjectPublicKeyInfo(tbs.ReadEncodedValue().ToArray());
        return new OcspCertificateFields(tbsCertificate, signatureAlgorithm, signature, serialNumber, issuer, subject, publicKey, AllowsOcspSigning(tbs));
    }

    /// <summary>Reads the rest of a <c>tbsCertificate</c> after its key: the unique IDs, skipped, and the extensions.</summary>
    private static bool AllowsOcspSigning(AsnReader tbs)
    {
        while (tbs.HasData)
        {
            if (tbs.PeekTag().HasSameClassAndValue(ExtensionsTag))
            {
                return ExtensionsAllowOcspSigning(tbs.ReadSequence(ExtensionsTag).ReadSequence());
            }

            tbs.ReadEncodedValue();
        }

        return false;
    }

    private static bool ExtensionsAllowOcspSigning(AsnReader extensions)
    {
        while (extensions.HasData)
        {
            AsnReader extension = extensions.ReadSequence();
            if (extension.ReadObjectIdentifier() == ExtendedKeyUsageOid)
            {
                if (extension.PeekTag().HasSameClassAndValue(Asn1Tag.Boolean))
                {
                    extension.ReadBoolean();
                }

                return KeyUsagesIncludeOcspSigning(extension.ReadOctetString());
            }
        }

        return false;
    }

    private static bool KeyUsagesIncludeOcspSigning(byte[] extendedKeyUsage)
    {
        AsnReader usages = new AsnReader(extendedKeyUsage, AsnEncodingRules.DER).ReadSequence();
        while (usages.HasData)
        {
            if (usages.ReadObjectIdentifier() == OcspSigningOid)
            {
                return true;
            }
        }

        return false;
    }
}
