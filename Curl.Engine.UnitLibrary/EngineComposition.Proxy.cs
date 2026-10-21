using Curl.Api.Options.Authentication;
using Curl.Api.Options.Http;
using Curl.Api.Options.Proxy;
using Curl.Authentication;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class EngineComposition
{
    /// <summary>
    /// The SOCKS proxy the connector reaches an HTTP or HTTPS proxy through: the <c>--preproxy</c>
    /// value, with no scheme as SOCKS4 as curl 8.21.0 reads it (BL-614), or <see langword="null" />
    /// when it is absent, empty, does not parse or is not a SOCKS proxy. The last three fail each
    /// transfer in <see cref="TransferProxySelection" /> before anything connects.
    /// </summary>
    /// <param name="proxy">The option group's proxy options.</param>
    /// <returns>The pre-proxy, or <see langword="null" />.</returns>
    internal static ProxyEndpoint? PreProxyOf(EasyProxyOptions proxy) =>
        !string.IsNullOrEmpty(proxy.PreProxy)
        && ProxyUrlParser.TryParse(proxy.PreProxy, ProxyKind.Socks4, out ProxyEndpoint? preProxy, out _)
        && preProxy.Kind is not (ProxyKind.Http or ProxyKind.Https)
            ? preProxy
            : null;

    /// <summary>
    /// The HAProxy PROXY protocol v1 line the TCP connector sends first on each connection:
    /// with <c>--haproxy-protocol</c>, or with <c>--haproxy-clientip</c> alone, which turns it on
    /// even after <c>--no-haproxy-protocol</c> as curl 8.21.0's libcurl does (measured, BL-616
    /// Notes); <see langword="null" /> for neither.
    /// </summary>
    /// <param name="proxy">The option group's proxy options.</param>
    /// <returns>The header, or <see langword="null" />.</returns>
    internal static HaproxyProtocolHeader? HaproxyProtocolOf(EasyProxyOptions proxy) =>
        proxy.HaProxyProtocol || proxy.HaProxyClientIp is not null
            ? new HaproxyProtocolHeader(proxy.HaProxyClientIp)
            : null;

    /// <summary>
    /// Maps the command line to what the CONNECT request through an HTTP proxy carries: the
    /// <c>-A</c> value as its <c>User-Agent</c>, no <c>User-Agent</c> header for <c>-A ""</c>,
    /// <c>curl/8.21.0</c> without <c>-A</c>; the proxy credential encoded as the server
    /// credential is (<see cref="CredentialEncoding.ForPlatform" />, ADR-0022); and the
    /// <c>--proxy-header</c> values verbatim, never the <c>-H</c> ones (ADR-0077); the
    /// <c>-A</c> and <c>--proxy-header</c> text encoded in that same platform encoding (ADR-0067);
    /// and the proxy authenticated with the scheme the <c>--proxy-*</c> auth switches pick
    /// (<see cref="EasyAuthenticationOptions.ProxyAuthentication" />, <see cref="HttpRequestOptionsMapping.ProxyAuthSchemesOf" />), answered by the same
    /// <see cref="CreateHttpAuthenticator" /> the origin uses: Basic up front, Digest and
    /// <c>--proxy-anyauth</c> after a <c>407</c> (ADR-0186), NTLM's Type 1 and Type 3 and
    /// Negotiate's tokens on the same connection (ADR-0270). Its Negotiate and NTLM contexts come
    /// from <paramref name="securityContexts" />, with the <c>--proxy-service-name</c> and
    /// <c>--delegation</c> of <see cref="NegotiateOptionsMapping.Of" />; and an HTTP/2
    /// tunnel through an <c>https://</c> proxy when <c>--proxy-http2</c> asked for one (ADR-0408), which
    /// <paramref name="proxy" /> holds as <see cref="ProxyType.Https2" /> (ADR-0515).
    /// </summary>
    /// <param name="http">The option group's HTTP options, for <c>-A</c>.</param>
    /// <param name="proxy">The option group's proxy options, for <c>--proxy-header</c> and <c>--proxy-http2</c>.</param>
    /// <param name="authentication">The option group's authentication options, for the proxy's auth methods, <c>--proxy-service-name</c> and <c>--delegation</c>.</param>
    /// <param name="securityContexts">Makes the proxy's NTLM and Negotiate contexts: <see cref="CreateSecurityContextFactory" />'s router in production.</param>
    /// <returns>The tunnel's options.</returns>
    /// <param name="diagnosticLog">The run's diagnostic log, which the authenticators and security contexts write their choices to (BL-923); <see langword="null" /> for none.</param>
    internal static HttpProxyTunnelOptions CreateProxyTunnelOptions(EasyHttpOptions http, EasyProxyOptions proxy, EasyAuthenticationOptions authentication, ISecurityContextFactory securityContexts, IDiagnosticLog? diagnosticLog = null) =>
        new(
            http.UserAgent switch
            {
                null => HttpProxyTunnelOptions.Default.UserAgent,
                "" => null,
                string userAgent => userAgent,
            },
            CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()))
        {
            ProxyHeaders = ConnectHeadersOf(http, proxy),
            CommandLineTextEncoding = CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()),
            ProxyAuthSchemes = HttpRequestOptionsMapping.ProxyAuthSchemesOf(authentication),
            ProxyAuthenticator = CreateHttpAuthenticator(securityContexts, NegotiateOptionsMapping.Of(authentication), diagnosticLog),
            ProxyHttp2 = proxy.ProxyType == ProxyType.Https2,
        };

    /// <summary>
    /// The custom headers a proxy's <c>CONNECT</c> sends, as curl 8.22.0's <c>CURLOPT_HEADEROPT</c> picks them:
    /// <c>CURLOPT_PROXYHEADER</c>'s under <see cref="HeaderOption.Separate" />, and <c>CURLOPT_HTTPHEADER</c>'s,
    /// the origin's own, under <see cref="HeaderOption.Unified" /> (upstream test1527, BL-2645).
    /// </summary>
    /// <param name="http">The option group's HTTP options, for <c>CURLOPT_HTTPHEADER</c>.</param>
    /// <param name="proxy">The option group's proxy options, for <c>CURLOPT_PROXYHEADER</c> and <c>CURLOPT_HEADEROPT</c>.</param>
    /// <returns>The header lines.</returns>
    internal static IReadOnlyList<string> ConnectHeadersOf(EasyHttpOptions http, EasyProxyOptions proxy) =>
        proxy.HeaderOption == HeaderOption.Unified ? http.HttpHeaders : proxy.ProxyHeaders;
}
