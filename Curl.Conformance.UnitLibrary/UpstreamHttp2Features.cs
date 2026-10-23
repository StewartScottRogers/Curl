namespace Curl.Conformance;

/// <summary>The HTTP/2 features every platform reports, since Curl runs <c>--http2</c> on Windows as elsewhere (ADR-0786, BL-3268).</summary>
internal static class UpstreamHttp2Features
{
    public static readonly string[] Names =
    [
        "http/2",
        "h2c",
    ];
}
