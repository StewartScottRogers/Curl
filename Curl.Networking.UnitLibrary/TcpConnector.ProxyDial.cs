using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    /// <summary>
    /// Dials the proxy and opens the tunnel through it, sending
    /// <paramref name="proxyAuthorization" /> on the first CONNECT: the connect's result, or
    /// <see langword="null" /> with the <c>Proxy-Authorization</c> to send on a new connection
    /// when the proxy challenged and closed this one.
    /// </summary>
    private async ValueTask<(ConnectResult? Result, string? RedialAuthorization)> DialAndOpenThroughProxyAsync(
        IReadOnlyList<IPAddress> addresses,
        TunnelRequest tunnel,
        string? proxyAuthorization,
        CancellationToken cancellationToken)
    {
        var (target, destination, proxy, started, nameResolved, _, _) = tunnel;
        var firstHop = FirstHopTo(proxy);
        var (dialed, lastDialError, lastBindFailure) = firstHop.UnixSocketPath is { } socketPath
            ? await DialProxyUnixSocketAsync(socketPath, firstHop.Host, target, cancellationToken).ConfigureAwait(false)
            : await DialFirstReachableAsync(addresses, firstHop.Port, firstHop.Host, target, cancellationToken).ConfigureAwait(false);
        if (dialed is null)
        {
            return (ProxyDialFailure(tunnel, firstHop, lastDialError, lastBindFailure), null);
        }

        if (!ReferenceEquals(firstHop, proxy)
            && await OpenSocksHopAsync(dialed, target, new ConnectDestination(proxy.Host, proxy.Port, IsMapped: false, ParseError: null), firstHop, cancellationToken).ConfigureAwait(false) is { } preProxyFailure)
        {
            return (preProxyFailure, null);
        }

        return await OpenThroughProxyKindAsync(dialed, tunnel, proxyAuthorization, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The failure of a dial that reached no address of the first hop. Through a pre-proxy curl
    /// 8.21.0 names the HTTP proxy as what it failed to reach (measured, BL-614).
    /// </summary>
    private ConnectResult ProxyDialFailure(TunnelRequest tunnel, ProxyEndpoint firstHop, SocketError lastDialError, LocalBindFailure? lastBindFailure)
    {
        var (target, _, proxy, started, nameResolved, _, _) = tunnel;
        var reached = ReferenceEquals(firstHop, proxy) ? $"{target.Host}:{target.Port}" : $"{proxy.Host}:{proxy.Port}";
        var elapsedMilliseconds = (long)timeProvider.GetElapsedTime(nameResolved).TotalMilliseconds;
        return DialFailure(
            target.Events,
            lastDialError,
            lastBindFailure,
            new ConnectTimings(started, nameResolved, null, null),
            $"Failed to connect to {reached} over proxy {firstHop.Host} after {elapsedMilliseconds} ms: {DialFailureText(lastBindFailure)}");
    }

    /// <summary>
    /// Opens the tunnel over the socket dialled to the proxy the way its kind does: CONNECT in the
    /// clear or over TLS to an HTTP or HTTPS proxy, a SOCKS handshake to any other.
    /// </summary>
    private async ValueTask<(ConnectResult? Result, string? RedialAuthorization)> OpenThroughProxyKindAsync(
        DialedSocket dialed,
        TunnelRequest tunnel,
        string? proxyAuthorization,
        CancellationToken cancellationToken)
    {
        var (target, destination, proxy, started, nameResolved, _, _) = tunnel;
        if (proxy.Kind is ProxyKind.Https)
        {
            return await OpenTunnelOverTlsAsync(dialed, tunnel, proxyAuthorization, cancellationToken).ConfigureAwait(false);
        }

        if (proxy.Kind is ProxyKind.Http or ProxyKind.Http10)
        {
            return await OpenPlainTunnelAsync(dialed, tunnel, proxyAuthorization, cancellationToken).ConfigureAwait(false);
        }

        return (await OpenSocksTunnelAsync(dialed, target, destination, proxy, new ConnectTimings(started, nameResolved, 0, null), cancellationToken).ConfigureAwait(false), null);
    }

    /// <summary>
    /// What a CONNECT tunnel is opened for: the target, the destination named in the CONNECT,
    /// the proxy, the connect's start and lookup timestamps, and whether the first CONNECT's
    /// <c>Proxy-Authorization</c> answers a <c>407</c> the proxy sent on a connection it closed,
    /// rather than being the value made before any challenge, and the events the proxy
    /// authenticator reports to, which keep its first SSPI failure for the tunnel's (BL-1033).
    /// </summary>
    private sealed record TunnelRequest(
        ConnectTarget Target,
        ConnectDestination Destination,
        ProxyEndpoint Proxy,
        long Started,
        long NameResolved,
        bool AuthorizationAnswersChallenge,
        SspiFailureRecordingTransferEvents AuthenticatorEvents)
    {
        /// <summary>
        /// Gets the transfer's header output for the CONNECT reply heads: the target's events as
        /// given, before any trace filter wrapped them, when they take the heads.
        /// </summary>
        public IConnectReplyHeadWritingEvents? HeadOutput { get; init; }

        /// <summary>
        /// Gets the running count of the CONNECT reply heads' bytes, every reply on every
        /// connection the tunnel was asked for, shared by the redials, which curl 8.21.0 adds to
        /// <c>%{size_header}</c> (upstream test1288, BL-2010).
        /// </summary>
        public StrongBox<long> ReplyHeadBytes { get; init; } = new();

        /// <summary>
        /// Gets the status code and header count of the last reply that carried a status, shared by
        /// the redials, which curl 8.22.0 still reports as <c>%{http_connect}</c> and
        /// <c>%{num_headers}</c> when a later CONNECT fails with no status of its own (STD-168).
        /// </summary>
        public StrongBox<(int StatusCode, int HeaderCount)> LastStatusReply { get; init; } = new();
    }
}
