using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    /// <summary>The name an origin's SSL filter writes its lines under.</summary>
    internal const string SslFilterName = "SSL";

    /// <summary>The name an HTTPS proxy's SSL filter writes its lines under.</summary>
    internal const string SslProxyFilterName = "SSL-PROXY";

    /// <summary>
    /// The line curl 8.21.0's setup filter writes, under <see cref="TracesSetupFilter" />, as it adds
    /// the tunnel filter once a plain HTTP proxy's socket connected, before <c>CONNECT: no ALPN
    /// negotiated</c> (measured, BL-1193 Notes).
    /// </summary>
    public const string HttpProxyTunnelFilterAddedLine = "[SETUP] added HTTP proxy tunnel filter";

    /// <summary>
    /// The line curl 8.21.0's setup filter writes, under <see cref="TracesSetupFilter" />, as it adds
    /// an HTTPS proxy's SSL filter once the proxy's socket connected, before
    /// <see cref="HttpProxyTunnelFilterAddedLine" /> and the proxy's handshake (measured, BL-1255 Notes).
    /// </summary>
    public const string HttpsProxySslFilterAddedLine = "[SETUP] added SSL filter for HTTP proxy";

    /// <summary>
    /// The line curl 8.21.0's setup filter writes, under <see cref="TracesSetupFilter" />, as it adds
    /// the SSL filter for a direct <c>https://</c> origin once the socket connected (BL-1192 Notes).
    /// </summary>
    public const string SslFilterAddedLine = "[SETUP] added SSL filter for origin";

    /// <summary>
    /// The line curl 8.21.0's setup filter writes, under <see cref="TracesSetupFilter" />, as it adds
    /// the PROXY protocol filter once the socket connected, before <c>Established connection</c>.
    /// </summary>
    public const string HaproxyFilterAddedLine = "[SETUP] added HAPROXY filter";

    /// <summary>
    /// The line curl 8.21.0's TCP filter writes, under <see cref="TracesTcpFilter" />, as a plain HTTP
    /// connection is asked its ALPN protocol, after the setup filters' removal (BL-1195 Notes).
    /// </summary>
    public const string QueryAlpnLine = "[TCP] query ALPN";

    /// <summary>
    /// The line curl 8.21.0's SOCKS filter writes, under <see cref="TracesSocksFilter" />, as a new
    /// plain HTTP connection through a SOCKS proxy or pre-proxy is asked its ALPN protocol, after
    /// <c>Established connection</c> and before <c>using HTTP/1.x</c> (BL-1246 Notes).
    /// </summary>
    public const string SocksQueryAlpnLine = "[SOCKS] query ALPN";

    // Under TracesSetupFilter, TracesDnsFilter, TracesHappyEyeballsFilter, TracesTcpFilter and
    // TracesTimers a direct connect reports through SetupFilterTraceEvents over
    // DnsFilterTraceEvents over ConnectAttemptTraceEvents, which write curl's [SETUP], [DNS],
    // [HAPPY-EYEBALLS], [TCP] and [TIMER] lines in curl's order around its own (BL-1102, BL-1103,
    // BL-1161, BL-1186), the last also given back for the
    // race to tell; otherwise the target is as given. Under TracesHttpsConnectFilter an https://
    // origin's [HTTPS-CONNECT] events sit between the [SETUP] and [DNS] ones, given back for a failed
    // connect to report on (BL-1192).
    private (ConnectTarget Target, ConnectAttemptTraceEvents? Trace, HttpsConnectFilterTraceEvents? HttpsConnect, FirstHttpsConnectAttempt? Attempt) TracingConnectionFilters(ConnectTarget target, ConnectDestination destination)
    {
        var trace = ConnectAttemptTraceOf(target.Events, destination.Host);
        var httpsOrigin = IsHttpsOrigin(target);
        if (!TracesAnyFilterAbove(trace, httpsOrigin))
        {
            return (target, null, null, null);
        }

        trace?.ConnectStarting();
        var (events, httpsConnect) = SetupAndDnsFilterEvents(
            trace ?? target.Events,
            httpsOrigin,
            TracesSetupFilter,
            below => DnsFilterTraceEvents.Start(below, destination.Host, destination.Port, addressFamily),
            below => new SetupFilterTraceEvents(below, destination.Host, destination.Port),
            firstAttempt: target.TcpFirstAttemptVersion is null ? TakeFirstHttpsConnectAttempt(target) : null,
            tcpFirstAttemptVersion: target.TcpFirstAttemptVersion);
        return (target with { Events = events }, trace, httpsConnect, TcpFirstAttemptOf(target, httpsConnect));
    }

    private bool TracesAnyFilterAbove(ConnectAttemptTraceEvents? trace, bool httpsOrigin) =>
        TracesSetupFilter || TracesDnsFilter || trace is not null || TracesHttpsConnectFor(httpsOrigin);

    private bool TracesHttpsConnectFor(bool httpsOrigin) => TracesHttpsConnectFilter && httpsOrigin;

    private bool WritesSslFilterAddedFor(ConnectTarget target) => TracesSetupFilter && IsHttpsOrigin(target);

    // An https:// origin, not a forward proxy: curl 8.21.0 connects it through its ALPN connect
    // filter, which adds the setup filter itself, so no "[SETUP] added" line is written for it, and
    // the setup filter reports adding the SSL filter once the socket connected (measured, BL-1192 Notes).
    private static bool IsHttpsOrigin(ConnectTarget target) =>
        target.UseTls && !target.IsForwardProxy && string.Equals(target.PoolScheme, "https", StringComparison.OrdinalIgnoreCase);

    private ConnectAttemptTraceEvents? ConnectAttemptTraceOf(ITransferEvents events, string host) =>
        TracesHappyEyeballsFilter || TracesTcpFilter || TracesTimers
            ? new ConnectAttemptTraceEvents(events, host, TracesHappyEyeballsFilter, TracesTcpFilter, TracesTimers, TracesTimerExpiry, TracedConnectTimeout, TracedTransferTimeout)
            : null;

    // The [SETUP] filter's events over the [DNS] filter's over the given ones, each when traced; the
    // setup filter's first line (the ALPN connect filter's for an https:// origin) is written before
    // the DNS filter's, and the ALPN connect filter's events sit between the two. A direct connect, a
    // proxy's and a Unix socket's each start their own DNS and setup filters (measured, BL-1254 Notes).
    // A QUIC connect names the attempt after it; the TCP attempt after a QUIC one adds no filter but
    // goes on from where the QUIC attempt's stood (BL-1284), and the QUIC attempt after a TCP one an
    // --alt-svc entry put first from where the TCP attempt's stood (BL-1360).
    private (ITransferEvents Events, HttpsConnectFilterTraceEvents? HttpsConnect) SetupAndDnsFilterEvents(
        ITransferEvents events,
        bool httpsOrigin,
        bool tracesSetup,
        Func<ITransferEvents, ITransferEvents> startDnsFilter,
        Func<ITransferEvents, ITransferEvents> startSetupFilter,
        string? secondAttemptVersion = null,
        FirstHttpsConnectAttempt? firstAttempt = null,
        string? tcpFirstAttemptVersion = null)
    {
        var tracesHttpsConnect = TracesHttpsConnectFor(httpsOrigin);
        if (firstAttempt is null)
        {
            WriteFirstFilterAddedLine(events, httpsOrigin, tracesHttpsConnect, tracesSetup);
        }

        events = TracesDnsFilter ? startDnsFilter(events) : events;
        var httpsConnect = tracesHttpsConnect ? HttpsConnectFilterOver(events, tracesSetup, secondAttemptVersion, firstAttempt, tcpFirstAttemptVersion) : null;
        events = httpsConnect ?? events;
        return (tracesSetup ? startSetupFilter(events) : events, httpsConnect);
    }

    private static void WriteFirstFilterAddedLine(ITransferEvents events, bool httpsOrigin, bool tracesHttpsConnect, bool tracesSetup)
    {
        if (tracesHttpsConnect)
        {
            events.ReportInfo(HttpsConnectFilterTraceEvents.AddedLine);
        }
        else if (tracesSetup && !httpsOrigin)
        {
            events.ReportInfo(SetupFilterTraceEvents.AddedLine);
        }
    }

    // The HTTP handler asks the topmost filter, the origin's SSL filter, which protocol ALPN
    // agreed, before using HTTP/1.x (measured, BL-1287 Notes).
    private void WriteSslQueryAlpnLines(ConnectTarget target, string? applicationProtocol)
    {
        if (target.PoolScheme == "https" && !target.IsForwardProxy)
        {
            SslTraceFor(target).ReportAlpnQueried(target.Events, applicationProtocol);
        }
    }

    // A direct https connection dialled under TracesTcpFilter writes curl 8.21.0's [TCP] send and recv
    // lines for the TLS records below the TLS filter: the handshake's with Schannel's 4096-byte reads,
    // then the application data's with its 103424-byte reads, and no [TCP] query ALPN line, as TLS
    // answers that (measured, BL-1253 Notes; ADR-0357's BL-1260 amendment). Through a proxy or a
    // tunnel the TCP filter sits below the proxy's, so its records are not written.
    internal static TcpIoTraceConnection? TlsRecordTraceFor(IConnection dialed, bool tracesTcpFilter, ConnectTarget target) =>
        tracesTcpFilter && target.Proxy is null && !target.IsForwardProxy && target.PoolScheme == "https"
            ? new TcpIoTraceConnection(dialed, target.Events, TcpIoTraceConnection.HttpsHandshakeLines)
            : null;

    // The topmost filter answers the ALPN query: through a plain HTTP proxy's tunnel the [H1-PROXY]
    // filter, through a SOCKS proxy or pre-proxy the [SOCKS] filter, never [TCP], so under network
    // alone a SOCKS connection writes none (measured, BL-1193 and BL-1246 Notes).
    private void WriteQueryAlpnLine(DialedSocket dialed, ConnectTarget target, bool tracesIo)
    {
        if (dialed.TunnelTrace is { } tunnelTrace)
        {
            tunnelTrace.ReportAlpnQueried(target.Events);
        }
        else if (IsThroughSocks(target))
        {
            WriteSocksQueryAlpnLine(target);
        }
        else if (tracesIo)
        {
            target.Events.ReportInfo(QueryAlpnLine);
        }
    }

    // Only a new plain HTTP connection is asked; a reused one writes no second line (measured,
    // BL-1246 Notes).
    private void WriteSocksQueryAlpnLine(ConnectTarget target)
    {
        if (TracesSocksFilter && target.PoolScheme == "http")
        {
            target.Events.ReportInfo(SocksQueryAlpnLine);
        }
    }

    // A forward proxy is the target itself, so its handshake is the proxy's SSL filter's.
    private SslFilterTrace SslTraceFor(ConnectTarget target) =>
        target.IsForwardProxy
            ? new SslFilterTrace(SslProxyFilterName, TracesSslProxyFilter)
            : new SslFilterTrace(SslFilterName, TracesSslFilter);
}
