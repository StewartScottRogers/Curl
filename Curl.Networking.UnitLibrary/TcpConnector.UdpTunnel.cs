using System.Net;
using System.Runtime.ExceptionServices;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// QUIC through an HTTP or HTTPS proxy, as curl 8.22.0 runs it (BL-942): a CONNECT-UDP tunnel
/// (RFC 9298) opened over the proxy connection, QUIC carried inside it as HTTP Datagram
/// capsules (RFC 9297, <see cref="CapsuleDatagramChannel" />).
/// </summary>
public sealed partial class TcpConnector
{
    /// <summary>
    /// Gives the proxy a QUIC connect to <paramref name="target" /> tunnels through with
    /// CONNECT-UDP: its own HTTP or HTTPS proxy, or <see langword="null" /> for none or a SOCKS
    /// one, which the HTTP handler refuses before connecting (ADR-0223).
    /// </summary>
    private static ProxyEndpoint? UdpTunnelProxyOf(ConnectTarget target) =>
        target.Proxy is { Kind: ProxyKind.Http or ProxyKind.Http10 or ProxyKind.Https } proxy ? proxy : null;

    /// <summary>
    /// Opens a QUIC connection to <paramref name="target" /> through <paramref name="proxy" />:
    /// the <c>--connect-to</c> mapping as a direct connect takes it (the target is not
    /// resolved: the proxy reaches it), the proxy resolved and dialled, its TLS handshake for an
    /// HTTPS proxy, the CONNECT-UDP request and the proxy's reply, then the QUIC handshake over
    /// the tunnel. The tunnel's part is bounded by <c>--connect-timeout</c> as a TCP connect's
    /// is, the handshake as a direct QUIC connect's is.
    /// </summary>
    private async ValueTask<MultiplexedConnectResult> ConnectMultiplexedThroughProxyAsync(
        ConnectTarget target,
        ProxyEndpoint proxy,
        QuicDialer dialer,
        CancellationToken cancellationToken)
    {
        // The [SETUP] and [HTTPS-CONNECT] filters go around the tunnel and the QUIC connect as around
        // a direct one, eyeballing to the proxy; the reply head goes to the output the target's
        // events were before they wrapped them (measured, BL-1320 Notes).
        var headOutput = target.Events as IConnectReplyHeadWritingEvents;
        var (traced, httpsConnect, attempt) = QuicConnectionFilters(target, proxy);
        var result = await ConnectMultiplexedThroughProxyTracedAsync(traced, new ProxyRoute(proxy, headOutput, timeProvider.GetTimestamp()), dialer, cancellationToken).ConfigureAwait(false);
        if (result.Connection is null)
        {
            ReportQuicHttpsConnectFailure(httpsConnect, attempt, result.ExitCode);
        }

        return result;
    }

