using System.Collections.Frozen;
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Makes the <c>tls-server-end-point</c> channel bindings' application data curl with MIT
/// passes to GSS-API over HTTPS (RFC 5929 section 4.1, BL-832): <c>tls-server-end-point:</c>
/// followed by the hash of the server certificate's DER, the hash being the one its
/// signature algorithm uses, or SHA-256 when that is MD5 or SHA-1. A signature algorithm
/// that names no hash fails the transfer with exit 91, as curl 8.18.0's OpenSSL build was
/// measured doing (ADR-0234, BL-965).
/// </summary>
internal static class TlsServerEndPointChannelBindings
{
    /// <summary>
    /// What curl 8.18.0's OpenSSL build prints when the signature algorithm is known but names
    /// no digest (RSASSA-PSS, Ed25519, Ed448): OpenSSL gives it <c>NID_undef</c> for the
    /// digest. Measured, BL-965.
    /// </summary>
    internal const string NoDigestAlgorithmMessage = "Could not find digest algorithm UNDEF (NID 0)";

    /// <summary>
    /// What curl 8.18.0's OpenSSL build prints when OpenSSL does not know the signature
    /// algorithm at all. Read from curl's source; OpenSSL's own server refuses such a
    /// certificate, so it was not measured (BL-965).
    /// </summary>
    internal const string NoDigestNidMessage = "Unable to find digest NID for certificate signature algorithm";

    private static readonly byte[] Prefix = Encoding.ASCII.GetBytes("tls-server-end-point:");

    /// <summary>
    /// The hash RFC 5929 section 4.1 takes for each signature algorithm, by OID: SHA-256 for
    /// MD5 and SHA-1, else the algorithm's own, for RSA PKCS #1, ECDSA and DSA.
    /// </summary>
    private static readonly FrozenDictionary<string, CertificateHash> HashBySignatureAlgorithm = new Dictionary<string, CertificateHash>
    {
        ["1.2.840.113549.1.1.4"] = SHA256.HashData,
        ["1.2.840.113549.1.1.5"] = SHA256.HashData,
        ["1.2.840.10045.4.1"] = SHA256.HashData,
        ["1.2.840.10040.4.3"] = SHA256.HashData,
        ["1.2.840.113549.1.1.14"] = Sha224.HashData,
        ["1.2.840.10045.4.3.1"] = Sha224.HashData,
        ["2.16.840.1.101.3.4.3.1"] = Sha224.HashData,
        ["1.2.840.113549.1.1.11"] = SHA256.HashData,
        ["1.2.840.10045.4.3.2"] = SHA256.HashData,
        ["2.16.840.1.101.3.4.3.2"] = SHA256.HashData,
        ["1.2.840.113549.1.1.12"] = SHA384.HashData,
        ["1.2.840.10045.4.3.3"] = SHA384.HashData,
        ["1.2.840.113549.1.1.13"] = SHA512.HashData,
        ["1.2.840.10045.4.3.4"] = SHA512.HashData,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// The signature algorithms OpenSSL knows but pairs with no digest: RSASSA-PSS, whose
    /// digest sits in its parameters, Ed25519 and Ed448.
    /// </summary>
    private static readonly FrozenSet<string> DigestlessSignatureAlgorithms =
        new[] { "1.2.840.113549.1.1.10", "1.3.101.112", "1.3.101.113" }.ToFrozenSet(StringComparer.Ordinal);

    private delegate byte[] CertificateHash(ReadOnlySpan<byte> certificate);

    /// <summary>
    /// Gets the application data for <paramref name="serverCertificate" />, or
    /// <see langword="null" /> when there is no certificate (plain HTTP).
    /// </summary>
    /// <param name="serverCertificate">The server certificate's DER; empty for none.</param>
    /// <returns>The application data, or <see langword="null" /> for no bindings.</returns>
    /// <exception cref="HttpAuthenticationFailedException">
    /// The certificate's signature algorithm names no hash: exit 91 with
    /// <see cref="NoDigestAlgorithmMessage" /> or <see cref="NoDigestNidMessage" />, as curl
    /// fails the transfer before it sends the request.
    /// </exception>
    internal static byte[]? Of(ReadOnlyMemory<byte> serverCertificate)
    {
        if (serverCertificate.IsEmpty)
        {
            return null;
        }

        string signatureAlgorithm = SignatureAlgorithmOf(serverCertificate);
        if (HashBySignatureAlgorithm.TryGetValue(signatureAlgorithm, out CertificateHash? hash))
        {
            return [.. Prefix, .. hash(serverCertificate.Span)];
        }

        throw new HttpAuthenticationFailedException(
            CurlExitCode.SslInvalidCertStatus,
            DigestlessSignatureAlgorithms.Contains(signatureAlgorithm) ? NoDigestAlgorithmMessage : NoDigestNidMessage);
    }

    /// <summary>
    /// Reads the OID of the certificate's outer <c>signatureAlgorithm</c> (RFC 5280 section
    /// 4.1.1.2), the second element of its <c>Certificate</c> sequence.
    /// </summary>
    private static string SignatureAlgorithmOf(ReadOnlyMemory<byte> certificate)
    {
        AsnReader fields = new AsnReader(certificate, AsnEncodingRules.DER).ReadSequence();
        fields.ReadEncodedValue();
        return fields.ReadSequence().ReadObjectIdentifier();
    }
}
