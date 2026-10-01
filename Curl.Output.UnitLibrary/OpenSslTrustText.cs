using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// curl 8.21.0's OpenSSL-build <c>-v</c> lines for the trust a connection is set up with:
/// a port of the <c>SSL Trust</c> lines of <c>ossl_populate_x509_store</c> and
/// <c>ossl_load_trust_anchors</c> in <c>lib/vtls/openssl.c</c> on Linux and macOS (ADR-0085),
/// which curl.se's LibreSSL build also prints for a QUIC connect on Windows (ADR-0144).
/// </summary>
internal static class OpenSslTrustText
{
    /// <summary>Returns the lines for the trust.</summary>
    /// <param name="trust">The trust.</param>
    /// <returns>The lines, without the <c>* </c> prefix.</returns>
    internal static IReadOnlyList<string> Lines(TlsTrustEvent trust)
    {
        if (!trust.VerifiesPeer)
        {
            return ["SSL Trust: peer verification disabled"];
        }

        var sources = TrustAnchorSources(trust);
        return ["SSL Trust Anchors:", .. sources.Count == 0 ? ["  no trust anchors configured"] : sources];
    }

    // A blob overrides the file, as ossl_load_trust_anchors reads CURLOPT_CAINFO_BLOB first.
    // The Windows stores come before both (curl.se's build with --ca-native, measured, BL-1050).
    private static List<string> TrustAnchorSources(TlsTrustEvent trust)
    {
        string?[] sources =
        [
            trust.UsesWindowsSystemStores ? "  Native: Windows System Stores ROOT+CA" : null,
            trust.HasCaCertificateBlob ? "  CA Blob from configuration" : null,
            !trust.HasCaCertificateBlob && trust.CaCertificateFile is { } file ? "  CAfile: " + file : null,
            trust.CaCertificateDirectory is { } directory ? "  CApath: " + directory : null,
        ];
        return sources.OfType<string>().ToList();
    }
}
