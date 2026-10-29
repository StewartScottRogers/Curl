namespace Curl.Conformance;

/// <summary>
/// The features Linux and macOS report: the OpenSSL build (which, like the Schannel build, has no <c>HTTPSRR</c>, ADR-0957), TLS session export (<c>SSLS-EXPORT</c>, which
/// <c>runtests.pl</c> also names <c>ssl-sessions</c>, BL-2858), and extended attributes.
/// </summary>
internal static class UpstreamUnixFeatures
{
    public static readonly string[] Names =
    [
        "OpenSSL",
        "SSLS-EXPORT",
        "ssl-sessions",
        "xattr",
    ];
}
