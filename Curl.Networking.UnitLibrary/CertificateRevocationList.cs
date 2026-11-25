using System.Collections.Frozen;
using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Cryptography;

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

    private const string RsaSsaPssOid = "1.2.840.113549.1.1.10";

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
    private readonly byte[] _signatureParameters;
    private readonly byte[] _signature;
    private readonly byte[] _issuerName;
    private readonly HashSet<BigInteger> _revokedSerialNumbers;
    private readonly HashSet<BigInteger> _removedSerialNumbers;
    private readonly RevocationListScope _scope;

    private CertificateRevocationList(
        byte[] signedPart,
        (string Oid, byte[] Parameters) signatureAlgorithm,
        byte[] signature,
        byte[] issuerName,
        DateTimeOffset thisUpdate,
        DateTimeOffset? nextUpdate,
        HashSet<BigInteger> revokedSerialNumbers,
        HashSet<BigInteger> removedSerialNumbers,
        RevocationListScope scope)
    {
        _scope = scope;
        _signedPart = signedPart;
        (_signatureAlgorithm, _signatureParameters) = signatureAlgorithm;
        _signature = signature;
        _issuerName = issuerName;
        ThisUpdate = thisUpdate;
        NextUpdate = nextUpdate;
        _revokedSerialNumbers = revokedSerialNumbers;
        _removedSerialNumbers = removedSerialNumbers;
    }

    /// <summary>Gets when the list was issued: before it, the list is not valid yet.</summary>
    public DateTimeOffset ThisUpdate { get; }

    /// <summary>Gets when the list expires, or <see langword="null" /> when it names no such date.</summary>
    public DateTimeOffset? NextUpdate { get; }

    /// <summary>
    /// Returns whether the list has not expired at <paramref name="now" />: its <c>nextUpdate</c> is
    /// not past, or it names none, which the Windows build treats as current (STD-197).
    /// </summary>
    /// <param name="now">The moment asked about.</param>
    /// <returns><see langword="true" /> when the list has not expired.</returns>
    public bool IsUnexpiredAt(DateTimeOffset now) => NextUpdate is not { } expiry || expiry >= now;

    /// <summary>
    /// Decodes a DER <c>CertificateList</c>: the signed <c>TBSCertList</c> (its optional
    /// version, signature algorithm, issuer, <c>thisUpdate</c>, optional <c>nextUpdate</c>,
    /// optional revoked certificates and extensions), the signature algorithm and the signature.
    /// The extensions of the list and of each entry are read into a scope (see <see cref="CoversScopeOf" />); the revoked serial numbers are kept whatever the scope.
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
        var revokedSerialNumbers = ReadRevokedSerialNumbers(signed, out var removedSerialNumbers, out var entriesUnusable);
        var scope = RevocationListScope.Read(signed);
        scope.Unusable |= entriesUnusable;
        return new CertificateRevocationList(
            signedPart, signatureAlgorithm, signature, issuerName, thisUpdate, nextUpdate, revokedSerialNumbers, removedSerialNumbers, scope);
    }

    /// <summary>
    /// Returns whether the list can answer for <paramref name="certificate" /> fetched from
    /// <paramref name="distributionPoint" /> (RFC 5280, 5.2.5): it has no unrecognised critical
    /// extension (of the list), is not a delta list, is not indirect, does not
    /// cover only some reasons, and its issuing distribution point, when it has one, names that
    /// address and admits the certificate's kind (CA or not). A list that fails is "unknown",
    /// never "not revoked" (STD-113).
    /// </summary>
    /// <param name="certificate">The certificate whose revocation is checked.</param>
    /// <param name="distributionPoint">The address the list was fetched from.</param>
    /// <returns><see langword="true" /> when the list's scope covers the certificate.</returns>
    public bool CoversScopeOf(X509Certificate2 certificate, Uri distributionPoint) =>
        _scope.Covers(certificate, distributionPoint);

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
    /// (PKCS #1 v1.5, or RSASSA-PSS as <see cref="RsaPssSignatureParameters" /> reads it, with
    /// exactly the salt length it names) or ECDSA signature over SHA-1, SHA-256, SHA-384 or
    /// SHA-512 is checked; any other algorithm, or a key of the wrong kind, fails.
    /// </summary>
    /// <param name="issuer">The certificate whose key signed the list.</param>
    /// <param name="anotherMaskHashVerifies">
    /// Whether an RSASSA-PSS signature whose MGF1 hash is not its message hash can verify, as in
    /// curl's OpenSSL build; the Schannel build leaves such a list unverified (ADR-1369).
    /// </param>
    /// <returns><see langword="true" /> when the signature verifies.</returns>
    public bool IsSignedBy(X509Certificate2 issuer, bool anotherMaskHashVerifies = false)
    {
        if (RsaSignatureAlgorithms.TryGetValue(_signatureAlgorithm, out var rsaHash))
        {
            return IsRsaPkcs1SignedBy(issuer, rsaHash);
        }

        if (_signatureAlgorithm == RsaSsaPssOid)
        {
            return IsRsaPssSignedBy(issuer, anotherMaskHashVerifies);
        }

        if (EcdsaSignatureAlgorithms.TryGetValue(_signatureAlgorithm, out var ecdsaHash))
        {
            using var ecdsa = issuer.GetECDsaPublicKey();
            return ecdsa is not null && ecdsa.VerifyData(_signedPart, _signature, ecdsaHash, DSASignatureFormat.Rfc3279DerSequence);
        }

        return false;
    }

    private bool IsRsaPkcs1SignedBy(X509Certificate2 issuer, HashAlgorithmName hash)
    {
        using var rsa = issuer.GetRSAPublicKey();
        return rsa is not null && rsa.VerifyData(_signedPart, _signature, hash, RSASignaturePadding.Pkcs1);
    }

    private bool IsRsaPssSignedBy(X509Certificate2 issuer, bool anotherMaskHashVerifies)
    {
        if (RsaPssSignatureParameters.Read(_signatureParameters) is not { } pss || (pss.MaskHash != pss.Hash && !anotherMaskHashVerifies))
        {
            return false;
        }

        using var rsa = issuer.GetRSAPublicKey();
        return rsa is not null && RsaPssVerification.Verify(
            rsa.ExportParameters(includePrivateParameters: false), _signedPart, _signature, pss.Hash, pss.MaskHash, pss.SaltLength);
    }

    /// <summary>Gets the value of the list's <c>freshestCRL</c> extension (where its delta lists are), or <see langword="null" /> when it has none.</summary>
    public byte[]? FreshestCrlValue => _scope.FreshestCrlValue;

    /// <summary>
    /// Returns whether this list is a delta list that updates <paramref name="baseList" /> and answers
    /// for <paramref name="certificate" />: its <c>deltaCRLIndicator</c> is not past the base list's <c>cRLNumber</c>.
    /// </summary>
    /// <param name="baseList">The base list the delta updates.</param>
    /// <param name="certificate">The certificate whose revocation is checked.</param>
    /// <returns><see langword="true" /> when this list can be applied to the base list.</returns>
    public bool IsDeltaOf(CertificateRevocationList baseList, X509Certificate2 certificate) =>
        _scope.CoversAsDelta(certificate, baseList._scope.Number);

    /// <summary>Returns whether the list revokes <paramref name="certificate" />, by its serial number.</summary>
    /// <param name="certificate">The certificate checked.</param>
    /// <returns><see langword="true" /> when its serial number is listed.</returns>
    public bool Revokes(X509Certificate2 certificate) =>
        _revokedSerialNumbers.Contains(SerialNumberOf(certificate));

    /// <summary>Returns whether the list lifts a hold on <paramref name="certificate" />: an entry with reason <c>removeFromCRL</c> (RFC 5280, section 5.3.1), a delta list's way to undo a <c>certificateHold</c>.</summary>
    /// <param name="certificate">The certificate checked.</param>
    /// <returns><see langword="true" /> when its serial number is listed with reason removeFromCRL.</returns>
    public bool Releases(X509Certificate2 certificate) =>
        _removedSerialNumbers.Contains(SerialNumberOf(certificate));

    private static BigInteger SerialNumberOf(X509Certificate2 certificate) =>
        new(certificate.SerialNumberBytes.Span, isUnsigned: false, isBigEndian: true);

    // AlgorithmIdentifier: the OID, then its parameters' DER, empty when there are none.
    private static (string Oid, byte[] Parameters) ReadAlgorithm(AsnReader reader)
    {
        var algorithm = reader.ReadSequence();
        var oid = algorithm.ReadObjectIdentifier();
        return (oid, algorithm.HasData ? algorithm.ReadEncodedValue().ToArray() : []);
    }

    private static bool IsTime(Asn1Tag tag) =>
        tag.HasSameClassAndValue(Asn1Tag.UtcTime) || tag.HasSameClassAndValue(Asn1Tag.GeneralizedTime);

    private static DateTimeOffset ReadTime(AsnReader reader) =>
        reader.PeekTag().HasSameClassAndValue(Asn1Tag.UtcTime) ? reader.ReadUtcTime() : reader.ReadGeneralizedTime();

    // revokedCertificates is present only when something is revoked; whatever follows it is
    // the [0] extensions. An entry with an unrecognised critical extension, or a certificate
    // issuer one is ignored, as the Schannel build ignores them (STD-162); a malformed one makes the list unusable.
    private static HashSet<BigInteger> ReadRevokedSerialNumbers(AsnReader signed, out HashSet<BigInteger> removedSerialNumbers, out bool entriesUnusable)
    {
        entriesUnusable = false;
        removedSerialNumbers = [];
        var serialNumbers = new HashSet<BigInteger>();
        if (!signed.HasData || !signed.PeekTag().HasSameClassAndValue(Asn1Tag.Sequence))
        {
            return serialNumbers;
        }

        var revoked = signed.ReadSequence();
        while (revoked.HasData)
        {
            entriesUnusable |= ReadRevokedEntry(revoked.ReadSequence(), serialNumbers, removedSerialNumbers);
        }

        return serialNumbers;
    }

    // Files the entry's serial number under revoked or, for reason removeFromCRL, under removed; returns whether the entry is unusable.
    private static bool ReadRevokedEntry(AsnReader entry, HashSet<BigInteger> revoked, HashSet<BigInteger> removed)
    {
        var serialNumber = entry.ReadInteger();
        entry.ReadEncodedValue();
        var extensions = entry.HasData ? entry.PeekEncodedValue() : default;
        var unusable = entry.HasData && RevocationListScope.EntryExtensionsAreUnusable(entry);
        (!unusable && RevocationListScope.EntryReasonIsRemoveFromCrl(extensions) ? removed : revoked).Add(serialNumber);
        return unusable;
    }
}
