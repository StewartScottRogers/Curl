using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// <c>CURLOPT_PROXY_TRANSFER_MODE</c> (BL-2718, BL-2793): the transfer type, <c>a</c> under
/// <c>CURLOPT_TRANSFERTEXT</c> or <c>i</c>, that <see cref="Curl.Protocol.Abstractions.HttpRequestOptions.ProxyTransferType" /> carries to the
/// HTTP handler, which puts <c>;type=</c> and it on the path of the absolute-form target it sends a forward proxy
/// for an <c>ftp</c> URL, as curl 8.22.0's <c>http_target</c> in <c>lib/http.c</c> does (upstream tests 549 and
/// 550). The transfer's URL stays as it is, so the effective URL does not carry the type.
/// </summary>
internal static class ProxyTransferModeType
{
    /// <summary>
    /// Gives the transfer type when <paramref name="proxyTransferMode" /> is set and the transfer is forwarded
    /// through an HTTP or HTTP/1.0 proxy without a tunnel; <see langword="null" /> otherwise.
    /// </summary>
    /// <param name="proxy">The transfer's proxy, or <see langword="null" /> for none.</param>
    /// <param name="tunnels">Whether the transfer tunnels through the proxy with <c>CONNECT</c>.</param>
    /// <param name="proxyTransferMode">Whether <c>CURLOPT_PROXY_TRANSFER_MODE</c> is set.</param>
    /// <param name="ascii">Whether <c>CURLOPT_TRANSFERTEXT</c> asks for ASCII, which is <c>a</c> rather than <c>i</c>.</param>
    /// <returns>The transfer type, or <see langword="null" /> for none.</returns>
    internal static char? Of(ProxyEndpoint? proxy, bool tunnels, bool proxyTransferMode, bool ascii) =>
        proxyTransferMode && IsForwarded(proxy, tunnels) ? LetterOf(ascii) : null;

    /// <summary>Whether the transfer is forwarded through an HTTP or HTTP/1.0 proxy without a tunnel.</summary>
    /// <param name="proxy">The transfer's proxy, or <see langword="null" /> for none.</param>
    /// <param name="tunnels">Whether the transfer tunnels through the proxy with <c>CONNECT</c>.</param>
    /// <returns><see langword="true" /> when the proxy is sent the whole URL.</returns>
    private static bool IsForwarded(ProxyEndpoint? proxy, bool tunnels) =>
        !tunnels && proxy is { Kind: ProxyKind.Http or ProxyKind.Http10 };

    /// <summary>Gives <c>a</c> for ASCII and <c>i</c> for binary.</summary>
    /// <param name="ascii">Whether <c>CURLOPT_TRANSFERTEXT</c> asks for ASCII.</param>
    /// <returns>The transfer type.</returns>
    private static char LetterOf(bool ascii) => ascii ? 'a' : 'i';
}
