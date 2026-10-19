using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// <c>CURLOPT_PROXY_TRANSFER_MODE</c> (BL-2718): an <c>ftp</c> URL forwarded through an HTTP proxy
/// with the option set gets <c>;type=a</c> (under <c>CURLOPT_TRANSFERTEXT</c>) or <c>;type=i</c> on
/// its path, unless the path already ends in <c>;type=</c> and one of <c>a</c>, <c>d</c> or <c>i</c>,
/// as curl 8.22.0's <c>http_target</c> in <c>lib/http.c</c> appends it to the request target it sends
/// the proxy (upstream tests 549 and 550).
/// </summary>
internal static class ProxyTransferModeUrl
{
    /// <summary>The marker the path's transfer type follows.</summary>
    private const string TypeMarker = ";type=";

    /// <summary>
    /// Gives <paramref name="url" /> with the transfer type on its path when <paramref name="proxyTransferMode" />
    /// is set and the transfer is an <c>ftp</c> one forwarded through an HTTP or HTTP/1.0 proxy without a tunnel;
    /// <paramref name="url" /> as it is otherwise.
    /// </summary>
    /// <param name="url">The URL to transfer.</param>
    /// <param name="proxy">The transfer's proxy, or <see langword="null" /> for none.</param>
    /// <param name="tunnels">Whether the transfer tunnels through the proxy with <c>CONNECT</c>.</param>
    /// <param name="proxyTransferMode">Whether <c>CURLOPT_PROXY_TRANSFER_MODE</c> is set.</param>
    /// <param name="ascii">Whether <c>CURLOPT_TRANSFERTEXT</c> asks for ASCII, which is <c>a</c> rather than <c>i</c>.</param>
    /// <param name="pathAsIs">Whether <c>--path-as-is</c> keeps the path's dot segments when the URL is parsed again.</param>
    /// <returns>The URL the transfer sends.</returns>
    internal static CurlUrl Of(CurlUrl url, ProxyEndpoint? proxy, bool tunnels, bool proxyTransferMode, bool ascii, bool pathAsIs) =>
        proxyTransferMode && IsForwardedFtp(url, proxy, tunnels) && !HasTypeCode(url.AbsolutePath)
            // A URL that parsed still parses with ";type=" and a letter on its path, so this cannot throw.
            ? CurlUrl.Parse(WithType(url.OriginalString, ascii ? 'a' : 'i'), pathAsIs)
            : url;

    /// <summary>Whether the transfer is an <c>ftp</c> one forwarded through an HTTP or HTTP/1.0 proxy without a tunnel.</summary>
    /// <param name="url">The URL to transfer.</param>
    /// <param name="proxy">The transfer's proxy, or <see langword="null" /> for none.</param>
    /// <param name="tunnels">Whether the transfer tunnels through the proxy with <c>CONNECT</c>.</param>
    /// <returns><see langword="true" /> when the proxy is sent the whole <c>ftp</c> URL.</returns>
    private static bool IsForwardedFtp(CurlUrl url, ProxyEndpoint? proxy, bool tunnels) =>
        !tunnels && url.Scheme == "ftp" && proxy is { Kind: ProxyKind.Http or ProxyKind.Http10 };

    /// <summary>Whether <paramref name="path" /> ends in <c>;type=</c> and one of <c>a</c>, <c>d</c> or <c>i</c>, in either case.</summary>
    /// <param name="path">The URL's path.</param>
    /// <returns><see langword="true" /> when the path already names its transfer type.</returns>
    private static bool HasTypeCode(string path)
    {
        int marker = path.LastIndexOf(TypeMarker, StringComparison.OrdinalIgnoreCase);
        return marker >= 0
            && path.Length == marker + TypeMarker.Length + 1
            && char.ToUpperInvariant(path[^1]) is 'A' or 'D' or 'I';
    }

    /// <summary>
    /// <paramref name="text" /> with <c>;type=</c> and <paramref name="type" /> at the end of its path, before any
    /// query or fragment, and a <c>/</c> before it when the URL has no path.
    /// </summary>
    /// <param name="text">The URL as written.</param>
    /// <param name="type">The transfer type, <c>a</c> or <c>i</c>.</param>
    /// <returns>The URL text with the type.</returns>
    private static string WithType(string text, char type)
    {
        int schemeEnd = text.IndexOf("://", StringComparison.Ordinal);
        int authorityStart = schemeEnd < 0 ? 0 : schemeEnd + 3;
        int pathEnd = text.IndexOfAny(['?', '#'], authorityStart);
        pathEnd = pathEnd < 0 ? text.Length : pathEnd;
        bool hasPath = text.IndexOf('/', authorityStart, pathEnd - authorityStart) >= 0;
        return text.Insert(pathEnd, (hasPath ? string.Empty : "/") + TypeMarker + type);
    }
}
