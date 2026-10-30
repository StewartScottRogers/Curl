using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Curl.Extension.WireGuard.Configuration;
using Curl.Extension.WireGuard.IpStack;
using Curl.Extension.WireGuard.Noise;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WireGuard.Tunnel;

/// <summary>
/// Opens each connection of a transfer inside its own WireGuard tunnel (ADR-1159): the peer that cryptokey routing
/// picks for the target address, a new UDP socket and handshake to it, and a user-space TCP connection through it;
/// curl's TLS handshake runs over that for a TLS target (ADR-1055).
/// </summary>
/// <param name="configuration">The wg-quick configuration.</param>
/// <param name="opener">Opens the UDP socket to the peer.</param>
/// <param name="timeProvider">The clock the handshake and TCP timers run on.</param>
/// <param name="random">The randomness for the handshake's ephemeral keys and indexes.</param>
/// <param name="tlsProvider">The TLS provider curl built from the option group's TLS options; <see langword="null" /> refuses a TLS target.</param>
/// <param name="httpOverTlsApplicationProtocols">What curl's connector offers through ALPN for <c>https</c>; <see langword="null" /> offers <c>http/1.1</c> alone.</param>
internal sealed class WireGuardConnector(
    WireGuardConfiguration configuration,
    IWireGuardUdpOpener opener,
    TimeProvider timeProvider,
    RandomNumberGenerator random,
    ITlsProvider? tlsProvider = null,
    IReadOnlyList<string>? httpOverTlsApplicationProtocols = null) : IConnector
{
    /// <summary>wg-quick's MTU when the configuration names none.</summary>
    public const int DefaultMtu = 1420;

    private static readonly IReadOnlyList<string> Http11Only = ["http/1.1"];

    /// <inheritdoc />
    public IReadOnlyList<string> HttpOverTlsApplicationProtocols => httpOverTlsApplicationProtocols ?? Http11Only;

    /// <inheritdoc />
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.UseTls && tlsProvider is null)
        {
            return ConnectResult.Failed(CurlExitCode.NotBuiltIn, "TLS through the WireGuard tunnel is not built in");
        }

        if (!IPAddress.TryParse(target.Host.Trim('[', ']'), out IPAddress? destination))
        {
            return ConnectResult.Failed(CurlExitCode.CouldntResolveHost, $"Could not resolve host: {target.Host} (--wireguard reaches IP addresses only)");
        }

        WireGuardPeer? peer = WireGuardRoute.PeerFor(configuration.Peers, destination);
        IPAddress? source = WireGuardRoute.SourceFor(configuration.Interface.Addresses, destination);
        return peer is null || source is null
            ? ConnectResult.Failed(CurlExitCode.CouldntConnect, $"Failed to connect to {target.Host} port {target.Port}: no WireGuard peer's AllowedIPs and interface Address cover it")
            : await ConnectThroughAsync(peer, source, new IPEndPoint(destination, target.Port), target, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives what the handshake for <paramref name="target" /> offers through ALPN: the target's own list or else
    /// <see cref="HttpOverTlsApplicationProtocols" /> for an <c>https</c> pool scheme, and nothing for any other.
    /// </summary>
    /// <param name="target">The target whose handshake is about to run.</param>
    /// <returns>The protocols to offer, in preference order.</returns>
    internal IReadOnlyList<string> ApplicationProtocolsFor(ConnectTarget target) =>
        string.Equals(target.PoolScheme, "https", StringComparison.OrdinalIgnoreCase)
            ? target.ApplicationProtocols ?? HttpOverTlsApplicationProtocols
            : [];

    private static ConnectResult Unreachable(ConnectTarget target, Exception exception) =>
        ConnectResult.Failed(CurlExitCode.CouldntConnect, $"Failed to connect to {target.Host} port {target.Port} through the WireGuard tunnel: {exception.Message}");

    private ushort MaximumSegmentSizeFor(IPAddress destination) =>
        (ushort)((configuration.Interface.Mtu ?? DefaultMtu) - (destination.AddressFamily == AddressFamily.InterNetwork ? 40 : 60));

    private async ValueTask<ConnectResult> ConnectThroughAsync(WireGuardPeer peer, IPAddress source, IPEndPoint destination, ConnectTarget target, CancellationToken cancellationToken)
    {
        IWireGuardUdpChannel udp;
        try
        {
            udp = await opener.OpenAsync(peer.Endpoint!, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException exception)
        {
            return Unreachable(target, exception);
        }

        Exception failure;
        try
        {
            return await ConnectOverAsync(udp, peer, source, destination, target, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        await udp.DisposeAsync().ConfigureAwait(false);
        return failure is TimeoutException or IOException ? Unreachable(target, failure) : throw failure;
    }

    private async ValueTask<ConnectResult> ConnectOverAsync(IWireGuardUdpChannel udp, WireGuardPeer peer, IPAddress source, IPEndPoint destination, ConnectTarget target, CancellationToken cancellationToken)
    {
        WireGuardHandshakeInitiator initiator = WireGuardHandshakeInitiator.ForPeer(configuration.Interface, peer, timeProvider, random);
        WireGuardTransportSession session = await WireGuardTunnelHandshake.ConnectAsync(initiator, udp, timeProvider, cancellationToken).ConfigureAwait(false);
        Stream stream;
        try
        {
            stream = await UserSpaceTcpClient.ConnectAsync(new WireGuardPacketTunnel(udp, session), source, destination, MaximumSegmentSizeFor(destination.Address), timeProvider, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            session.Dispose();
            throw;
        }

        WireGuardConnection connection = new(stream, udp, session, destination);
        return target.UseTls
            ? await tlsProvider!.AuthenticateAsClientAsync(connection, TlsServerNameOf(target), target.Events, ApplicationProtocolsFor(target), cancellationToken).ConfigureAwait(false)
            : ConnectResult.Connected(connection);
    }

    private static string TlsServerNameOf(ConnectTarget target) => target.TlsServerName ?? target.Host;
}
