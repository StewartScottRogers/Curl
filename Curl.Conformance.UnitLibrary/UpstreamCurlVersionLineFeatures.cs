namespace Curl.Conformance;

/// <summary>The features on Curl's <c>curl -V</c> <c>Features:</c> line that every platform has, plus <c>ECH</c> (ADR-0520); no platform has <c>HTTPSRR</c> (ADR-0956).</summary>
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
