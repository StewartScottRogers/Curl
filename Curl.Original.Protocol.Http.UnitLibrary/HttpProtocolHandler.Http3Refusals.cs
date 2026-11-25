using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that refuses HTTP/3 for the requests that cannot use it.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Gives why HTTP/3 is refused for <paramref name="plan" />, in the order curl 8.21.0's
    /// <c>Curl_conn_may_http3</c> checks: over a Unix domain socket, exit 96, for
    /// <c>--http3-only</c> with any URL and <c>--http3</c> with an <c>https://</c> URL (BL-867);
    /// then, exit 3, <c>--http3</c> or <c>--http3-only</c> with an <c>https://</c> URL through a
    /// SOCKS proxy (ADR-0223; an HTTP or HTTPS proxy is tunnelled through, BL-942); or <see langword="null" />
    /// when HTTP/3 is not refused.
    /// </summary>
    private static Http3Refusal? Http3RefusalOf(HttpRequestPlan plan)
    {
        if (plan.Options.Version is not (HttpVersionPreference.Http3 or HttpVersionPreference.Http3Only))
        {
            return null;
        }

        bool https = plan.Context.Url.Scheme == "https";
        return RefusesHttp3OverUnixSocket(plan, https)
            ? new Http3Refusal(HttpTransferMessages.Http3NotOverUnixSocket, CurlExitCode.QuicConnectError)
            : Http3ProxyRefusalOf(plan, https);
    }

    /// <summary>
    /// Decides whether a Unix domain socket refuses HTTP/3 for <paramref name="plan" />:
    /// <c>--http3-only</c> with any URL, <c>--http3</c> only with an <c>https://</c> one.
    /// </summary>
    private static bool RefusesHttp3OverUnixSocket(HttpRequestPlan plan, bool https) =>
        plan.Options.OverUnixSocket && (https || plan.Options.Version == HttpVersionPreference.Http3Only);

    /// <summary>
    /// Gives a SOCKS proxy's refusal of HTTP/3 for an <c>https://</c> URL, exit 3, or
    /// <see langword="null" /> without a proxy, through an HTTP or HTTPS proxy, which QUIC
    /// tunnels through with CONNECT-UDP as curl 8.21.0 does (BL-942), or for any other URL.
    /// </summary>
    private static Http3Refusal? Http3ProxyRefusalOf(HttpRequestPlan plan, bool https) =>
        https && plan.Options.ForwardProxy is { } proxy && !IsHttpProxy(proxy)
            ? new Http3Refusal(HttpTransferMessages.Http3NotOverSocksProxy, CurlExitCode.UrlMalformat)
            : null;

    /// <summary>Decides whether <paramref name="proxy" /> is an HTTP or HTTPS proxy rather than a SOCKS one.</summary>
    private static bool IsHttpProxy(ProxyEndpoint proxy) =>
        proxy.Kind is ProxyKind.Http or ProxyKind.Http10 or ProxyKind.Https;

    /// <summary>
    /// Why HTTP/3 is refused before connecting: the <paramref name="Message" /> curl writes into
    /// its error buffer, and the <paramref name="ExitCode" /> <c>--http3-only</c> fails with.
    /// </summary>
    /// <param name="Message">The refusal's message.</param>
    /// <param name="ExitCode">The exit code <c>--http3-only</c> fails with.</param>
    private readonly record struct Http3Refusal(string Message, CurlExitCode ExitCode);

    /// <summary>
    /// Runs a transfer whose HTTP/3 a SOCKS proxy (measured on curl.se's ngtcp2 build, ADR-0223) or a
    /// Unix domain socket (read from curl 8.21.0, BL-867) rules out: reports the refusal's
    /// message; then fails <c>--http3-only</c> with the refusal's exit code and message before
    /// connecting; and runs <c>--http3</c> over TCP or the Unix socket, where a failure keeps
    /// its own exit code but is reported with the refusal's message, the first message curl
    /// wrote into its error buffer.
    /// </summary>
    private async ValueTask<TransferResult> ExchangeWithoutHttp3Async(HttpRequestPlan plan, Http3Refusal refusal)
    {
        plan.Context.Events.ReportInfo(refusal.Message);
        if (plan.Options.Version == HttpVersionPreference.Http3Only)
        {
            return FailBeforeConnecting(plan, refusal.ExitCode, refusal.Message);
        }

        TransferResult result = await ConnectAndExchangeAsync(plan, earlier: null).ConfigureAwait(false);
        return result.ExitCode == CurlExitCode.Ok ? result : result with { ErrorMessage = refusal.Message };
    }
}
