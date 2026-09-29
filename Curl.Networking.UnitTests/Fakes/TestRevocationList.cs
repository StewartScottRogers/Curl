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

    /// <summary>Writes a list issued in <paramref name="issuerName" />'s name and signed by <paramref name="signer" />'s key.</summary>
    /// <param name="signer">The certificate whose private key signs; RSA signs PKCS #1 SHA-256, ECDSA signs SHA-256.</param>
    /// <param name="issuerName">The issuer name written; the signer's subject when <see langword="null" />.</param>
    /// <param name="thisUpdate">The list's issue date; an hour ago when <see langword="null" />.</param>
    /// <param name="nextUpdate">The list's expiry; none is written when <see langword="null" />.</param>
    /// <param name="revokedSerialNumbers">The big-endian serial numbers revoked; none when <see langword="null" />.</param>
    /// <param name="writeVersion">Whether to write the v2 version number.</param>
    /// <param name="generalizedTimes">Whether to write the dates as GeneralizedTime rather than UTCTime.</param>
    /// <param name="signatureAlgorithm">The algorithm named; the signer's natural one when <see langword="null" />.</param>
    /// <returns>The list's DER.</returns>
    internal static byte[] Write(
        X509Certificate2 signer,
        X500DistinguishedName? issuerName = null,
        DateTimeOffset? thisUpdate = null,
        DateTimeOffset? nextUpdate = null,
        IReadOnlyList<byte[]>? revokedSerialNumbers = null,
        bool writeVersion = true,
        bool generalizedTimes = false,
        string? signatureAlgorithm = null)
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
                        }
                    }
                }
            }
        }

        var signedPart = signed.Encode();
        var signature = rsa is not null
            ? rsa.SignData(signedPart, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            : ecdsa!.SignData(signedPart, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        var list = new AsnWriter(AsnEncodingRules.DER);
        using (list.PushSequence())
        {
            list.WriteEncodedValue(signedPart);
            WriteAlgorithm(list, algorithm);
            list.WriteBitString(signature);
        }

        return list.Encode();
    }

    /// <summary>The list as a PEM <c>X509 CRL</c> block.</summary>
    /// <param name="der">The list's DER.</param>
    /// <returns>The PEM text.</returns>
    internal static string Pem(byte[] der) => PemEncoding.WriteString("X509 CRL", der) + "\n";

    private static void WriteAlgorithm(AsnWriter writer, string algorithm)
    {
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(algorithm);
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
