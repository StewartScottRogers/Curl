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
        var started = timeProvider.GetTimestamp();
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
            var (opened, failure) = await OpenUdpTunnelAsync(target, destination, proxy, started, limited.Token).ConfigureAwait(false);
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
    /// Resolves and dials <paramref name="proxy" />, secures it when it is an HTTPS proxy, and
    /// opens the CONNECT-UDP tunnel to <paramref name="destination" /> through it: the tunnel,
    /// or the failure with curl 8.22.0's exit code and message.
    /// </summary>
    private async ValueTask<(UdpTunnel? Tunnel, MultiplexedConnectResult? Failure)> OpenUdpTunnelAsync(
        ConnectTarget target,
        ConnectDestination destination,
        ProxyEndpoint proxy,
        long started,
        CancellationToken cancellationToken)
    {
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

        var replyFailure = await RequestUdpTunnelAsync(connection, target, destination, proxy, cancellationToken).ConfigureAwait(false);
        return replyFailure is null
            ? (new UdpTunnel(new CapsuleDatagramChannel(connection, dialed.RemoteEndPoint!), dialed.RemoteEndPoint!, nameResolved), null)
            : (null, replyFailure);
    }

    /// <summary>
    /// The connection the CONNECT-UDP request goes on: the dialled one to an HTTP proxy, the
    /// proxy's TLS stream to an HTTPS proxy, its handshake run as the CONNECT path runs it and
    /// followed by curl 8.22.0's <c>CONNECT-UDP:</c> ALPN line (measured, BL-942).
    /// </summary>
    private async ValueTask<(IConnection? Connection, MultiplexedConnectResult? Failure)> SecureUdpTunnelProxyAsync(
        DialedSocket dialed,
        ConnectTarget target,
        ProxyEndpoint proxy,
        CancellationToken cancellationToken)
    {
        if (proxy.Kind != ProxyKind.Https)
        {
            return (dialed.Connection, null);
        }

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

    /// <summary>
    /// Sends the CONNECT-UDP request with the pre-emptive <c>Proxy-Authorization</c> and reads
    /// the proxy's reply, whose head goes to the header output as a CONNECT reply's does:
    /// <see langword="null" /> once a <c>101</c> or <c>2xx</c> opened the tunnel, reported as
    /// <c>CONNECT-UDP tunnel established, response &lt;n&gt;</c>; else the connection is
    /// disposed and the failure returned - exit 56 for a reply curl gives up on, exit 7
    /// <c>CONNECT-UDP tunnel failed, response &lt;n&gt;</c> for any other status, also reported
    /// as <c>-v</c> repeats it (measured, BL-942).
    /// </summary>
    private async ValueTask<MultiplexedConnectResult?> RequestUdpTunnelAsync(
        IConnection connection,
        ConnectTarget target,
        ConnectDestination destination,
        ProxyEndpoint proxy,
        CancellationToken cancellationToken)
    {
        var (reply, exception) = await ExchangeUdpTunnelRequestAsync(connection, target, destination, proxy, cancellationToken).ConfigureAwait(false);
        if (exception is null && reply.OpensUdpTunnel)
        {
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
    /// output when the events take it: the reply, or what was thrown along the way.
    /// </summary>
    private async ValueTask<(HttpProxyTunnelReply Reply, ExceptionDispatchInfo? Exception)> ExchangeUdpTunnelRequestAsync(
        IConnection connection,
        ConnectTarget target,
        ConnectDestination destination,
        ProxyEndpoint proxy,
        CancellationToken cancellationToken)
    {
        try
        {
            var proxyAuthorization = await CreateProxyAuthorizationAsync(destination, proxy, [], cancellationToken).ConfigureAwait(false);
            await connection.WriteAsync(HttpProxyTunnel.BuildConnectUdpRequest(destination.Host, destination.Port, proxy, _proxyTunnelOptions, proxyAuthorization), cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            var reply = await HttpProxyTunnel.ReadReplyAsync(connection, cancellationToken).ConfigureAwait(false);
            EndProxyAuthorization(proxyAuthorization);
            if (target.Events is IConnectReplyHeadWritingEvents headOutput && !reply.Head.IsEmpty)
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
