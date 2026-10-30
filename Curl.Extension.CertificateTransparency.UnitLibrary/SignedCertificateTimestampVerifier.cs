using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Extension.CertificateTransparency;

/// <summary>
/// Decides whether a server's certificate carries a valid SCT (RFC 6962 §5.2, RFC 9162 §8.1.3): an embedded SCT from a
/// log in the list, no later than now, whose signature the log's key verifies over the reconstructed precertificate entry.
/// </summary>
internal static class SignedCertificateTimestampVerifier
{
    /// <summary>The TLS <c>HashAlgorithm</c> value of SHA-256.</summary>
    internal const byte Sha256 = 4;

    /// <summary>The TLS <c>SignatureAlgorithm</c> value of RSA (PKCS #1 v1.5).</summary>
    internal const byte Rsa = 1;

    /// <summary>The TLS <c>SignatureAlgorithm</c> value of ECDSA.</summary>
    internal const byte Ecdsa = 3;

    /// <summary>The refusal when the server sent its certificate alone.</summary>
    internal const string NoIssuer = "Certificate Transparency: the server sent no issuer certificate to check its SCTs against";

    /// <summary>The refusal when no SCT is valid.</summary>
    internal const string NoValidTimestamp = "Certificate Transparency: the server certificate carries no valid SCT from a known log";

    /// <summary>The refusal when the certificate or its SCT list cannot be read.</summary>
    internal const string Malformed = "Certificate Transparency: the server certificate's SCT list is malformed";

    /// <summary>Checks the chain.</summary>
    /// <param name="peerCertificates">The DER certificates the server sent, its own first and its issuer next.</param>
    /// <param name="keysByLogId">Each known log's DER public key, by log ID in upper-case hexadecimal.</param>
    /// <param name="now">The time no SCT may be later than.</param>
    /// <returns><see langword="null" /> when at least one SCT is valid, otherwise why the chain is refused.</returns>
    internal static string? RefusalOf(IReadOnlyList<ReadOnlyMemory<byte>> peerCertificates, IReadOnlyDictionary<string, byte[]> keysByLogId, DateTimeOffset now)
    {
        if (peerCertificates.Count < 2)
        {
            return NoIssuer;
        }

        try
        {
            return HasValidTimestamp(peerCertificates[0], peerCertificates[1], keysByLogId, now) ? null : NoValidTimestamp;
        }
        catch (Exception exception) when (exception is AsnContentException or CryptographicException or FormatException)
        {
            return Malformed;
        }
    }

    /// <summary>Checks one SCT's signature with a log's key.</summary>
    /// <param name="logKey">The log's DER <c>SubjectPublicKeyInfo</c>.</param>
    /// <param name="timestamp">The SCT.</param>
    /// <param name="signedData">The data the log signed.</param>
    /// <returns>Whether the signature is SHA-256 with ECDSA or RSA and the key verifies it.</returns>
    internal static bool SignatureVerifies(byte[] logKey, SignedCertificateTimestamp timestamp, byte[] signedData)
    {
        if (timestamp.HashAlgorithm != Sha256)
        {
            return false;
        }

        try
        {
            return timestamp.SignatureAlgorithm switch
            {
                Ecdsa => EcdsaVerifies(logKey, signedData, timestamp.Signature.Span),
                Rsa => RsaVerifies(logKey, signedData, timestamp.Signature.Span),
                _ => false,
            };
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static bool HasValidTimestamp(ReadOnlyMemory<byte> leaf, ReadOnlyMemory<byte> issuer, IReadOnlyDictionary<string, byte[]> keysByLogId, DateTimeOffset now)
    {
        if (EmbeddedTimestamps.Of(leaf) is not { } embedded)
        {
            return false;
        }

        var issuerKeyHash = SHA256.HashData(IssuerKeyOf(issuer));
        var latest = (ulong)now.ToUnixTimeMilliseconds();
        return SignedCertificateTimestamp.ParseList(embedded.List).Any(timestamp =>
            timestamp.Timestamp <= latest
            && keysByLogId.TryGetValue(Convert.ToHexString(timestamp.LogId.Span), out var logKey)
            && SignatureVerifies(logKey, timestamp, timestamp.SignedDataFor(issuerKeyHash, embedded.TbsCertificate.Span)));
    }

    private static byte[] IssuerKeyOf(ReadOnlyMemory<byte> issuer)
    {
        using var certificate = X509CertificateLoader.LoadCertificate(issuer.Span);
        return certificate.PublicKey.ExportSubjectPublicKeyInfo();
    }

    private static bool EcdsaVerifies(byte[] logKey, byte[] signedData, ReadOnlySpan<byte> signature)
    {
        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(logKey, out _);
        return key.VerifyData(signedData, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
    }

    private static bool RsaVerifies(byte[] logKey, byte[] signedData, ReadOnlySpan<byte> signature)
    {
        using var key = RSA.Create();
        key.ImportSubjectPublicKeyInfo(logKey, out _);
        return key.VerifyData(signedData, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }
}
