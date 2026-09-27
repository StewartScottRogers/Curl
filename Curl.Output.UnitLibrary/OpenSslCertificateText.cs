using System.Formats.Asn1;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Output;

/// <summary>
/// curl 8.21.0's OpenSSL-build <c>-v</c> lines about the server's certificates: the
/// <c>Server certificate:</c> block of <c>ossl_infof_cert</c> and the
/// <c>Certificate level</c> lines of <c>infof_certstack</c> in <c>lib/vtls/openssl.c</c>
/// (ADR-0085).
/// </summary>
internal static class OpenSslCertificateText
{
    private const string RsaEncryption = "1.2.840.113549.1.1.1";
    private const string RsassaPss = "1.2.840.113549.1.1.10";
    private const string EcPublicKey = "1.2.840.10045.2.1";
    private const string Ed25519 = "1.3.101.112";
    private const string Ed448 = "1.3.101.113";

    // EVP_PKEY_get_group_name, EVP_PKEY_bits and EVP_PKEY_get_security_bits for the named curves.
    private static readonly Dictionary<string, string> NamedCurves = new(StringComparer.Ordinal)
    {
        ["1.2.840.10045.3.1.7"] = "prime256v1 (256/128",
        ["1.3.132.0.10"] = "secp256k1 (256/128",
        ["1.3.132.0.34"] = "secp384r1 (384/192",
        ["1.3.132.0.35"] = "secp521r1 (521/256",
        ["1.3.36.3.3.2.8.1.1.7"] = "brainpoolP256r1 (256/128",
        ["1.3.36.3.3.2.8.1.1.11"] = "brainpoolP384r1 (384/192",
        ["1.3.36.3.3.2.8.1.1.13"] = "brainpoolP512r1 (512/256",
    };

    // OpenSSL's long names for the signature algorithms (OBJ_obj2txt); any other prints dotted.
    private static readonly Dictionary<string, string> SignatureAlgorithmNames = new(StringComparer.Ordinal)
    {
        ["1.2.840.113549.1.1.4"] = "md5WithRSAEncryption",
        ["1.2.840.113549.1.1.5"] = "sha1WithRSAEncryption",
        [RsassaPss] = "rsassaPss",
        ["1.2.840.113549.1.1.11"] = "sha256WithRSAEncryption",
        ["1.2.840.113549.1.1.12"] = "sha384WithRSAEncryption",
        ["1.2.840.113549.1.1.13"] = "sha512WithRSAEncryption",
        ["1.2.840.113549.1.1.14"] = "sha224WithRSAEncryption",
        ["1.2.840.10045.4.1"] = "ecdsa-with-SHA1",
        ["1.2.840.10045.4.3.1"] = "ecdsa-with-SHA224",
        ["1.2.840.10045.4.3.2"] = "ecdsa-with-SHA256",
        ["1.2.840.10045.4.3.3"] = "ecdsa-with-SHA384",
        ["1.2.840.10045.4.3.4"] = "ecdsa-with-SHA512",
        ["1.2.840.10040.4.3"] = "dsaWithSHA1",
        ["2.16.840.1.101.3.4.3.2"] = "dsa_with_SHA256",
        [Ed25519] = "ED25519",
        [Ed448] = "ED448",
    };

    /// <summary>Returns the <c>Server certificate:</c> block.</summary>
    /// <param name="certificate">The server's certificate.</param>
    /// <returns>The five lines, without the <c>* </c> prefix.</returns>
    /// <remarks>A name OpenSSL cannot print is <c>[NONE]</c>, as curl prints the subject.</remarks>
    internal static IReadOnlyList<string> ServerCertificate(X509Certificate2 certificate)
    {
        return
        [
            "Server certificate:",
            "  subject: " + (OpenSslDistinguishedNameText.Format(certificate.SubjectName) ?? "[NONE]"),
            "  start date: " + FormatTime(certificate.NotBefore),
            "  expire date: " + FormatTime(certificate.NotAfter),
            "  issuer: " + (OpenSslDistinguishedNameText.Format(certificate.IssuerName) ?? "[NONE]"),
        ];
    }

    /// <summary>Returns the <c>Certificate level</c> line for one certificate in the chain.</summary>
    /// <param name="level">The certificate's place in the chain, 0 for the server's own.</param>
    /// <param name="certificate">The certificate.</param>
    /// <returns>
    /// The line, or <see langword="null"/> for a key this tool cannot describe as OpenSSL
    /// does, which curl would skip only for a key OpenSSL cannot read.
    /// </returns>
    internal static string? CertificateLevel(int level, X509Certificate2 certificate)
    {
        var key = PublicKeyText(certificate.PublicKey);
        if (key is null)
        {
            return null;
        }

        var algorithm = certificate.SignatureAlgorithm.Value!;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"  Certificate level {level}: Public key type {key} Bits/secBits), signed using {SignatureAlgorithmNames.GetValueOrDefault(algorithm, algorithm)}");
    }

    /// <summary>
    /// Returns the key's OpenSSL type name, group and sizes as curl joins them, up to the
    /// <c> Bits/secBits)</c> that follows: <c>RSA (2048/112</c>, <c>EC/prime256v1 (256/128</c>.
    /// </summary>
    /// <param name="key">The public key.</param>
    /// <returns>The text, or <see langword="null"/> for a key type or curve not described.</returns>
    internal static string? PublicKeyText(PublicKey key)
    {
        try
        {
            return key.Oid.Value switch
            {
                RsaEncryption => RsaText("RSA", key),
                RsassaPss => RsaText("RSA-PSS", key),
                EcPublicKey => EcText(key),
                Ed25519 => "ED25519 (253/128",
                Ed448 => "ED448 (456/224",
                _ => null,
            };
        }
        catch (AsnContentException)
        {
            return null;
        }
    }

    private static string RsaText(string typeName, PublicKey key)
    {
        var rsaPublicKey = new AsnReader(key.EncodedKeyValue.RawData, AsnEncodingRules.BER).ReadSequence();
        var modulusBits = (int)rsaPublicKey.ReadInteger().GetBitLength();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{typeName} ({modulusBits}/{OpenSslSecurityBits.ForModulusBits(modulusBits)}");
    }

    private static string? EcText(PublicKey key)
    {
        var curve = new AsnReader(key.EncodedParameters?.RawData ?? [], AsnEncodingRules.BER).ReadObjectIdentifier();
        return NamedCurves.TryGetValue(curve, out var sizes) ? "EC/" + sizes : null;
    }

    // ASN1_TIME_print: the month's abbreviation, the day padded to two with a space, the
    // time, the year and GMT.
    private static string FormatTime(DateTime time)
    {
        var utc = time.ToUniversalTime();
        return string.Create(CultureInfo.InvariantCulture, $"{utc:MMM} {utc.Day,2} {utc:HH:mm:ss} {utc.Year} GMT");
    }
}
