using System.Collections.Frozen;
using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// One X.509 certificate revocation list, decoded from its DER with
/// <see cref="AsnReader" /> (RFC 5280, section 5.1), as curl's OpenSSL build reads a
/// <c>--crlfile</c> entry (ADR-0197, BL-609). The BCL has no reader for a CRL's issuer, dates
/// or signature (<c>CertificateRevocationListBuilder.Load</c>
/// gives only the revoked serial numbers), so the list is decoded here and its signature
/// verified with the issuer's key.
/// </summary>
internal sealed class CertificateRevocationList
{
    // sha1WithRSAEncryption, sha256WithRSAEncryption, sha384WithRSAEncryption, sha512WithRSAEncryption.
    private static readonly FrozenDictionary<string, HashAlgorithmName> RsaSignatureAlgorithms = new Dictionary<string, HashAlgorithmName>
    {
        ["1.2.840.113549.1.1.5"] = HashAlgorithmName.SHA1,
        ["1.2.840.113549.1.1.11"] = HashAlgorithmName.SHA256,
        ["1.2.840.113549.1.1.12"] = HashAlgorithmName.SHA384,
        ["1.2.840.113549.1.1.13"] = HashAlgorithmName.SHA512,
    }.ToFrozenDictionary();

    // ecdsa-with-SHA1, ecdsa-with-SHA256, ecdsa-with-SHA384, ecdsa-with-SHA512.
    private static readonly FrozenDictionary<string, HashAlgorithmName> EcdsaSignatureAlgorithms = new Dictionary<string, HashAlgorithmName>
    {
        ["1.2.840.10045.4.1"] = HashAlgorithmName.SHA1,
        ["1.2.840.10045.4.3.2"] = HashAlgorithmName.SHA256,
        ["1.2.840.10045.4.3.3"] = HashAlgorithmName.SHA384,
        ["1.2.840.10045.4.3.4"] = HashAlgorithmName.SHA512,
    }.ToFrozenDictionary();

    private readonly byte[] _signedPart;
    private readonly string _signatureAlgorithm;
    private readonly byte[] _signature;
    private readonly byte[] _issuerName;
    private readonly HashSet<BigInteger> _revokedSerialNumbers;

    private CertificateRevocationList(
        byte[] signedPart,
        string signatureAlgorithm,
        byte[] signature,
        byte[] issuerName,
        DateTimeOffset thisUpdate,
        DateTimeOffset? nextUpdate,
        HashSet<BigInteger> revokedSerialNumbers)
    {
        _signedPart = signedPart;
        _signatureAlgorithm = signatureAlgorithm;
        _signature = signature;
        _issuerName = issuerName;
        ThisUpdate = thisUpdate;
        NextUpdate = nextUpdate;
        _revokedSerialNumbers = revokedSerialNumbers;
    }

    /// <summary>Gets when the list was issued: before it, the list is not valid yet.</summary>
    public DateTimeOffset ThisUpdate { get; }

    /// <summary>Gets when the list expires, or <see langword="null" /> when it names no such date.</summary>
    public DateTimeOffset? NextUpdate { get; }

    /// <summary>
    /// Decodes a DER <c>CertificateList</c>: the signed <c>TBSCertList</c> (its optional
    /// version, signature algorithm, issuer, <c>thisUpdate</c>, optional <c>nextUpdate</c>,
    /// optional revoked certificates and extensions), the signature algorithm and the signature.
    /// Extensions, of the list and of each entry, are read past and not acted on.
    /// </summary>
    /// <param name="der">The list's DER.</param>
    /// <returns>The decoded list.</returns>
    /// <exception cref="AsnContentException">The bytes are not a DER certificate revocation list.</exception>
    public static CertificateRevocationList Decode(ReadOnlyMemory<byte> der)
    {
        var outer = new AsnReader(der, AsnEncodingRules.DER);
        var certificateList = outer.ReadSequence();
        outer.ThrowIfNotEmpty();
        var signedPart = certificateList.PeekEncodedValue().ToArray();
        var signed = certificateList.ReadSequence();
        var signatureAlgorithm = ReadAlgorithm(certificateList);
        var signature = certificateList.ReadBitString(out _);
        certificateList.ThrowIfNotEmpty();

        if (signed.PeekTag().HasSameClassAndValue(Asn1Tag.Integer))
        {
            signed.ReadInteger();
        }

        ReadAlgorithm(signed);
        var issuerName = signed.ReadEncodedValue().ToArray();
        var thisUpdate = ReadTime(signed);
        var nextUpdate = signed.HasData && IsTime(signed.PeekTag()) ? ReadTime(signed) : (DateTimeOffset?)null;
        return new CertificateRevocationList(
            signedPart, signatureAlgorithm, signature, issuerName, thisUpdate, nextUpdate, ReadRevokedSerialNumbers(signed));
    }

