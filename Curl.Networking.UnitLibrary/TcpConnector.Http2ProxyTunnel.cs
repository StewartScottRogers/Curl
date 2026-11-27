using System.Runtime.ExceptionServices;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    /// <summary>The ALPN identifier of HTTP/2 over TLS, on which an HTTPS proxy is tunnelled through an HTTP/2 stream.</summary>
    private const string Http2ApplicationProtocol = "h2";

    /// <summary>
    /// Tunnels through an HTTPS proxy that agreed on <c>h2</c> with an HTTP/2 <c>CONNECT</c> stream
    /// (<see cref="Http2ProxyTunnelConnection" />, ADR-0408), with curl 8.18.0's <c>-v</c> lines
    /// (measured, BL-1413 Notes): <c>Establish HTTP/2 proxy tunnel to</c> first; on a <c>2xx</c>
    /// <c>CONNECT tunnel established, response</c> then <c>CONNECT phase completed</c>, the reverse
    /// of the HTTP/1.1 tunnel's order. Any other answer, or none, is exit 7 with curl's
    /// <c>Could not connect to server</c> and no line for the proxy's reply.
    /// </summary>
    private async ValueTask<ConnectResult> OpenHttp2TunnelAsync(
        DialedSocket dialed,
        TunnelRequest tunnel,
        string? proxyAuthorization,
        CancellationToken cancellationToken)
    {
        var events = tunnel.Target.Events;
        var authority = HttpProxyTunnel.FormatAuthority(tunnel.Destination.Host, tunnel.Destination.Port);
        events.ReportInfo($"Establish HTTP/2 proxy tunnel to {authority}");
        new NetworkDiagnosticLog(tunnel.Target.DiagnosticLog).TunnelRequested(tunnel.Proxy, tunnel.Destination.Host, tunnel.Destination.Port);
        var headerFields = Http2ProxyTunnelConnection.ConnectHeaderFields(authority, proxyAuthorization, _proxyTunnelOptions.UserAgent);
        (int StatusCode, Http2ProxyTunnelConnection? Tunnel) opened = default;
        ExceptionDispatchInfo? exception = null;
        try
        {
            opened = await Http2ProxyTunnelConnection.OpenAsync(dialed.Connection, headerFields, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception caught)
        {
            exception = ExceptionDispatchInfo.Capture(caught);
        }

        var (statusCode, tunnelConnection) = opened;
        if (tunnelConnection is null)
        {
            // The proxy's connection goes whether the CONNECT was refused or could not be exchanged.
            await dialed.Connection.DisposeAsync().ConfigureAwait(false);
            exception?.Throw();
            return ConnectResult.Failed(CurlExitCode.CouldntConnect, "Could not connect to server");
        }

        EndProxyAuthorization(proxyAuthorization);
        events.ReportInfo($"CONNECT tunnel established, response {statusCode}");
        events.ReportInfo("CONNECT phase completed");
        var timings = new ConnectTimings(tunnel.Started, tunnel.NameResolved, timeProvider.GetTimestamp(), null);
        new NetworkDiagnosticLog(tunnel.Target.DiagnosticLog).TunnelEstablished(tunnel.Proxy, tunnel.Destination.Host, tunnel.Destination.Port, statusCode);
        return await SecureWhenAskedAsync(dialed with { Connection = tunnelConnection }, tunnel.Target, timings, statusCode, cancellationToken).ConfigureAwait(false);
    }
}
