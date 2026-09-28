using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="IConnector" /> for TCP: resolves the host, dials each
/// address in the resolver's order until one connects, and hands the connection to
/// <see cref="ITlsProvider" /> when the target asks for TLS.
/// </summary>
/// <param name="dnsResolver">Resolves the target host to addresses.</param>
/// <param name="tcpDialer">Opens a plaintext connection to one address.</param>
/// <param name="tlsProvider">Upgrades the connection when <see cref="ConnectTarget.UseTls" /> is set.</param>
/// <param name="timeProvider">
/// Takes every timestamp in <see cref="ConnectResult.Timings" />, and measures the connect
/// time curl reports in its exit 7 message.
/// </param>
/// <param name="proxyTunnelOptions">
/// The <c>User-Agent</c> and credential encoding of the CONNECT request that tunnels through
/// an HTTP proxy; <see langword="null" /> for <see cref="HttpProxyTunnelOptions.Default" />.
/// </param>
/// <param name="resolveOverrides">
/// The <c>--resolve</c> entries, consulted for a <c>host:port</c> before
/// <paramref name="dnsResolver" />; <see langword="null" /> for <see cref="ResolveOverrides.None" />.
/// </param>
/// <param name="connectToMappings">
/// The <c>--connect-to</c> mappings, which change the host and port resolved and dialled
/// but not the host TLS verifies; <see langword="null" /> for <see cref="ConnectToMappings.None" />.
/// </param>
/// <param name="proxyTlsProvider">
/// Runs the handshake to an HTTPS proxy (<see cref="ProxyKind.Https" />), as curl verifies the
/// proxy with the <c>--proxy-*</c> TLS options and the target with <c>-k</c> and <c>--cacert</c>;
/// <see langword="null" /> for <paramref name="tlsProvider" />.
/// </param>
public sealed class TcpConnector(
    IDnsResolver dnsResolver,
    ITcpDialer tcpDialer,
    ITlsProvider tlsProvider,
    TimeProvider timeProvider,
    HttpProxyTunnelOptions? proxyTunnelOptions = null,
    ResolveOverrides? resolveOverrides = null,
    ConnectToMappings? connectToMappings = null,
    ITlsProvider? proxyTlsProvider = null) : IConnector
{
    private readonly ITlsProvider _proxyTlsProvider = proxyTlsProvider ?? tlsProvider;
    private readonly HttpProxyTunnelOptions _proxyTunnelOptions = proxyTunnelOptions ?? HttpProxyTunnelOptions.Default;
    private readonly ResolveOverrides _resolveOverrides = resolveOverrides ?? ResolveOverrides.None;
    private readonly ConnectToMappings _connectToMappings = connectToMappings ?? ConnectToMappings.None;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The messages are curl 8.21.0's, cut to 255 characters as curl cuts them (ADR-0072):
    /// <c>Could not resolve host: &lt;host&gt;</c> for
    /// exit 6, and <c>Failed to connect to &lt;host&gt;:&lt;port&gt; after &lt;n&gt; ms:
    /// Could not connect to server</c> for exit 7, where <c>n</c> is the time spent
    /// dialing as measured by the injected <see cref="TimeProvider" />. When
    /// <see cref="ConnectTarget.UseTls" /> is set, the <see cref="ITlsProvider" />'s result
    /// is returned as it is, so a failed handshake keeps the exit code and message the
    /// provider chose.
    /// </para>
    /// <para>
    /// When <see cref="ConnectTarget.Proxy" /> is an HTTP proxy (<see cref="ProxyKind.Http" />
    /// or <see cref="ProxyKind.Http10" />), the connector resolves and dials the proxy, sends
    /// CONNECT for the target, and applies TLS over the tunnel when
    /// <see cref="ConnectTarget.UseTls" /> is set; the proxy's status code is
    /// <see cref="ConnectResult.ProxyConnectResponseCode" />. The failures are curl 8.21.0's:
    /// exit 5 <c>Could not resolve proxy: &lt;proxy host&gt;</c>, exit 7 <c>Failed to connect
    /// to &lt;host&gt;:&lt;port&gt; over proxy &lt;proxy host&gt; after &lt;n&gt; ms: Could not
    /// connect to server</c>, exit 7 <c>CONNECT tunnel failed, response &lt;code&gt;</c> for a
    /// status outside 200 to 299 (<c>0</c> when the reply is not HTTP), and exit 56
    /// <c>Proxy CONNECT aborted</c> when the proxy closes before its header block ends,
    /// <c>CONNECT response too large</c> for a reply line of 16384 bytes or more, and
    /// <c>Too large response headers: &lt;n&gt; &gt; 307200</c> for a longer header block.
    /// </para>
    /// <para>
    /// When <see cref="ConnectTarget.Proxy" /> is an HTTPS proxy (<see cref="ProxyKind.Https" />),
    /// the connector resolves and dials the proxy with the same exit 5 and exit 7 failures,
    /// runs TLS to it through the proxy's <see cref="ITlsProvider" />, verified against the proxy host,
    /// then sends the same CONNECT over that TLS and, when <see cref="ConnectTarget.UseTls" />
    /// is set, runs a second handshake to <see cref="ConnectTarget.Host" /> inside it, as curl
    /// 8.21.0 does. A failed handshake to the proxy or to the target is the provider's result
    /// as it is, and a refused CONNECT fails as through an HTTP proxy.
    /// <see cref="ConnectTimings.TlsHandshakeCompleted" /> and
    /// <see cref="ConnectResult.PeerCertificates" /> are the target's handshake's only, so an
    /// http target has none (measured: <c>%{time_appconnect}</c> is <c>0</c>).
    /// </para>
    /// <para>
    /// When <see cref="ConnectTarget.Proxy" /> is a SOCKS proxy (<see cref="ProxyKind.Socks4" />,
    /// <see cref="ProxyKind.Socks4a" />, <see cref="ProxyKind.Socks5" /> or
    /// <see cref="ProxyKind.Socks5Hostname" />), the connector resolves and dials the proxy the
    /// same way, with the same exit 5 and exit 7 failures, runs curl 8.21.0's handshake for
    /// the kind (<see cref="SocksProxyTunnel" />) and applies TLS over the tunnel when
    /// <see cref="ConnectTarget.UseTls" /> is set. SOCKS4 and SOCKS5 resolve the target
    /// locally, <c>--resolve</c> included, and fail with exit 6 when it does not resolve; a
    /// handshake the proxy refuses or cuts short fails with exit 97
    /// (<see cref="CurlExitCode.Proxy" />) and curl's message. The proxy connection is
    /// disposed on every failure. <see cref="ConnectResult.ProxyConnectResponseCode" /> is
    /// <c>0</c>.
    /// </para>
    /// <para>
    /// Before anything is resolved, a <c>--resolve</c> entry that did not parse, or a
    /// <c>--connect-to</c> mapping that matches the target and whose destination does not
    /// parse, fails with exit 49 and curl 8.21.0's message (see <see cref="ResolveOverrides" />
    /// and <see cref="ConnectToMappings" />). A matching mapping's host and port are resolved,
    /// dialled and, through an HTTP proxy, named in the CONNECT request; exit 6 then names the
    /// mapped host and exit 7 reads <c>Failed to connect to &lt;host&gt;:&lt;port&gt; via
    /// &lt;mapped host&gt;:&lt;mapped port&gt; after &lt;n&gt; ms: Could not connect to
    /// server</c> (measured). TLS still verifies <see cref="ConnectTarget.Host" />. A
    /// <c>--resolve</c> entry answers for the host and port being resolved, the proxy's
    /// included, in place of the <see cref="IDnsResolver" />.
    /// </para>
    /// <para>
    /// A success carries <see cref="ConnectResult.Timings" />, taken from the injected
    /// <see cref="TimeProvider" />: <see cref="ConnectTimings.Started" /> when this method
    /// begins, <see cref="ConnectTimings.NameResolved" /> when the host (or proxy) has
    /// resolved, <see cref="ConnectTimings.Connected" /> when the TCP connect completes or,
    /// through a proxy, when the tunnel is open, and <see cref="ConnectTimings.TlsHandshakeCompleted" />
    /// from the <see cref="ITlsProvider" />'s own timings, or when it returned if it recorded
    /// none. <see cref="ConnectResult.LocalEndPoint" /> is the local end point
    /// <see cref="ITcpDialer" /> reports; the remote end point is the connection's
    /// <see cref="IConnection.RemoteEndPoint" />. <see cref="ConnectResult.PeerCertificates" />
    /// are the <see cref="ITlsProvider" />'s, and empty without TLS.
    /// </para>
    /// <para>
    /// A dial that reached no address, to the host or to the proxy, carries
    /// <see cref="ConnectTimings.Started" /> and <see cref="ConnectTimings.NameResolved" /> with
    /// <see cref="ConnectTimings.Connected" /> <see langword="null" />, as curl 8.21.0 reports
    /// <c>%{time_namelookup}</c> but a <c>%{time_connect}</c> of <c>0</c> after a refused
    /// connect (measured, ADR-0091). A failed resolve, a bad <c>--resolve</c> or <c>--connect-to</c>
    /// entry and a failed tunnel carry none; a failed handshake is the provider's result as it is.
    /// </para>
    /// </remarks>
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        var started = timeProvider.GetTimestamp();
        var destination = _connectToMappings.Map(target.Host, target.Port);
        if ((_resolveOverrides.ParseError ?? destination.ParseError) is { } parseError)
        {
            return ConnectResult.Failed(CurlExitCode.SetoptOptionSyntax, parseError);
        }

        return target.Proxy is { } proxy
            ? await ConnectThroughProxyAsync(target, destination, proxy, started, cancellationToken).ConfigureAwait(false)
            : await ConnectDirectlyAsync(target, destination, started, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ConnectResult> ConnectDirectlyAsync(
        ConnectTarget target,
        ConnectDestination destination,
        long started,
        CancellationToken cancellationToken)
    {
        var addresses = await ResolveAsync(destination.Host, destination.Port, cancellationToken).ConfigureAwait(false);
        if (addresses.Count == 0)
        {
            return ConnectResult.Failed(
                CurlExitCode.CouldntResolveHost,
                CurlErrorBuffer.Truncate($"Could not resolve host: {destination.Host}"));
        }

        var nameResolved = timeProvider.GetTimestamp();
        var (dialed, lastDialError) = await DialFirstReachableAsync(addresses, destination.Port, cancellationToken).ConfigureAwait(false);
        if (dialed is null)
        {
            var elapsedMilliseconds = (long)timeProvider.GetElapsedTime(nameResolved).TotalMilliseconds;
            var via = destination.IsMapped ? $" via {destination.Host}:{destination.Port}" : string.Empty;
            return DialFailure(
                lastDialError,
                new ConnectTimings(started, nameResolved, null, null),
                $"Failed to connect to {target.Host}:{target.Port}{via} after {elapsedMilliseconds} ms: Could not connect to server");
        }

        var timings = new ConnectTimings(started, nameResolved, timeProvider.GetTimestamp(), null);
        return await SecureWhenAskedAsync(dialed, target, timings, 0, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, int port, CancellationToken cancellationToken) =>
        _resolveOverrides.Find(host, port) ?? await dnsResolver.ResolveAsync(host, cancellationToken).ConfigureAwait(false);

    private async ValueTask<ConnectResult> ConnectThroughProxyAsync(
        ConnectTarget target,
        ConnectDestination destination,
        ProxyEndpoint proxy,
        long started,
        CancellationToken cancellationToken)
    {
        var addresses = await ResolveAsync(proxy.Host, proxy.Port, cancellationToken).ConfigureAwait(false);
        if (addresses.Count == 0)
        {
            return ConnectResult.Failed(
                CurlExitCode.CouldntResolveProxy,
                CurlErrorBuffer.Truncate($"Could not resolve proxy: {proxy.Host}"));
        }

        var nameResolved = timeProvider.GetTimestamp();
        var (dialed, lastDialError) = await DialFirstReachableAsync(addresses, proxy.Port, cancellationToken).ConfigureAwait(false);
        if (dialed is null)
        {
            var elapsedMilliseconds = (long)timeProvider.GetElapsedTime(nameResolved).TotalMilliseconds;
            return DialFailure(
                lastDialError,
                new ConnectTimings(started, nameResolved, null, null),
                $"Failed to connect to {target.Host}:{target.Port} over proxy {proxy.Host} after {elapsedMilliseconds} ms: Could not connect to server");
        }

        return proxy.Kind switch
        {
            ProxyKind.Http or ProxyKind.Http10 => await OpenTunnelAsync(dialed, target, destination, proxy, started, nameResolved, cancellationToken).ConfigureAwait(false),
            ProxyKind.Https => await OpenTunnelOverTlsAsync(dialed, target, destination, proxy, started, nameResolved, cancellationToken).ConfigureAwait(false),
            _ => await OpenSocksTunnelAsync(dialed, target, destination, proxy, new ConnectTimings(started, nameResolved, 0, null), cancellationToken).ConfigureAwait(false),
        };
    }

    private async ValueTask<ConnectResult> OpenTunnelOverTlsAsync(
        DialedTcpConnection dialed,
        ConnectTarget target,
        ConnectDestination destination,
        ProxyEndpoint proxy,
        long started,
        long nameResolved,
        CancellationToken cancellationToken)
    {
        // curl 8.21.0 verifies the proxy against its own host name and reports a failed
        // handshake to it with the same exit code and message as one to a target (measured).
        // It verifies with the --proxy-* TLS options, not -k or --cacert, so the proxy's own
        // provider runs this handshake (ADR-0061).
        var securedProxy = await _proxyTlsProvider.AuthenticateAsClientAsync(dialed.Connection, proxy.Host, cancellationToken).ConfigureAwait(false);
        if (securedProxy.Connection is not { } proxyConnection)
        {
            return securedProxy;
        }

        // CONNECT and the target's TLS run over the proxy's TLS; the socket's local end point stays.
        var securedDialed = new DialedTcpConnection(proxyConnection, dialed.LocalEndPoint);
        return await OpenTunnelAsync(securedDialed, target, destination, proxy, started, nameResolved, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ConnectResult> OpenSocksTunnelAsync(
        DialedTcpConnection dialed,
        ConnectTarget target,
        ConnectDestination destination,
        ProxyEndpoint proxy,
        ConnectTimings timings,
        CancellationToken cancellationToken)
    {
        var connection = dialed.Connection;
        var (failure, exception) = await RunSocksHandshakeAsync(connection, destination, proxy, cancellationToken).ConfigureAwait(false);
        if (exception is not null || failure is not null)
        {
            // The proxy connection is disposed whether the handshake failed or could not be sent or read.
            await connection.DisposeAsync().ConfigureAwait(false);
            exception?.Throw();
            return failure!;
        }

        // As through an HTTP proxy, %{time_connect} is when the tunnel is open.
        return await SecureWhenAskedAsync(dialed, target, timings with { Connected = timeProvider.GetTimestamp() }, 0, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ConnectResult> OpenTunnelAsync(
        DialedTcpConnection dialed,
        ConnectTarget target,
        ConnectDestination destination,
        ProxyEndpoint proxy,
        long started,
        long nameResolved,
        CancellationToken cancellationToken)
    {
        var connection = dialed.Connection;
        var (reply, exception) = await RequestTunnelAsync(connection, destination, proxy, cancellationToken).ConfigureAwait(false);
        if (exception is not null || !reply.OpensTunnel)
        {
            // The proxy connection is disposed whether CONNECT failed or could not be sent or read.
            await connection.DisposeAsync().ConfigureAwait(false);
            exception?.Throw();
            return TunnelFailure(reply);
        }

        // For a tunnel, %{time_connect} is when the tunnel is open (ConnectTimings.Connected).
        var timings = new ConnectTimings(started, nameResolved, timeProvider.GetTimestamp(), null);
        return await SecureWhenAskedAsync(dialed, target, timings, reply.StatusCode, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<(HttpProxyTunnelReply Reply, ExceptionDispatchInfo? Exception)> RequestTunnelAsync(
        IConnection connection,
        ConnectDestination destination,
        ProxyEndpoint proxy,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.WriteAsync(HttpProxyTunnel.BuildConnectRequest(destination.Host, destination.Port, proxy, _proxyTunnelOptions), cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            return (await HttpProxyTunnel.ReadReplyAsync(connection, cancellationToken).ConfigureAwait(false), null);
        }
        catch (Exception exception)
        {
            return (default, ExceptionDispatchInfo.Capture(exception));
        }
    }

    private async ValueTask<(ConnectResult? Failure, ExceptionDispatchInfo? Exception)> RunSocksHandshakeAsync(
        IConnection connection,
        ConnectDestination destination,
        ProxyEndpoint proxy,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await SocksProxyTunnel.OpenAsync(connection, proxy, destination.Host, destination.Port, ResolveAsync, cancellationToken).ConfigureAwait(false), null);
        }
        catch (Exception exception)
        {
            return (null, ExceptionDispatchInfo.Capture(exception));
        }
    }

    private static ConnectResult TunnelFailure(HttpProxyTunnelReply reply) =>
        reply.RecvErrorMessage is { } recvErrorMessage
            ? ConnectResult.Failed(CurlExitCode.RecvError, recvErrorMessage)
            : ConnectResult.Failed(CurlExitCode.CouldntConnect, $"CONNECT tunnel failed, response {reply.StatusCode}");

    private async ValueTask<ConnectResult> SecureWhenAskedAsync(
        DialedTcpConnection dialed,
        ConnectTarget target,
        ConnectTimings timings,
        int proxyConnectResponseCode,
        CancellationToken cancellationToken)
    {
        if (!target.UseTls)
        {
            return ConnectResult.Connected(dialed.Connection, timings, dialed.LocalEndPoint, proxyConnectResponseCode);
        }

        var secured = await tlsProvider.AuthenticateAsClientAsync(dialed.Connection, target.Host, cancellationToken).ConfigureAwait(false);
        if (secured.Connection is not { } securedConnection)
        {
            return secured;
        }

        // A provider that measured its handshake is trusted for the moment it completed; for
        // one that did not, the moment it returned is that moment.
        var handshakeCompleted = secured.Timings?.TlsHandshakeCompleted ?? timeProvider.GetTimestamp();
        return ConnectResult.Connected(
            securedConnection,
            timings with { TlsHandshakeCompleted = handshakeCompleted },
            dialed.LocalEndPoint,
            proxyConnectResponseCode,
            secured.PeerCertificates);
    }

    /// <summary>
    /// Dials each address in turn and returns the first connection, or <see langword="null" />
    /// with the <see cref="SocketError" /> of the last attempt, which curl keeps as
    /// <c>CURLINFO_OS_ERRNO</c>.
    /// </summary>
    private async ValueTask<(DialedTcpConnection? Dialed, SocketError LastError)> DialFirstReachableAsync(
        IReadOnlyList<IPAddress> addresses,
        int port,
        CancellationToken cancellationToken)
    {
        var lastError = SocketError.Success;
        foreach (var address in addresses)
        {
            try
            {
                return (await tcpDialer.DialAsync(new IPEndPoint(address, port), cancellationToken).ConfigureAwait(false), SocketError.Success);
            }
            catch (SocketException exception)
            {
                // curl moves on to the next address; only when every one fails is it exit 7.
                lastError = exception.SocketErrorCode;
            }
        }

        return (null, lastError);
    }

    /// <summary>
    /// The exit 7 for a dial that reached no address: marked refused when the last attempt
    /// was refused, as <c>--retry-connrefused</c> reads curl's <c>CURLINFO_OS_ERRNO</c>. It
    /// carries the start and lookup timestamps, as curl 8.21.0 still reports
    /// <c>%{time_namelookup}</c> after a refused connect (measured, ADR-0091).
    /// </summary>
    private static ConnectResult DialFailure(SocketError lastError, ConnectTimings timings, string errorMessage) =>
        lastError == SocketError.ConnectionRefused
            ? ConnectResult.Refused(errorMessage, timings)
            : ConnectResult.Failed(CurlExitCode.CouldntConnect, errorMessage, timings);
}
