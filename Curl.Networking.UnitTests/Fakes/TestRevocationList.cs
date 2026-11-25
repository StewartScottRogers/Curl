using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking.Fakes;

/// <summary>
/// Writes certificate revocation lists by hand with <see cref="AsnWriter" />, for the shapes
/// <see cref="CertificateRevocationListBuilder" /> never writes: no version, no
/// <c>nextUpdate</c>, no extensions, GeneralizedTime dates, another signature algorithm, or a
/// list for an issuer that is not a CA (BL-609).
/// </summary>
internal static class TestRevocationList
{
    internal const string Sha256WithRsa = "1.2.840.113549.1.1.11";

    internal const string EcdsaWithSha256 = "1.2.840.10045.4.3.2";

    // id-Ed25519, which the list reader does not verify.
    internal const string Ed25519 = "1.3.101.112";

    internal const string RsaSsaPss = "1.2.840.113549.1.1.10";

    internal const string Mgf1 = "1.2.840.113549.1.1.8";

    internal const string Sha1 = "1.3.14.3.2.26";

    internal const string Sha256 = "2.16.840.1.101.3.4.2.1";

    internal const string Sha384 = "2.16.840.1.101.3.4.2.2";

    internal const string Sha512 = "2.16.840.1.101.3.4.2.3";

    /// <summary>DER <c>RSASSA-PSS-params</c> (RFC 4055); a field passed as <see langword="null" /> is left out.</summary>
    /// <param name="hash">The OID of <c>hashAlgorithm</c>.</param>
    /// <param name="maskHash">The OID of the hash the mask generation function uses.</param>
    /// <param name="saltLength">The <c>saltLength</c>.</param>
    /// <param name="trailerField">The <c>trailerField</c>.</param>
    /// <param name="maskFunction">The OID of the mask generation function.</param>
    /// <returns>The parameters' DER.</returns>
    internal static byte[] PssParameters(
        string? hash = null, string? maskHash = null, int? saltLength = null, int? trailerField = null, string maskFunction = Mgf1)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            if (hash is not null)
            {
                using (writer.PushSequence(Explicit(0)))
                {
                    WriteAlgorithm(writer, hash);
                }
            }

            if (maskHash is not null)
            {
                using (writer.PushSequence(Explicit(1)))
                using (writer.PushSequence())
                {
                    writer.WriteObjectIdentifier(maskFunction);
                    WriteAlgorithm(writer, maskHash);
                }
            }

