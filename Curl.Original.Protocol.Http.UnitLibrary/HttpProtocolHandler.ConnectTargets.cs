using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that names what to connect to and the forward proxy to go through.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Builds the connect target for <paramref name="url" />: its host without IPv6
    /// brackets, its port, and TLS for <c>https</c>.
    /// </summary>
    private static ConnectTarget TargetOf(CurlUrl url) =>
        new(url.IdnHost, url.Port, url.Scheme == "https");

    /// <summary>
    /// Builds the connect target for <paramref name="plan" />: the forward proxy itself, marked
    /// <see cref="ConnectTarget.IsForwardProxy" />, with TLS for an HTTPS proxy; or else the URL's host, tunnelled through the transfer's proxy
    /// when it has one. Either is pooled under <c>https</c> for an <c>https</c> URL and
    /// <c>http</c> otherwise, so a forward proxy connection serves every origin behind it, and
    /// carries the transfer's events for the connector to report through (ADR-0050) and its
    /// diagnostic log for the connector's own lines (ADR-0222).
    /// </summary>
    private static ConnectTarget TargetOf(HttpRequestPlan plan)
    {
        ConnectTarget urlTarget = TargetOf(plan.Context.Url);
        ConnectTarget target = plan.ForwardProxy is { } proxy
            ? new ConnectTarget(proxy.Host, proxy.Port, proxy.Kind == ProxyKind.Https) { IsForwardProxy = true, ForwardProxy = proxy }
            : urlTarget with
            {
                Proxy = plan.Options.ForwardProxy,
                AltSvcRoute = plan.Options.AltSvcRoute,
                ApplicationProtocols = plan.DowngradedToHttp11 ? Http11ApplicationProtocols : AltSvcApplicationProtocols.Of(plan.Options.AltSvcRoute),
            };
        return target with { PoolScheme = urlTarget.UseTls ? "https" : "http", Events = plan.Context.Events, DiagnosticLog = plan.Context.DiagnosticLog };
    }

    /// <summary>
    /// Gives the proxy the request is forwarded through in absolute form: an HTTP-kind proxy,
    /// for an <c>http</c> or <c>ftp</c> URL, without <c>-p</c>. Every other proxy is tunnelled through by
    /// the connector, and <see langword="null" /> is returned for it.
    /// </summary>
    private static ProxyEndpoint? ForwardProxyOf(CurlUrl url, HttpRequestOptions options) =>
        options.ForwardProxy is { Kind: ProxyKind.Http or ProxyKind.Http10 or ProxyKind.Https } proxy
            && !options.ProxyTunnel
            && !TargetOf(url).UseTls
            ? proxy
            : null;
}
