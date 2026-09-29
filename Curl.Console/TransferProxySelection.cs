using System.Diagnostics.CodeAnalysis;
using System.Net;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Chooses the proxy one transfer goes through from <c>-x</c> or a <c>--socks</c> option,
/// <c>--noproxy</c>, <c>-U</c> and the proxy environment variables.
/// </summary>
/// <remarks>
/// <see cref="ProxySelector" /> picks the proxy (ADR-0024); <c>-U</c>/<c>--proxy-user</c> then
/// replaces its credential, as curl 8.21.0 lets <c>-U</c> win over the proxy URL's user
/// information. The chosen proxy reaches both <see cref="ITransferContext.Proxy" />, which a
/// handler connecting over TCP hands to the connector's tunnel (ADR-0056), and
/// <see cref="Curl.Protocol.Abstractions.HttpRequestOptions.ForwardProxy" />, where the HTTP
/// handler forwards a plain <c>http</c> request itself and hands every other route to the
/// connector, which tunnels through HTTP, HTTPS and SOCKS proxies alike (BL-183, BL-212,
/// BL-213, BL-266). A <c>file</c> transfer never uses a proxy: it is not selected at all, so
/// even proxy text curl cannot use is ignored, as curl 8.21.0 ignores <c>-x foo://h:1</c> for a
/// <c>file://</c> URL (measured 2026-09-27, BL-338 Notes).
/// </remarks>
internal static class TransferProxySelection
{
    /// <summary>The one scheme that never uses a proxy.</summary>
    private const string FileScheme = "file";

    /// <summary>
    /// Chooses the proxy for <paramref name="url" />.
    /// </summary>
    /// <param name="selector">Reads <c>-x</c>, <c>--noproxy</c> and the proxy environment variables.</param>
    /// <param name="options">The accepted command line.</param>
    /// <param name="url">The URL being transferred.</param>
    /// <param name="proxy">
    /// The proxy, with the <c>-U</c> credential when one was given; <see langword="null" /> for a
    /// direct connection, a <c>file</c> URL, a transfer through a Unix domain socket or a failure.
    /// </param>
    /// <param name="failure">
    /// The selector's failure for proxy text curl cannot use; <see langword="null" /> otherwise.
    /// </param>
    /// <returns><see langword="true" /> unless the transfer must end with <paramref name="failure" />.</returns>
    internal static bool TrySelect(
        ProxySelector selector,
        CommandLineOptions options,
        CurlUrl url,
        out ProxyEndpoint? proxy,
        [NotNullWhen(false)] out TransferResult? failure)
    {
        proxy = null;
        failure = null;
        // curl 8.21.0 drops the proxy, unread, when --unix-socket or --abstract-unix-socket is given
        // ("do not mix proxy and Unix domain sockets"; measured, BL-507).
        if (url.Scheme == FileScheme || options.UnixSocketPath is not null)
        {
            return true;
        }

        if (!selector.TrySelect(url, options.Proxy?.Address, KindWithoutSchemeOf(options.Proxy), options.NoProxy, out ProxyEndpoint? selected, out failure))
        {
            return false;
        }

        proxy = WithProxyUser(selected, options.ProxyCredentials);
        return true;
    }

    /// <summary>The kind proxy option text with no scheme names: the option's own, or HTTP when none was given.</summary>
    private static ProxyKind KindWithoutSchemeOf(CommandLineProxy? proxyOption) =>
        proxyOption?.KindWithoutScheme ?? ProxyKind.Http;

    /// <summary>Replaces the proxy's credential with the <c>-U</c> one, when both are present.</summary>
    private static ProxyEndpoint? WithProxyUser(ProxyEndpoint? proxy, NetworkCredential? proxyUser) =>
        proxy is not null && proxyUser is not null ? proxy with { Credential = proxyUser } : proxy;
}
