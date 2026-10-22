namespace Curl.Conformance;

/// <summary>
/// The features Linux and macOS report: the OpenSSL build, TLS session export (<c>SSLS-EXPORT</c>, which
/// <c>runtests.pl</c> also names <c>ssl-sessions</c>, BL-2858), extended attributes, and HTTP/2 (BL-2230).
/// </summary>
internal static class UpstreamUnixFeatures
{
    public static readonly string[] Names =
    [
        "OpenSSL",
        "SSLS-EXPORT",
        "ssl-sessions",
        "xattr",
        "http/2",
        "h2c",
    ];
}
