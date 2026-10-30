using Curl.Protocol.Abstractions;

namespace Curl.Extension.WireGuard.Tunnel;

/// <summary>
/// Stands in for curl's datagram connector under <c>--wireguard</c>, refusing every UDP socket so that no datagram
/// (QUIC for HTTP/3) leaves outside the tunnel; the IP stack carries TCP alone (ADR-1150).
/// </summary>
internal sealed class WireGuardDatagramConnector : IDatagramConnector
{
    /// <inheritdoc />
    public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
        ValueTask.FromResult(DatagramOpenResult.Failed(CurlExitCode.NotBuiltIn, $"UDP to {host} port {port} through the WireGuard tunnel is not built in"));
}
