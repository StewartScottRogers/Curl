namespace Curl.Conformance;

/// <summary>The features on Curl's <c>curl -V</c> <c>Features:</c> line that every platform has, plus <c>ECH</c> (ADR-0520); <c>HTTPSRR</c> is Linux and macOS only (ADR-0856).</summary>
internal static class UpstreamCurlVersionLineFeatures
{
    public static readonly string[] Names =
    [
        "alt-svc",
        "AsynchDNS",
        "brotli",
        "ECH",
        "GSS-API",
        "HSTS",
        "HTTPS-proxy",
        "IDN",
        "IPv6",
        "Kerberos",
        "Largefile",
        "libz",
        "NTLM",
        "PSL",
        "SPNEGO",
        "SSL",
        "UnixSockets",
        "zstd",
    ];
}
