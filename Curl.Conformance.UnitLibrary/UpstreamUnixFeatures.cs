namespace Curl.Conformance;

/// <summary>The features Linux and macOS report: the OpenSSL build, extended attributes, and HTTP/2 (BL-2230).</summary>
internal static class UpstreamUnixFeatures
{
    public static readonly string[] Names =
    [
        "OpenSSL",
        "SSLS-EXPORT",
        "xattr",
        "http/2",
        "h2c",
    ];
}
