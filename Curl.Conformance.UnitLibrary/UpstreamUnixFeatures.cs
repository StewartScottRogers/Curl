namespace Curl.Conformance;

/// <summary>
/// The features Linux and macOS report: HTTPS records through DoH (<c>HTTPSRR</c>, which the Schannel build lacks, ADR-0849), the OpenSSL build, TLS session export (<c>SSLS-EXPORT</c>, which
/// <c>runtests.pl</c> also names <c>ssl-sessions</c>, BL-2858), and extended attributes.
/// </summary>
internal static class UpstreamUnixFeatures
{
    public static readonly string[] Names =
    [
        "HTTPSRR",
        "OpenSSL",
        "SSLS-EXPORT",
        "ssl-sessions",
        "xattr",
    ];
}
