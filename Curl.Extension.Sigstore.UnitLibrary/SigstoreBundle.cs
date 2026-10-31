using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace Curl.Extension.Sigstore;

/// <summary>
/// The parts of a Sigstore bundle (<c>application/vnd.dev.sigstore.bundle</c> v0.1 to v0.3, protobuf-specs
/// <c>sigstore_bundle.proto</c> in its proto3 JSON form) a <c>messageSignature</c> is verified with.
/// </summary>
/// <param name="Certificate">The DER signing certificate: <c>verificationMaterial.certificate</c> (v0.3), or the first of <c>verificationMaterial.x509CertificateChain.certificates</c> (v0.1, v0.2).</param>
/// <param name="DigestAlgorithm">The <c>messageSignature.messageDigest.algorithm</c>, such as <c>SHA2_256</c>.</param>
/// <param name="Digest">The <c>messageSignature.messageDigest.digest</c> bytes.</param>
/// <param name="Signature">The <c>messageSignature.signature</c> bytes.</param>
/// <param name="IntegratedTime">The first transparency log entry's <c>integratedTime</c>: when Rekor logged the signature.</param>
internal sealed record SigstoreBundle(byte[] Certificate, string DigestAlgorithm, byte[] Digest, byte[] Signature, DateTimeOffset IntegratedTime)
{
    private const string MediaTypePrefix = "application/vnd.dev.sigstore.bundle";

    /// <summary>Reads a bundle.</summary>
    /// <param name="json">The bundle's JSON bytes.</param>
    /// <returns>The bundle, or <see langword="null" /> when it is not a Sigstore bundle holding a message signature, a certificate and a transparency log entry.</returns>
    public static SigstoreBundle? Parse(ReadOnlyMemory<byte> json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("mediaType").GetString()?.StartsWith(MediaTypePrefix, StringComparison.Ordinal) != true)
            {
                return null;
            }

            var material = root.GetProperty("verificationMaterial");
            var signature = root.GetProperty("messageSignature");
            var digest = signature.GetProperty("messageDigest");
            return new SigstoreBundle(
                CertificateOf(material),
                digest.GetProperty("algorithm").GetString() ?? string.Empty,
                digest.GetProperty("digest").GetBytesFromBase64(),
                signature.GetProperty("signature").GetBytesFromBase64(),
                DateTimeOffset.FromUnixTimeSeconds(Int64Of(material.GetProperty("tlogEntries").EnumerateArray().First().GetProperty("integratedTime"))));
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Tells whether the bundle's message digest is the SHA-256 of <paramref name="artifact" />.</summary>
    /// <param name="artifact">The downloaded bytes.</param>
    /// <returns><see langword="true" /> when the algorithm is <c>SHA2_256</c> and the digest matches.</returns>
    public bool DigestMatches(ReadOnlySpan<byte> artifact) =>
        DigestAlgorithm == "SHA2_256" && CryptographicOperations.FixedTimeEquals(SHA256.HashData(artifact), Digest);

    private static byte[] CertificateOf(JsonElement material) =>
        (material.TryGetProperty("certificate", out var certificate)
            ? certificate
            : material.GetProperty("x509CertificateChain").GetProperty("certificates").EnumerateArray().First())
        .GetProperty("rawBytes").GetBytesFromBase64();

    // proto3 JSON writes an int64 as a string; a JSON number is accepted too.
    private static long Int64Of(JsonElement element) =>
        element.ValueKind == JsonValueKind.String
            ? long.Parse(element.GetString()!, NumberStyles.None, CultureInfo.InvariantCulture)
            : element.GetInt64();
}
