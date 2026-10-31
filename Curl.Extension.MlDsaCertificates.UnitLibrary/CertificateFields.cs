using System.Formats.Asn1;

namespace Curl.Extension.MlDsaCertificates;

/// <summary>The fields of a DER X.509 certificate (RFC 5280 section 4.1) that checking its signature needs.</summary>
/// <param name="ToBeSigned">The DER encoding of the <c>tbsCertificate</c>, the bytes the signature covers.</param>
/// <param name="SignatureAlgorithm">The certificate's outer <c>signatureAlgorithm</c>.</param>
/// <param name="ToBeSignedSignatureAlgorithm">The <c>signature</c> field inside the <c>tbsCertificate</c>, which must equal the outer one.</param>
/// <param name="Signature">The <c>signatureValue</c> bytes.</param>
/// <param name="Issuer">The DER encoding of the issuer name.</param>
/// <param name="Subject">The DER encoding of the subject name.</param>
/// <param name="KeyAlgorithm">The algorithm of the subject public key.</param>
/// <param name="PublicKey">The <c>subjectPublicKey</c> bytes.</param>
internal sealed record CertificateFields(
    ReadOnlyMemory<byte> ToBeSigned,
    CertificateAlgorithm SignatureAlgorithm,
    CertificateAlgorithm ToBeSignedSignatureAlgorithm,
    ReadOnlyMemory<byte> Signature,
    ReadOnlyMemory<byte> Issuer,
    ReadOnlyMemory<byte> Subject,
    CertificateAlgorithm KeyAlgorithm,
    ReadOnlyMemory<byte> PublicKey)
{
    private static readonly Asn1Tag VersionTag = new(TagClass.ContextSpecific, 0, isConstructed: true);

    /// <summary>Gets whether the certificate's issuer name is its own subject name.</summary>
    internal bool IsSelfIssued => Issuer.Span.SequenceEqual(Subject.Span);

    /// <summary>Reads a certificate's fields from its DER encoding.</summary>
    /// <param name="certificate">The certificate's DER encoding, with nothing after it.</param>
    /// <returns>The fields, or <see langword="null" /> when it is not a DER certificate or a bit string has unused bits.</returns>
    internal static CertificateFields? Read(ReadOnlyMemory<byte> certificate)
    {
        try
        {
            AsnReader outer = new(certificate, AsnEncodingRules.DER);
            AsnReader fields = outer.ReadSequence();
            outer.ThrowIfNotEmpty();
            ReadOnlyMemory<byte> toBeSigned = fields.ReadEncodedValue();
            CertificateAlgorithm signatureAlgorithm = ReadAlgorithm(fields);
            byte[] signature = ReadWholeBytes(fields);
            fields.ThrowIfNotEmpty();
            return ReadToBeSigned(toBeSigned, signatureAlgorithm, signature);
        }
        catch (AsnContentException)
        {
            return null;
        }
    }

    private static CertificateFields ReadToBeSigned(ReadOnlyMemory<byte> toBeSigned, CertificateAlgorithm signatureAlgorithm, byte[] signature)
    {
        AsnReader fields = new AsnReader(toBeSigned, AsnEncodingRules.DER).ReadSequence();
        if (fields.PeekTag().HasSameClassAndValue(VersionTag))
        {
            fields.ReadEncodedValue();
        }

        fields.ReadEncodedValue();
        CertificateAlgorithm innerAlgorithm = ReadAlgorithm(fields);
        ReadOnlyMemory<byte> issuer = fields.ReadEncodedValue();
        fields.ReadEncodedValue();
        ReadOnlyMemory<byte> subject = fields.ReadEncodedValue();
        AsnReader keyInfo = fields.ReadSequence();
        CertificateAlgorithm keyAlgorithm = ReadAlgorithm(keyInfo);
        byte[] publicKey = ReadWholeBytes(keyInfo);
        keyInfo.ThrowIfNotEmpty();
        return new CertificateFields(toBeSigned, signatureAlgorithm, innerAlgorithm, signature, issuer, subject, keyAlgorithm, publicKey);
    }

    private static CertificateAlgorithm ReadAlgorithm(AsnReader reader)
    {
        AsnReader algorithm = reader.ReadSequence();
        return new CertificateAlgorithm(algorithm.ReadObjectIdentifier(), algorithm.HasData);
    }

    private static byte[] ReadWholeBytes(AsnReader reader)
    {
        byte[] bytes = reader.ReadBitString(out int unusedBitCount);
        return unusedBitCount == 0 ? bytes : throw new AsnContentException("A signature or public key bit string has unused bits.");
    }
}
