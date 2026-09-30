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

        if (!string.IsNullOrEmpty(options.PreProxy))
        {
            return TrySelectBehindPreProxy(selector, options, options.PreProxy, url, out proxy, out failure);
        }

        if (!selector.TrySelect(url, options.Proxy?.Address, KindWithoutSchemeOf(options.Proxy), options.NoProxy, out ProxyEndpoint? selected, out failure))
        {
            return false;
        }

        proxy = WithProxyUser(selected, options.ProxyCredentials);
        return true;
    }

    /// <summary>
    /// Chooses the proxy when <c>--preproxy</c> names one, as curl 8.21.0 does (measured, BL-614):
    /// <c>--noproxy</c> matching the host drops both; the pre-proxy text is read first, with no scheme
    /// as SOCKS4, and an <c>http</c> or <c>https</c> one is exit 5 <c>Unsupported pre-proxy type for
    /// '&lt;text&gt;'</c>; with no <c>-x</c> (the environment is not read) the pre-proxy is the SOCKS
    /// proxy itself; a SOCKS <c>-x</c> is exit 5 <c>Having a SOCKS pre-proxy and proxy is not supported
    /// with '&lt;-x&gt;'</c>; an HTTP or HTTPS <c>-x</c> is the proxy, which the connector reaches
    /// through the pre-proxy (<see cref="CurlComposition.PreProxyOf" />).
    /// </summary>
    private static bool TrySelectBehindPreProxy(
        ProxySelector selector,
        CommandLineOptions options,
        string preProxyText,
        CurlUrl url,
        out ProxyEndpoint? proxy,
        [NotNullWhen(false)] out TransferResult? failure)
    {
        proxy = null;
        if (!TrySelectPreProxy(selector, options, preProxyText, url, out ProxyEndpoint? preProxy, out failure)
            || preProxy is null
            || !TrySelectProxyBehindPreProxy(selector, options, url, out ProxyEndpoint? selected, out failure))
        {
            return failure is null;
        }

        proxy = WithProxyUser(selected ?? preProxy, options.ProxyCredentials);
        return true;
    }

    /// <summary>
    /// Reads the <c>--preproxy</c> text, with no scheme as SOCKS4: <see langword="null" /> when
    /// <c>--noproxy</c> matches the host, and exit 5 for an HTTP or HTTPS pre-proxy.
    /// </summary>
    private static bool TrySelectPreProxy(
        ProxySelector selector,
        CommandLineOptions options,
        string preProxyText,
        CurlUrl url,
        out ProxyEndpoint? preProxy,
        [NotNullWhen(false)] out TransferResult? failure)
    {
        if (!selector.TrySelect(url, preProxyText, ProxyKind.Socks4, options.NoProxy, out preProxy, out failure))
        {
            return false;
        }

        if (preProxy is not null && IsHttpKind(preProxy.Kind))
        {
            failure = TransferResult.Failure(CurlExitCode.CouldntResolveProxy, $"Unsupported pre-proxy type for '{preProxyText}'");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads the <c>-x</c> or <c>--socks</c> proxy behind a pre-proxy: <see langword="null" /> when
    /// none was given or it is empty, and exit 5 for a SOCKS one.
    /// </summary>
    private static bool TrySelectProxyBehindPreProxy(
        ProxySelector selector,
        CommandLineOptions options,
        CurlUrl url,
        out ProxyEndpoint? selected,
        [NotNullWhen(false)] out TransferResult? failure)
    {
        selected = null;
        failure = null;
        if (options.Proxy is not { } proxyOption)
        {
            return true;
        }

        if (!selector.TrySelect(url, proxyOption.Address, KindWithoutSchemeOf(proxyOption), options.NoProxy, out selected, out failure))
        {
            return false;
        }

        if (selected is not null && !IsHttpKind(selected.Kind))
        {
            failure = TransferResult.Failure(
                CurlExitCode.CouldntResolveProxy,
                $"Having a SOCKS pre-proxy and proxy is not supported with '{proxyOption.Address}'");
            return false;
        }

        return true;
    }

    /// <summary>Whether <paramref name="kind" /> is an HTTP or HTTPS proxy, which a pre-proxy can lead to.</summary>
    private static bool IsHttpKind(ProxyKind kind) => kind is ProxyKind.Http or ProxyKind.Http10 or ProxyKind.Https;

    /// <summary>The kind proxy option text with no scheme names: the option's own, or HTTP when none was given.</summary>
    private static ProxyKind KindWithoutSchemeOf(CommandLineProxy? proxyOption) =>
        proxyOption?.KindWithoutScheme ?? ProxyKind.Http;

    /// <summary>Replaces the proxy's credential with the <c>-U</c> one, when both are present.</summary>
    private static ProxyEndpoint? WithProxyUser(ProxyEndpoint? proxy, NetworkCredential? proxyUser) =>
        proxy is not null && proxyUser is not null ? proxy with { Credential = proxyUser } : proxy;
}
