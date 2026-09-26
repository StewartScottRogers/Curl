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
/// <param name="timeProvider">Measures the connect time curl reports in its exit 7 message.</param>
/// <param name="proxyTunnelOptions">
/// The <c>User-Agent</c> and credential encoding of the CONNECT request that tunnels through
/// an HTTP proxy; <see langword="null" /> for <see cref="HttpProxyTunnelOptions.Default" />.
/// </param>
public sealed class TcpConnector(
    IDnsResolver dnsResolver,
    ITcpDialer tcpDialer,
    ITlsProvider tlsProvider,
    TimeProvider timeProvider,
    HttpProxyTunnelOptions? proxyTunnelOptions = null) : IConnector
{
    private readonly HttpProxyTunnelOptions _proxyTunnelOptions = proxyTunnelOptions ?? HttpProxyTunnelOptions.Default;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The messages are curl 8.21.0's: <c>Could not resolve host: &lt;host&gt;</c> for
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
    /// </remarks>
    /// <exception cref="NotSupportedException">
    /// <see cref="ConnectTarget.Proxy" /> is an HTTPS or SOCKS proxy, which this connector
    /// does not tunnel through yet.
    /// </exception>
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.Proxy is { } proxy)
        {
            return await ConnectThroughHttpProxyAsync(target, proxy, cancellationToken).ConfigureAwait(false);
        }

        var addresses = await dnsResolver.ResolveAsync(target.Host, cancellationToken).ConfigureAwait(false);
        if (addresses.Count == 0)
        {
            return ConnectResult.Failed(
                CurlExitCode.CouldntResolveHost,
                $"Could not resolve host: {target.Host}");
        }

        var dialStarted = timeProvider.GetTimestamp();
        var connection = await DialFirstReachableAsync(addresses, target.Port, cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            var elapsedMilliseconds = (long)timeProvider.GetElapsedTime(dialStarted).TotalMilliseconds;
            return ConnectResult.Failed(
                CurlExitCode.CouldntConnect,
                $"Failed to connect to {target.Host}:{target.Port} after {elapsedMilliseconds} ms: Could not connect to server");
        }

        if (!target.UseTls)
        {
            return ConnectResult.Connected(connection);
        }

        return await tlsProvider.AuthenticateAsClientAsync(connection, target.Host, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ConnectResult> ConnectThroughHttpProxyAsync(
        ConnectTarget target,
        ProxyEndpoint proxy,
        CancellationToken cancellationToken)
    {
        if (proxy.Kind is not (ProxyKind.Http or ProxyKind.Http10))
        {
            throw new NotSupportedException($"Tunnelling through a {proxy.Kind} proxy is not implemented yet.");
        }

        var addresses = await dnsResolver.ResolveAsync(proxy.Host, cancellationToken).ConfigureAwait(false);
        if (addresses.Count == 0)
        {
            return ConnectResult.Failed(
                CurlExitCode.CouldntResolveProxy,
                $"Could not resolve proxy: {proxy.Host}");
        }

        var dialStarted = timeProvider.GetTimestamp();
        var connection = await DialFirstReachableAsync(addresses, proxy.Port, cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            var elapsedMilliseconds = (long)timeProvider.GetElapsedTime(dialStarted).TotalMilliseconds;
            return ConnectResult.Failed(
                CurlExitCode.CouldntConnect,
                $"Failed to connect to {target.Host}:{target.Port} over proxy {proxy.Host} after {elapsedMilliseconds} ms: Could not connect to server");
        }

        return await OpenTunnelAsync(connection, target, proxy, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ConnectResult> OpenTunnelAsync(
        IConnection connection,
        ConnectTarget target,
        ProxyEndpoint proxy,
        CancellationToken cancellationToken)
    {
        var (reply, exception) = await RequestTunnelAsync(connection, target, proxy, cancellationToken).ConfigureAwait(false);
        if (exception is not null || !reply.OpensTunnel)
        {
            // The proxy connection is disposed whether CONNECT failed or could not be sent or read.
            await connection.DisposeAsync().ConfigureAwait(false);
            exception?.Throw();
            return TunnelFailure(reply);
        }

        return target.UseTls
            ? await SecureTunnelAsync(connection, target, reply.StatusCode, cancellationToken).ConfigureAwait(false)
            : ConnectResult.Connected(connection, null, null, reply.StatusCode);
    }

    private async ValueTask<(HttpProxyTunnelReply Reply, ExceptionDispatchInfo? Exception)> RequestTunnelAsync(
        IConnection connection,
        ConnectTarget target,
        ProxyEndpoint proxy,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.WriteAsync(HttpProxyTunnel.BuildConnectRequest(target, proxy, _proxyTunnelOptions), cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            return (await HttpProxyTunnel.ReadReplyAsync(connection, cancellationToken).ConfigureAwait(false), null);
        }
        catch (Exception exception)
        {
            return (default, ExceptionDispatchInfo.Capture(exception));
        }
    }

    private static ConnectResult TunnelFailure(HttpProxyTunnelReply reply) =>
        reply.RecvErrorMessage is { } recvErrorMessage
            ? ConnectResult.Failed(CurlExitCode.RecvError, recvErrorMessage)
            : ConnectResult.Failed(CurlExitCode.CouldntConnect, $"CONNECT tunnel failed, response {reply.StatusCode}");

    private async ValueTask<ConnectResult> SecureTunnelAsync(
        IConnection tunnel,
        ConnectTarget target,
        int tunnelStatusCode,
        CancellationToken cancellationToken)
    {
        var secured = await tlsProvider.AuthenticateAsClientAsync(tunnel, target.Host, cancellationToken).ConfigureAwait(false);
        return secured.Connection is { } securedConnection
            ? ConnectResult.Connected(securedConnection, secured.Timings, secured.LocalEndPoint, tunnelStatusCode)
            : secured;
    }

    private async ValueTask<IConnection?> DialFirstReachableAsync(
        IReadOnlyList<IPAddress> addresses,
        int port,
        CancellationToken cancellationToken)
    {
        foreach (var address in addresses)
        {
            try
            {
                return await tcpDialer.DialAsync(new IPEndPoint(address, port), cancellationToken).ConfigureAwait(false);
            }
            catch (SocketException)
            {
                // curl moves on to the next address; only when every one fails is it exit 7.
            }
        }

        return null;
    }
}