            WriteExplicitInteger(writer, 2, saltLength);
            WriteExplicitInteger(writer, 3, trailerField);
        }

        return writer.Encode();
    }

    private static Asn1Tag Explicit(int number) => new(TagClass.ContextSpecific, number, isConstructed: true);

    private static void WriteExplicitInteger(AsnWriter writer, int number, int? value)
    {
        if (value is { } integer)
        {
            using (writer.PushSequence(Explicit(number)))
            {
                writer.WriteInteger(integer);
            }
        }
    }

    /// <summary>Writes a list issued in <paramref name="issuerName" />'s name and signed by <paramref name="signer" />'s key.</summary>
    /// <param name="signer">The certificate whose private key signs; RSA signs PKCS #1, or PSS when <paramref name="signatureAlgorithm" /> is <see cref="RsaSsaPss" />.</param>
    /// <param name="issuerName">The issuer name written; the signer's subject when <see langword="null" />.</param>
    /// <param name="thisUpdate">The list's issue date; an hour ago when <see langword="null" />.</param>
    /// <param name="nextUpdate">The list's expiry; none is written when <see langword="null" />.</param>
    /// <param name="revokedSerialNumbers">The big-endian serial numbers revoked; none when <see langword="null" />.</param>
    /// <param name="writeVersion">Whether to write the v2 version number.</param>
    /// <param name="generalizedTimes">Whether to write the dates as GeneralizedTime rather than UTCTime.</param>
    /// <param name="signatureAlgorithm">The algorithm named; the signer's natural one when <see langword="null" />.</param>
    /// <param name="extensions">DER <c>Extension</c> values (see <see cref="Extension" />) for the list; none when <see langword="null" />.</param>
    /// <param name="entryExtensions">DER <c>Extension</c> values written on every revoked entry; none when <see langword="null" />.</param>
    /// <param name="signatureParameters">The DER of the outer signature algorithm's parameters; none when <see langword="null" />.</param>
    /// <param name="signatureHash">The hash signed over; SHA-256 when <see langword="null" />.</param>
    /// <returns>The list's DER.</returns>
    internal static byte[] Write(
        X509Certificate2 signer,
        X500DistinguishedName? issuerName = null,
        DateTimeOffset? thisUpdate = null,
        DateTimeOffset? nextUpdate = null,
        IReadOnlyList<byte[]>? revokedSerialNumbers = null,
        bool writeVersion = true,
        bool generalizedTimes = false,
        string? signatureAlgorithm = null,
        IReadOnlyList<byte[]>? extensions = null,
        IReadOnlyList<byte[]>? entryExtensions = null,
        byte[]? signatureParameters = null,
        HashAlgorithmName? signatureHash = null)
    {
        using var rsa = signer.GetRSAPrivateKey();
        using var ecdsa = signer.GetECDsaPrivateKey();
        var algorithm = signatureAlgorithm ?? (rsa is not null ? Sha256WithRsa : EcdsaWithSha256);

        var signed = new AsnWriter(AsnEncodingRules.DER);
        using (signed.PushSequence())
        {
            if (writeVersion)
            {
                signed.WriteInteger(1);
            }

            WriteAlgorithm(signed, algorithm);
            signed.WriteEncodedValue((issuerName ?? signer.SubjectName).RawData);
            WriteTime(signed, thisUpdate ?? DateTimeOffset.UtcNow.AddHours(-1), generalizedTimes);
            if (nextUpdate is { } expiry)
            {
                WriteTime(signed, expiry, generalizedTimes);
            }

            if (revokedSerialNumbers is { Count: > 0 })
            {
                using (signed.PushSequence())
                {
                    foreach (var serialNumber in revokedSerialNumbers)
                    {
                        using (signed.PushSequence())
                        {
                            signed.WriteIntegerUnsigned(serialNumber);
                            WriteTime(signed, DateTimeOffset.UtcNow.AddHours(-1), generalizedTimes);
                            WriteExtensions(signed, entryExtensions, explicitTag: false);
                        }
                    }
                }
            }

            WriteExtensions(signed, extensions, explicitTag: true);
        }

        var signedPart = signed.Encode();
        var digest = signatureHash ?? HashAlgorithmName.SHA256;
        var signature = rsa is not null
            ? rsa.SignData(signedPart, digest, algorithm == RsaSsaPss ? RSASignaturePadding.Pss : RSASignaturePadding.Pkcs1)
            : ecdsa!.SignData(signedPart, digest, DSASignatureFormat.Rfc3279DerSequence);

        var list = new AsnWriter(AsnEncodingRules.DER);
        using (list.PushSequence())
        {
            list.WriteEncodedValue(signedPart);
            WriteAlgorithm(list, algorithm, signatureParameters);
            list.WriteBitString(signature);
        }

        return list.Encode();
    }

    /// <summary>One DER <c>Extension</c>: its OID, the critical flag when set, and its value.</summary>
    /// <param name="oid">The extension's OID.</param>
    /// <param name="critical">Whether the extension is critical.</param>
    /// <param name="value">The DER written inside the extension's OCTET STRING.</param>
    /// <returns>The extension's DER.</returns>
    internal static byte[] Extension(string oid, bool critical, byte[] value)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(oid);
            if (critical)
            {
                writer.WriteBoolean(true);
            }

            writer.WriteOctetString(value);
        }

        return writer.Encode();
    }

    /// <summary>The list as a PEM <c>X509 CRL</c> block.</summary>
    /// <param name="der">The list's DER.</param>
    /// <returns>The PEM text.</returns>
    internal static string Pem(byte[] der) => PemEncoding.WriteString("X509 CRL", der) + "\n";

    private static void WriteExtensions(AsnWriter writer, IReadOnlyList<byte[]>? extensions, bool explicitTag)
    {
        if (extensions is not { Count: > 0 })
        {
            return;
        }

        using (explicitTag ? writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)) : default(AsnWriter.Scope))
        {
            using (writer.PushSequence())
            {
                foreach (var extension in extensions)
                {
                    writer.WriteEncodedValue(extension);
                }
            }
        }
    }

    private static void WriteAlgorithm(AsnWriter writer, string algorithm, byte[]? parameters = null)
    {
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(algorithm);
            if (parameters is not null)
            {
                writer.WriteEncodedValue(parameters);
            }
        }
    }

    private static void WriteTime(AsnWriter writer, DateTimeOffset value, bool generalized)
    {
        if (generalized)
        {
            writer.WriteGeneralizedTime(value, omitFractionalSeconds: true);
        }
        else
        {
            writer.WriteUtcTime(value);
        }
    }
}
