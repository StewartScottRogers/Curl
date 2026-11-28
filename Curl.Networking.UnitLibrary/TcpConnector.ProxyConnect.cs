using System.Net;
using System.Runtime.CompilerServices;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    private async ValueTask<ConnectResult> ConnectThroughProxyAsync(
        ConnectTarget target,
        ConnectDestination destination,
        ProxyEndpoint proxy,
        long started,
        CancellationToken cancellationToken)
    {
        var firstHop = FirstHopTo(proxy);
        var headOutput = target.Events as IConnectReplyHeadWritingEvents;
        (target, var httpsConnect) = TracingTunnelFilters(target, proxy, firstHop);
        var result = await ConnectThroughProxyTracedAsync(target, destination, proxy, new ProxyRoute(firstHop, headOutput, started), cancellationToken).ConfigureAwait(false);
        ReportHttpsConnectFailure(httpsConnect, result);
        return result;
    }

    /// <summary>
    /// Gives a failure that came after the TCP connect, such as a refused CONNECT, a failed SOCKS
    /// handshake or a refused TLS handshake, the timings the connector took before it, so
    /// <c>%{time_namelookup}</c> and <c>%{time_connect}</c> report them as curl does (STD-145);
    /// a success, and a failure that already carries timings, as they are.
    /// </summary>
    private static ConnectResult WithTimingsWhenFailedWithout(ConnectResult result, ConnectTimings timings) =>
        result.Connection is not null || result.Timings is not null
            ? result
            : ConnectResult.Failed(result.ExitCode, result.ErrorMessage!, timings, result.ConnectionNumber, result.ProxyConnectResponseCode, result.LastSocketError, result.ProxyError, result.ConnectReplyHeadersStored, result.ProxyConnectHeaderBytes).WithProxyAuthenticationProblem(result.IsProxyAuthenticationProblem);

    /// <summary>
    /// Awaits <paramref name="dial" />; a cancellation, such as the connect limit passing after
    /// the proxy resolved, leaves as a <see cref="CanceledAfterResolveException" /> carrying
    /// <paramref name="timings" />, so the timeout's failure reports <c>%{time_namelookup}</c> (STD-145).
    /// </summary>
    private static async ValueTask<(ConnectResult? Result, string? RedialAuthorization)> CarryingTimingsIfCanceledAsync(
        ValueTask<(ConnectResult? Result, string? RedialAuthorization)> dial,
        ConnectTimings timings)
    {
        try
        {
            return await dial.ConfigureAwait(false);
        }
        catch (OperationCanceledException canceled)
        {
            var carrying = new CanceledAfterResolveException(timings, canceled);
            carrying.Data[ConnectTimings.CarriedOnCancellationKey] = timings;
            throw carrying;
        }
    }

    /// <summary>
    /// A cancellation that came after the name resolved, carrying the timings taken until then.
    /// </summary>
    private sealed class CanceledAfterResolveException(ConnectTimings timings, OperationCanceledException canceled)
        : OperationCanceledException(canceled.Message, canceled, canceled.CancellationToken)
    {
        public ConnectTimings Timings { get; } = timings;
    }

    private readonly record struct ProxyRoute(ProxyEndpoint FirstHop, IConnectReplyHeadWritingEvents? HeadOutput, long Started);

    private async ValueTask<ConnectResult> ConnectThroughProxyTracedAsync(
        ConnectTarget target,
        ConnectDestination destination,
        ProxyEndpoint proxy,
        ProxyRoute route,
        CancellationToken cancellationToken)
    {
        var (firstHop, headOutput, started) = route;
        IReadOnlyList<IPAddress> addresses = [];
        if (firstHop.UnixSocketPath is null)
        {
            (addresses, var failure) = await ResolveWithFailureReasonAsync(firstHop.Host, firstHop.Port, target, cancellationToken).ConfigureAwait(false);
            if (addresses.Count == 0)
            {
                var (exitCode, message) = NameResolutionFailure.Describe(CurlExitCode.CouldntResolveProxy, "proxy", firstHop.Host, firstHop.Port, failure);
                return ConnectResult.Failed(exitCode, message);
            }
        }

        var nameResolved = timeProvider.GetTimestamp();
        var authenticatorEvents = new SspiFailureRecordingTransferEvents(target.Events);
        var proxyAuthorization = await CreateProxyAuthorizationAsync(destination, proxy, [], authenticatorEvents, cancellationToken).ConfigureAwait(false);
        var answersChallenge = false;
        var reconnects = 0;
        var replyHeadBytes = new StrongBox<long>();
        var lastStatusReply = new StrongBox<(int StatusCode, int HeaderCount)>();
        while (true)
        {
            // A 407 answered on a connection the proxy closes is sent again on a new one, as
            // curl 8.21.0 connects again ("Connect me again please", BL-602 Notes).
            var (result, redialAuthorization) = await CarryingTimingsIfCanceledAsync(
                DialAndOpenThroughProxyAsync(
                    addresses,
                    new TunnelRequest(target, destination, proxy, started, nameResolved, answersChallenge, authenticatorEvents) { HeadOutput = headOutput, ReplyHeadBytes = replyHeadBytes, LastStatusReply = lastStatusReply },
                    proxyAuthorization,
                    cancellationToken),
                new ConnectTimings(started, nameResolved, null, null)).ConfigureAwait(false);
            if (result is not null)
            {
                return WithTimingsWhenFailedWithout(result, new ConnectTimings(started, nameResolved, timeProvider.GetTimestamp(), null));
            }

            target.Events.ReportInfo(ConnectTunnelVerboseLines.ConnectAgain);
            if (++reconnects > MaxProxyReconnects)
            {
                return ConnectResult.Failed(CurlExitCode.CouldntConnect, "Could not connect to server");
            }

            proxyAuthorization = redialAuthorization;
            answersChallenge = true;
        }
    }

    // curl 8.21.0's DNS filter resolves the proxy it dials first (measured, BL-1181 Notes). Through
    // a plain HTTP proxy its setup filter writes [SETUP] added (not for an https:// origin, whose
    // ALPN connect filter adds it) before the DNS filter's lines, and happy eyeballing to proxy
    // before the first Trying (measured, BL-1193 Notes), through an HTTPS proxy too (BL-1255 Notes). Through a SOCKS proxy it eyeballs to origin,
    // the SOCKS proxy it dials (measured, BL-1254 Notes). An https:// origin's ALPN connect filter
    // goes around them as for a direct connect, its added line first (measured, BL-1254 Notes).
    private (ConnectTarget Target, HttpsConnectFilterTraceEvents? HttpsConnect) TracingTunnelFilters(ConnectTarget target, ProxyEndpoint proxy, ProxyEndpoint firstHop)
    {
        var peer = IsSocks(proxy) ? SetupFilterTraceEvents.ToOrigin : SetupFilterTraceEvents.ToProxy;
        var (events, httpsConnect) = SetupAndDnsFilterEvents(
            target.Events,
            IsHttpsOrigin(target),
            TracesSetupFilter,
            below => DnsFilterTraceEvents.Start(below, firstHop.Host, firstHop.Port, addressFamily),
            below => new SetupFilterTraceEvents(below, firstHop.Host, firstHop.Port, peer),
            firstAttempt: TakeFirstHttpsConnectAttempt(target));
        return (target with { Events = events }, httpsConnect);
    }

    private static bool IsSocks(ProxyEndpoint proxy) =>
        proxy.Kind is ProxyKind.Socks4 or ProxyKind.Socks4a or ProxyKind.Socks5 or ProxyKind.Socks5Hostname;

    /// <summary>
    /// The proxy a connect to <paramref name="target" /> tunnels through: its own
    /// <see cref="ConnectTarget.Proxy" />, else the <see cref="PreProxy" /> for a forward proxy,
    /// which is opened as a SOCKS tunnel to the forward proxy (BL-614), else none.
    /// </summary>
    private ProxyEndpoint? TunnelProxyOf(ConnectTarget target) =>
        target.Proxy ?? (target.IsForwardProxy ? PreProxy : null);

    /// <summary>
    /// The proxy <paramref name="proxy" /> is reached through: the <see cref="PreProxy" /> for an
    /// HTTP or HTTPS proxy when there is one, as curl 8.21.0 opens the SOCKS connection first and
    /// speaks to the HTTP proxy inside it (measured, BL-614); else the proxy itself.
    /// </summary>
    private ProxyEndpoint FirstHopTo(ProxyEndpoint proxy) =>
        PreProxy is { } socks && proxy.Kind is ProxyKind.Http or ProxyKind.Http10 or ProxyKind.Https ? socks : proxy;
}
