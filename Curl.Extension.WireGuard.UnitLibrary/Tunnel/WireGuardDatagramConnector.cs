using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Curl.Extension.WireGuard.Configuration;
using Curl.Extension.WireGuard.IpStack;
using Curl.Extension.WireGuard.IpStack.Udp;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WireGuard.Tunnel;

/// <summary>
/// Stands in for curl's datagram connector under <c>--wireguard</c> (ADR-1163): each channel is a UDP socket of the
/// user-space IP stack inside its own tunnel - the peer cryptokey routing picks, a new UDP socket and handshake to it -
/// so QUIC for HTTP/3 travels sealed as transport data and no datagram leaves outside the tunnel. A host name is
/// resolved by <paramref name="resolver" />'s DNS over TCP through the tunnel (ADR-1161).
/// </summary>
/// <param name="configuration">The wg-quick configuration.</param>
/// <param name="opener">Opens the UDP socket to the peer.</param>
/// <param name="timeProvider">The clock the handshake runs on.</param>
/// <param name="random">The randomness for the handshake and the socket's ephemeral port.</param>
/// <param name="resolver">The tunnel's TCP connector, whose DNS through the tunnel resolves a host name.</param>
internal sealed class WireGuardDatagramConnector(
    WireGuardConfiguration configuration,
    IWireGuardUdpOpener opener,
    TimeProvider timeProvider,
    RandomNumberGenerator random,
    WireGuardConnector resolver) : IDatagramConnector
{
    /// <inheritdoc />
    public async ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        TunnelResolution resolution = IPAddress.TryParse(host.Trim('[', ']'), out IPAddress? literal)
            ? new TunnelResolution(literal, null)
            : await resolver.ResolveThroughTunnelAsync(host, cancellationToken).ConfigureAwait(false);
        return resolution.Address is null
            ? NotResolved(host, resolution.Failure)
            : await OpenToAsync(new IPEndPoint(resolution.Address, port), host, cancellationToken).ConfigureAwait(false);
    }

    private static DatagramOpenResult NotResolved(string host, ConnectResult? failure) =>
        failure is null
            ? DatagramOpenResult.Failed(CurlExitCode.CouldntResolveHost, $"Could not resolve host: {host}")
            : DatagramOpenResult.Failed(failure.ExitCode, failure.ErrorMessage!);

    private static DatagramOpenResult Unreachable(string host, int port, string reason) =>
        DatagramOpenResult.Failed(CurlExitCode.CouldntConnect, $"Failed to connect to {host} port {port}{reason}");

    private async ValueTask<DatagramOpenResult> OpenToAsync(IPEndPoint server, string host, CancellationToken cancellationToken)
    {
        WireGuardPeer? peer = WireGuardRoute.PeerFor(configuration.Peers, server.Address);
        IPAddress? source = WireGuardRoute.SourceFor(configuration.Interface.Addresses, server.Address);
        if (peer is null || source is null)
        {
            return Unreachable(host, server.Port, ": no WireGuard peer's AllowedIPs and interface Address cover it");
        }

        IWireGuardUdpChannel udp;
        try
        {
            udp = await opener.OpenAsync(peer.Endpoint!, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException exception)
        {
            return Unreachable(host, server.Port, $" through the WireGuard tunnel: {exception.Message}");
        }

        Exception failure;
        try
        {
            WireGuardPacketTunnel tunnel = await WireGuardPacketTunnel.OpenAsync(configuration.Interface, peer, udp, timeProvider, random, cancellationToken).ConfigureAwait(false);
            IPEndPoint local = new(source, EphemeralPort());
            return DatagramOpenResult.Opened(new UserSpaceUdpChannel(tunnel, local, server, new WireGuardTunnelLifetime(udp, tunnel)));
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        await udp.DisposeAsync().ConfigureAwait(false);
        return WireGuardConnector.IsTunnelFailure(failure)
            ? Unreachable(host, server.Port, $" through the WireGuard tunnel: {failure.Message}")
            : throw failure;
    }

    private int EphemeralPort()
    {
        byte[] bytes = new byte[2];
        random.GetBytes(bytes);
        return UserSpaceTcpClient.FirstEphemeralPort + (BitConverter.ToUInt16(bytes) % (IPEndPoint.MaxPort + 1 - UserSpaceTcpClient.FirstEphemeralPort));
    }
}
