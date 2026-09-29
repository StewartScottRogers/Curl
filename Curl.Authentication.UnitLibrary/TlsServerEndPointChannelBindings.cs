using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Curl.Authentication;

/// <summary>
/// Makes the <c>tls-server-end-point</c> channel bindings' application data curl with MIT
/// passes to GSS-API over HTTPS (RFC 5929 section 4.1, BL-832): <c>tls-server-end-point:</c>
/// followed by the hash of the server certificate's DER, the hash being the one its
/// signature algorithm uses, or SHA-256 when that is MD5 or SHA-1.
/// </summary>
internal static class TlsServerEndPointChannelBindings
{
    private static readonly byte[] Prefix = Encoding.ASCII.GetBytes("tls-server-end-point:");

    /// <summary>
    /// The hash RFC 5929 section 4.1 takes for each signature algorithm, by OID: SHA-256 for
    /// MD5 and SHA-1, else the algorithm's own, for RSA PKCS #1, ECDSA and DSA.
    /// </summary>
    private static readonly FrozenDictionary<string, HashAlgorithmName> HashBySignatureAlgorithm = new Dictionary<string, HashAlgorithmName>
    {
        ["1.2.840.113549.1.1.4"] = HashAlgorithmName.SHA256,
        ["1.2.840.113549.1.1.5"] = HashAlgorithmName.SHA256,
        ["1.2.840.10045.4.1"] = HashAlgorithmName.SHA256,
        ["1.2.840.10040.4.3"] = HashAlgorithmName.SHA256,
        ["1.2.840.113549.1.1.11"] = HashAlgorithmName.SHA256,
        ["1.2.840.10045.4.3.2"] = HashAlgorithmName.SHA256,
        ["2.16.840.1.101.3.4.3.2"] = HashAlgorithmName.SHA256,
        ["1.2.840.113549.1.1.12"] = HashAlgorithmName.SHA384,
        ["1.2.840.10045.4.3.3"] = HashAlgorithmName.SHA384,
        ["1.2.840.113549.1.1.13"] = HashAlgorithmName.SHA512,
        ["1.2.840.10045.4.3.4"] = HashAlgorithmName.SHA512,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Gets the application data for <paramref name="serverCertificate" />, or
    /// <see langword="null" /> when there is no certificate (plain HTTP) or its signature
    /// algorithm names no hash this knows (RSASSA-PSS, Ed25519, SHA-224).
    /// </summary>
    /// <param name="serverCertificate">The server certificate's DER; empty for none.</param>
    /// <returns>The application data, or <see langword="null" /> for no bindings.</returns>
    internal static byte[]? Of(ReadOnlyMemory<byte> serverCertificate)
    {
        if (serverCertificate.IsEmpty)
        {
            return null;
        }

        using X509Certificate2 certificate = X509CertificateLoader.LoadCertificate(serverCertificate.Span);
        return HashBySignatureAlgorithm.TryGetValue(certificate.SignatureAlgorithm.Value!, out HashAlgorithmName hash)
            ? [.. Prefix, .. CryptographicOperations.HashData(hash, serverCertificate.Span)]
            : null;
    }
}