    private async ValueTask<MultiplexedConnectResult> ConnectMultiplexedThroughProxyTracedAsync(
        ConnectTarget target,
        ProxyRoute route,
        QuicDialer dialer,
        CancellationToken cancellationToken)
    {
        var started = route.Started;
        LoadResolveEntriesUnlessLoaded(target.Events);
        var destination = DestinationOf(target);
        if ((_resolveOverrides.ParseError ?? destination.ParseError) is { } parseError)
        {
            return MultiplexedConnectResult.Failed(CurlExitCode.SetoptOptionSyntax, parseError);
        }

        var connectLimit = ConnectTimeoutOrDefault(connectTimeout);
        using var timeout = new CancellationTokenSource(connectLimit, timeProvider);
        using var limited = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        UdpTunnel tunnel;
        try
        {
            var (opened, failure) = await OpenUdpTunnelAsync(target, destination, route, limited.Token).ConfigureAwait(false);
            if (opened is null)
            {
                return failure!;
            }

            tunnel = opened;
        }
        catch (OperationCanceledException) when (timeProvider.GetElapsedTime(started) >= connectLimit)
        {
            var message = $"Connection timed out after {(long)timeProvider.GetElapsedTime(started).TotalMilliseconds} milliseconds";
            target.Events.ReportInfo(message);
            return MultiplexedConnectResult.Failed(CurlExitCode.OperationTimedOut, message);
        }

        var request = new QuicDialRequest(
            target,
            destination.Host,
            destination.Port,
            [tunnel.ProxyEndPoint.Address],
            started,
            tunnel.NameResolved,
            connectTimeout > TimeSpan.Zero ? connectTimeout : null,
            TakeConnectionNumber());
        return await dialer.DialThroughTunnelAsync(request, tunnel.Channel, tunnel.ProxyEndPoint, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves and dials the <paramref name="route" />'s proxy, secures it when it is an HTTPS
    /// proxy, and opens the CONNECT-UDP tunnel to <paramref name="destination" /> through it: the
    /// tunnel, or the failure with curl 8.22.0's exit code and message.
    /// </summary>
    private async ValueTask<(UdpTunnel? Tunnel, MultiplexedConnectResult? Failure)> OpenUdpTunnelAsync(
        ConnectTarget target,
        ConnectDestination destination,
        ProxyRoute route,
        CancellationToken cancellationToken)
    {
        var (proxy, _, started) = route;
        var (addresses, resolveFailure) = await ResolveWithFailureReasonAsync(proxy.Host, proxy.Port, target, cancellationToken).ConfigureAwait(false);
        if (addresses.Count == 0)
        {
            var (exitCode, message) = NameResolutionFailure.Describe(CurlExitCode.CouldntResolveProxy, "proxy", proxy.Host, proxy.Port, resolveFailure);
            return (null, MultiplexedConnectResult.Failed(exitCode, message));
        }

        var nameResolved = timeProvider.GetTimestamp();
        var (dialed, lastDialError, lastBindFailure) = await DialFirstReachableAsync(addresses, proxy.Port, proxy.Host, target, cancellationToken).ConfigureAwait(false);
        if (dialed is null)
        {
            var elapsedMilliseconds = (long)timeProvider.GetElapsedTime(nameResolved).TotalMilliseconds;
            var failed = DialFailure(
                target.Events,
                lastDialError,
                lastBindFailure,
                new ConnectTimings(started, nameResolved, null, null),
                $"Failed to connect to {target.Host}:{target.Port} over proxy {proxy.Host} after {elapsedMilliseconds} ms: {DialFailureText(lastBindFailure)}");
            return (null, MultiplexedConnectResult.Failed(failed.ExitCode, failed.ErrorMessage!));
        }

        var (connection, tlsFailure) = await SecureUdpTunnelProxyAsync(dialed, target, proxy, cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return (null, tlsFailure);
        }

        var replyFailure = await RequestUdpTunnelAsync(connection, target, destination, route, cancellationToken).ConfigureAwait(false);
        return replyFailure is null
            ? (new UdpTunnel(new CapsuleDatagramChannel(connection, dialed.RemoteEndPoint!), dialed.RemoteEndPoint!, nameResolved), null)
            : (null, replyFailure);
    }

    /// <summary>
    /// The connection the CONNECT-UDP request goes on: the dialled one to an HTTP proxy, the
    /// proxy's TLS stream to an HTTPS proxy, its handshake run as the CONNECT path runs it and
    /// followed by curl 8.22.0's <c>CONNECT-UDP:</c> ALPN line (measured, BL-942), which an HTTP
    /// proxy's connection writes too. Under <see cref="TracesSetupFilter" /> the setup filter adds
    /// the tunnel filter first, after the HTTPS proxy's SSL filter, as for a CONNECT tunnel
    /// (measured through an HTTP proxy, BL-1320 Notes).
    /// </summary>
    private async ValueTask<(IConnection? Connection, MultiplexedConnectResult? Failure)> SecureUdpTunnelProxyAsync(
        DialedSocket dialed,
        ConnectTarget target,
        ProxyEndpoint proxy,
        CancellationToken cancellationToken)
    {
        if (proxy.Kind != ProxyKind.Https)
        {
            ReportSetupFilterAdded(target.Events, HttpProxyTunnelFilterAddedLine);
            target.Events.ReportInfo("CONNECT-UDP: no ALPN negotiated");
            return (dialed.Connection, null);
        }

        ReportSetupFilterAdded(target.Events, HttpsProxySslFilterAddedLine);
        ReportSetupFilterAdded(target.Events, HttpProxyTunnelFilterAddedLine);
        var secured = await AuthenticateAsync(_proxyTlsProvider, dialed.Connection, proxy.Host, target, isProxy: true, applicationProtocols: HttpApplicationProtocols.Http11Only, cancellationToken).ConfigureAwait(false);
        if (secured.Connection is null)
        {
            return (null, MultiplexedConnectResult.Failed(secured.ExitCode, secured.ErrorMessage!));
        }

        target.Events.ReportInfo(secured.ApplicationProtocol is { } agreed
            ? $"CONNECT-UDP: '{agreed}' negotiated"
            : "CONNECT-UDP: no ALPN negotiated");
        return (secured.Connection, null);
    }

    private void ReportSetupFilterAdded(ITransferEvents events, string addedLine)
    {
        if (TracesSetupFilter)
        {
            events.ReportInfo(addedLine);
        }
    }

    /// <summary>
    /// Sends the CONNECT-UDP request with the pre-emptive <c>Proxy-Authorization</c> and reads
    /// the proxy's reply, whose head goes to the header output as a CONNECT reply's does:
    /// <see langword="null" /> once a <c>101</c> or <c>2xx</c> opened the tunnel, reported as
    /// <c>CONNECT-UDP phase completed for HTTP proxy</c> and <c>CONNECT-UDP tunnel established,
    /// response &lt;n&gt;</c>; else the connection is disposed and the failure returned - exit 56
    /// for a reply curl gives up on, exit 7 <c>CONNECT-UDP tunnel failed, response &lt;n&gt;</c>
    /// for any other status, also reported as <c>-v</c> repeats it (measured, BL-942).
    /// </summary>
    private async ValueTask<MultiplexedConnectResult?> RequestUdpTunnelAsync(
        IConnection connection,
        ConnectTarget target,
        ConnectDestination destination,
        ProxyRoute route,
        CancellationToken cancellationToken)
    {
        var (reply, exception) = await ExchangeUdpTunnelRequestAsync(connection, target, destination, route, cancellationToken).ConfigureAwait(false);
        if (exception is null && reply.OpensUdpTunnel)
        {
            target.Events.ReportInfo("CONNECT-UDP phase completed for HTTP proxy");
            target.Events.ReportInfo($"CONNECT-UDP tunnel established, response {reply.StatusCode}");
            return null;
        }

        await connection.DisposeAsync().ConfigureAwait(false);
        exception?.Throw();
        var (exitCode, message) = reply.RecvErrorMessage is { } recvErrorMessage
            ? (CurlExitCode.RecvError, recvErrorMessage)
            : (CurlExitCode.CouldntConnect, $"CONNECT-UDP tunnel failed, response {reply.StatusCode}");
        target.Events.ReportInfo(message);
        return MultiplexedConnectResult.Failed(exitCode, message);
    }

    /// <summary>
    /// Writes the CONNECT-UDP request and reads the reply, its head written to the header
    /// output the route carries: the reply, or what was thrown along the way. <c>-v</c> shows
    /// <c>Establishing HTTP proxy UDP tunnel to &lt;host&gt;:&lt;port&gt;</c>, the request head and
    /// the reply's header lines, as curl 8.22.0 writes them (measured, BL-1320 Notes).
    /// </summary>
    private async ValueTask<(HttpProxyTunnelReply Reply, ExceptionDispatchInfo? Exception)> ExchangeUdpTunnelRequestAsync(
        IConnection connection,
        ConnectTarget target,
        ConnectDestination destination,
        ProxyRoute route,
        CancellationToken cancellationToken)
    {
        try
        {
            var (proxy, headOutput, _) = route;
            var events = target.Events;
            var proxyAuthorization = await CreateProxyAuthorizationAsync(destination, proxy, [], NoTransferEvents.Instance, cancellationToken).ConfigureAwait(false);
            events.ReportInfo($"Establishing HTTP proxy UDP tunnel to {HttpProxyTunnel.FormatAuthority(destination.Host, destination.Port)}");
            var request = HttpProxyTunnel.BuildConnectUdpRequest(destination.Host, destination.Port, proxy, _proxyTunnelOptions, proxyAuthorization);
            events.ReportRequestHeader(request);
            await connection.WriteAsync(request, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            var reply = await HttpProxyTunnel.ReadReplyAsync(connection, cancellationToken).ConfigureAwait(false);
            EndProxyAuthorization(proxyAuthorization);
            ConnectTunnelVerboseLines.ReportReplyHead(events, reply.Head.Span, reply.StatusCode, proxyAuthorization);
            if (headOutput is not null && !reply.Head.IsEmpty)
            {
                await headOutput.WriteConnectReplyHeadAsync(reply.Head, cancellationToken).ConfigureAwait(false);
            }

            return (reply, null);
        }
        catch (Exception exception)
        {
            return (default, ExceptionDispatchInfo.Capture(exception));
        }
    }

    /// <summary>
    /// An open CONNECT-UDP tunnel: the datagram channel it carries, the proxy's address, and
    /// when the proxy's name was resolved.
    /// </summary>
    private sealed record UdpTunnel(CapsuleDatagramChannel Channel, IPEndPoint ProxyEndPoint, long NameResolved);
}
