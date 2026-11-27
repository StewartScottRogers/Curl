using System.Runtime.ExceptionServices;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    private async ValueTask<ConnectResult> OpenSocksTunnelAsync(
        DialedSocket dialed,
        ConnectTarget target,
        ConnectDestination destination,
        ProxyEndpoint proxy,
        ConnectTimings timings,
        CancellationToken cancellationToken)
    {
        if (await OpenSocksHopAsync(dialed, target, destination, proxy, cancellationToken).ConfigureAwait(false) is { } failure)
        {
            return failure;
        }

        // As through an HTTP proxy, %{time_connect} is when the tunnel is open, and the setup filter
        // adds an https:// origin's SSL filter then (measured, BL-1254 Notes).
        return await SecureWhenAskedAsync(dialed, target, timings with { Connected = timeProvider.GetTimestamp() }, 0, cancellationToken, WritesSslFilterAddedFor(target)).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs the SOCKS handshake to <paramref name="destination" /> over <paramref name="dialed" />'s connection:
    /// <see langword="null" /> once the tunnel is open, after curl's <c>Opened SOCKS connection</c>
    /// line, else the failure, with the connection disposed and no line.
    /// </summary>
    private async ValueTask<ConnectResult?> OpenSocksHopAsync(
        DialedSocket dialed,
        ConnectTarget target,
        ConnectDestination destination,
        ProxyEndpoint socks,
        CancellationToken cancellationToken)
    {
        var connection = dialed.Connection;
        var log = new NetworkDiagnosticLog(target.DiagnosticLog);
        log.SocksHandshakeStarting(socks, destination.Host, destination.Port);
        if (TracesSetupFilter)
        {
            // curl 8.21.0's setup filter adds the SOCKS filter once the proxy's socket connected (BL-1191 Notes).
            target.Events.ReportInfo($"[SETUP] added SOCKS filter to {destination.Host}:{destination.Port}");
        }

        var (failure, exception) = await RunSocksHandshakeAsync(connection, destination, socks, target, cancellationToken).ConfigureAwait(false);
        if (exception is not null || failure is not null)
        {
            // The proxy connection is disposed whether the handshake failed or could not be sent or read.
            await connection.DisposeAsync().ConfigureAwait(false);
            exception?.Throw();

            ReportSocks4Failure(target.Events, socks, failure!);
            return failure;
        }

        // curl 8.21.0 names the destination as given, not the address SOCKS4 or SOCKS5 sent for it,
        // and the proxy by the address it dialled (measured for every kind and --preproxy, BL-1038).
        target.Events.ReportInfo(SocksOpenedLine(dialed, destination));
        log.TunnelEstablished(socks, destination.Host, destination.Port, statusCode: 0);
        return null;
    }

    /// <summary>
    /// curl's <c>Opened SOCKS connection from ... (via ...)</c> line. A hop dialled over TCP names
    /// both end points; one over a Unix domain socket (BL-2955) has none, so it names no local
    /// address on port 0 and the socket's path on port 0, as curl's <c>ipquad</c> reads a Unix socket.
    /// </summary>
    private static string SocksOpenedLine(DialedSocket dialed, ConnectDestination destination)
    {
        var (localAddress, localPort) = dialed.LocalEndPoint is { } local ? (local.Address.ToString(), local.Port) : (string.Empty, 0);
        var (viaAddress, viaPort) = dialed.RemoteEndPoint is { } via ? (via.Address.ToString(), via.Port) : (dialed.UnixSocketRemoteIp, 0);
        return $"Opened SOCKS connection from {localAddress} port {localPort} to {destination.Host} port {destination.Port} (via {viaAddress} port {viaPort})";
    }

    private async ValueTask<(ConnectResult? Failure, ExceptionDispatchInfo? Exception)> RunSocksHandshakeAsync(
        IConnection connection,
        ConnectDestination destination,
        ProxyEndpoint proxy,
        ConnectTarget target,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await SocksProxyTunnel.OpenAsync(
                connection,
                proxy,
                destination.Host,
                destination.Port,
                (host, port, token) => ResolveWithFailureReasonAsync(host, port, target, token),
                Socks5Authentication,
                target.Events,
                TracesSocksFilter ? target.Events : null,
                cancellationToken).ConfigureAwait(false), null);
        }
        catch (Exception exception)
        {
            return (null, ExceptionDispatchInfo.Capture(exception));
        }
    }

    /// <summary>
    /// Gives the failure of a CONNECT the proxy refused: the reply's own failure, or exit 7
    /// <c>CONNECT tunnel failed, response N</c>, which curl 8.22.0's <c>failf</c> also writes as a
    /// <c>-v</c> line (measured, BL-3617).
    /// </summary>
    /// <summary>
    /// Writes a failed SOCKS4 or SOCKS4a handshake's message as a <c>-v</c> line, as curl 8.22.0's
    /// <c>failf</c> does (measured, BL-3617); SOCKS5's failures already wrote theirs.
    /// </summary>
    private static void ReportSocks4Failure(ITransferEvents events, ProxyEndpoint socks, ConnectResult failure)
    {
        if (socks.Kind is ProxyKind.Socks4 or ProxyKind.Socks4a)
        {
            events.ReportInfo(failure.ErrorMessage!);
        }
    }

    // Whether the SOCKS filter is the connection's topmost: the target's own SOCKS proxy, or the
    // pre-proxy in front of a forward HTTP proxy. Through a CONNECT tunnel the proxy's filter is above it.
    private bool IsThroughSocks(ConnectTarget target) =>
        TunnelProxyOf(target)?.Kind is ProxyKind.Socks4 or ProxyKind.Socks4a or ProxyKind.Socks5 or ProxyKind.Socks5Hostname;
}
