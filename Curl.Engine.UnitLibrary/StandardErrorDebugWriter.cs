using Curl.Api.Callbacks;

namespace Curl.Engine;

/// <summary>
/// libcurl's default debug output, the <c>CURLOPT_VERBOSE</c> lines a handle with no <c>CURLOPT_DEBUGFUNCTION</c>
/// writes (BL-2726): as curl 8.22.0's <c>Curl_debug</c> in <c>lib/curl_trace.c</c>, each info text, outgoing header
/// and incoming header call writes <c>* </c>, <c>&gt; </c> or <c>&lt; </c> and then its bytes as given; data and
/// TLS bytes are not written.
/// </summary>
/// <param name="standardError">Where the lines go: <c>CURLOPT_STDERR</c>, else the engine's standard error. It is left open.</param>
internal sealed class StandardErrorDebugWriter(Stream standardError)
{
    private static readonly byte[] TextPrefix = "* "u8.ToArray();
    private static readonly byte[] HeaderInPrefix = "< "u8.ToArray();
    private static readonly byte[] HeaderOutPrefix = "> "u8.ToArray();

    /// <summary>Writes one debug call's bytes behind its type's prefix, or nothing for a type libcurl does not write.</summary>
    /// <param name="type">The kind of bytes.</param>
    /// <param name="data">The bytes.</param>
    /// <returns>0, which libcurl's default ignores as it ignores every debug callback's answer.</returns>
    internal int Write(CurlInfoType type, ReadOnlySpan<byte> data)
    {
        if (PrefixOf(type) is { } prefix)
        {
            standardError.Write(prefix);
            standardError.Write(data);
            standardError.Flush();
        }

        return 0;
    }

    private static byte[]? PrefixOf(CurlInfoType type) => type switch
    {
        CurlInfoType.Text => TextPrefix,
        CurlInfoType.HeaderIn => HeaderInPrefix,
        CurlInfoType.HeaderOut => HeaderOutPrefix,
        _ => null,
    };
}
