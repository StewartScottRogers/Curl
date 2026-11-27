using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    /// <summary>
    /// Ends the CONNECT a <c>2xx</c> with <paramref name="statusCode" /> answered: its proxy
    /// authorization, curl's <c>CONNECT phase completed</c> and <c>CONNECT tunnel established</c>
    /// lines (BL-964), and the tunnel's diagnostic line; then runs the target's TLS when asked.
    /// </summary>
    private async ValueTask<ConnectResult> SecureOpenedTunnelAsync(
        DialedSocket dialed,
        HttpProxyTunnelTrace trace,
        TunnelRequest tunnel,
        int statusCode,
        string? proxyAuthorization,
        CancellationToken cancellationToken)
    {
        EndProxyAuthorization(proxyAuthorization);
        ConnectTunnelVerboseLines.ReportTunnelEstablished(tunnel.Target.Events, statusCode);
        trace.ReportEstablished(tunnel.Target.Events);

        // For a tunnel, %{time_connect} is when the tunnel is open (ConnectTimings.Connected).
        var timings = new ConnectTimings(tunnel.Started, tunnel.NameResolved, timeProvider.GetTimestamp(), null);
        new NetworkDiagnosticLog(tunnel.Target.DiagnosticLog).TunnelEstablished(tunnel.Proxy, tunnel.Destination.Host, tunnel.Destination.Port, statusCode);

        // Through a plain HTTP proxy the setup filter adds an https:// origin's SSL filter once the
        // tunnel is open (measured, BL-1193 Notes).
        var writesSslFilterAdded = WritesSslFilterAddedFor(tunnel.Target);
        return await SecureWhenAskedAsync(dialed, tunnel.Target, timings, statusCode, cancellationToken, writesSslFilterAdded).ConfigureAwait(false);
    }
}