    /// <summary>
    /// Returns whether the list is the one for certificates <paramref name="certificate" />'s
    /// issuer issued: its issuer name is, byte for byte, the certificate's issuer name.
    /// </summary>
    /// <param name="certificate">The certificate whose revocation is checked.</param>
    /// <returns><see langword="true" /> when the names are the same.</returns>
    public bool CoversCertificatesIssuedFor(X509Certificate2 certificate) =>
        _issuerName.AsSpan().SequenceEqual(certificate.IssuerName.RawData);

    /// <summary>
    /// Returns whether <paramref name="issuer" />'s key verifies the list's signature. An RSA
    /// (PKCS #1 v1.5) or ECDSA signature over SHA-1, SHA-256, SHA-384 or SHA-512 is checked;
    /// any other algorithm, or a key of the wrong kind, fails.
    /// </summary>
    /// <param name="issuer">The certificate whose key signed the list.</param>
    /// <returns><see langword="true" /> when the signature verifies.</returns>
    public bool IsSignedBy(X509Certificate2 issuer)
    {
        if (RsaSignatureAlgorithms.TryGetValue(_signatureAlgorithm, out var rsaHash))
        {
            using var rsa = issuer.GetRSAPublicKey();
            return rsa is not null && rsa.VerifyData(_signedPart, _signature, rsaHash, RSASignaturePadding.Pkcs1);
        }

        if (EcdsaSignatureAlgorithms.TryGetValue(_signatureAlgorithm, out var ecdsaHash))
        {
            using var ecdsa = issuer.GetECDsaPublicKey();
            return ecdsa is not null && ecdsa.VerifyData(_signedPart, _signature, ecdsaHash, DSASignatureFormat.Rfc3279DerSequence);
        }

        return false;
    }

    /// <summary>Returns whether the list revokes <paramref name="certificate" />, by its serial number.</summary>
    /// <param name="certificate">The certificate checked.</param>
    /// <returns><see langword="true" /> when its serial number is listed.</returns>
    public bool Revokes(X509Certificate2 certificate) =>
        _revokedSerialNumbers.Contains(new BigInteger(certificate.SerialNumberBytes.Span, isUnsigned: false, isBigEndian: true));

    // AlgorithmIdentifier: the OID, then parameters, which are read past.
    private static string ReadAlgorithm(AsnReader reader)
    {
        var algorithm = reader.ReadSequence();
        return algorithm.ReadObjectIdentifier();
    }

    private static bool IsTime(Asn1Tag tag) =>
        tag.HasSameClassAndValue(Asn1Tag.UtcTime) || tag.HasSameClassAndValue(Asn1Tag.GeneralizedTime);

    private static DateTimeOffset ReadTime(AsnReader reader) =>
        reader.PeekTag().HasSameClassAndValue(Asn1Tag.UtcTime) ? reader.ReadUtcTime() : reader.ReadGeneralizedTime();

    // revokedCertificates is present only when something is revoked; whatever follows it is
    // the [0] extensions, which are not read.
    private static HashSet<BigInteger> ReadRevokedSerialNumbers(AsnReader signed)
    {
        var serialNumbers = new HashSet<BigInteger>();
        if (!signed.HasData || !signed.PeekTag().HasSameClassAndValue(Asn1Tag.Sequence))
        {
            return serialNumbers;
        }

        var revoked = signed.ReadSequence();
        while (revoked.HasData)
        {
            serialNumbers.Add(revoked.ReadSequence().ReadInteger());
        }

        return serialNumbers;
    }
}
