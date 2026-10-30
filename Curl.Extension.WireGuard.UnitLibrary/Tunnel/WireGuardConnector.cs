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
/// curl's TLS handshake runs over that for a TLS target (ADR-1055). A host name is resolved by DNS over TCP through
/// the tunnel to the configuration's first <c>DNS</c> address (ADR-1161), never by the system resolver.
/// </summary>
/// <param name="configuration">The wg-quick configuration.</param>
/// <param name="opener">Opens the UDP socket to the peer.</param>
/// <param name="timeProvider">The clock the handshake and TCP timers run on.</param>
/// <param name="random">The randomness for the handshake's ephemeral keys and indexes.</param>
/// <param name="tlsProvider">The TLS provider curl built from the option group's TLS options; <see langword="null" /> refuses a TLS target.</param>
/// <param name="httpOverTlsApplicationProtocols">What curl's connector offers through ALPN for <c>https</c>; <see langword="null" /> offers <c>http/1.1</c> alone.</param>
/// <param name="quicConnector">
/// Curl's connector, whose QUIC dialer opens its UDP through the hook's datagram connector and so through the tunnel
/// (ADR-1163); <see langword="null" /> refuses QUIC.
/// </param>
internal sealed class WireGuardConnector(
    WireGuardConfiguration configuration,
    IWireGuardUdpOpener opener,
    TimeProvider timeProvider,
    RandomNumberGenerator random,
    ITlsProvider? tlsProvider = null,
    IReadOnlyList<string>? httpOverTlsApplicationProtocols = null,
    IConnector? quicConnector = null) : IConnector
{
    /// <summary>wg-quick's MTU when the configuration names none.</summary>
    public const int DefaultMtu = 1420;

    private static readonly IReadOnlyList<string> Http11Only = ["http/1.1"];

    /// <inheritdoc />
    public IReadOnlyList<string> HttpOverTlsApplicationProtocols => httpOverTlsApplicationProtocols ?? Http11Only;

    /// <summary>Opens a QUIC connection through curl's connector, whose datagrams the tunnel carries (ADR-1163).</summary>
    /// <param name="target">The target.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>Curl's connector's result, or a refusal without one.</returns>
    public ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        quicConnector?.ConnectMultiplexedAsync(target, cancellationToken)
            ?? ValueTask.FromResult(MultiplexedConnectResult.Failed(CurlExitCode.CouldntConnect, "QUIC through the WireGuard tunnel needs curl's connector"));

    /// <summary>Opens a session on a QUIC connection through curl's connector, so its pool shares the connection (ADR-1163).</summary>
    /// <param name="target">The target.</param>
    /// <param name="openSession">Opens the session on the connection.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>Curl's connector's result, or a refusal without one.</returns>
    public ValueTask<ConnectResult> ConnectMultiplexedSessionAsync(ConnectTarget target, Func<IMultiplexedConnection, IConnection> openSession, CancellationToken cancellationToken) =>
        quicConnector?.ConnectMultiplexedSessionAsync(target, openSession, cancellationToken)
            ?? ValueTask.FromResult(ConnectResult.Failed(CurlExitCode.CouldntConnect, "QUIC through the WireGuard tunnel needs curl's connector"));

    /// <inheritdoc />
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.UseTls && tlsProvider is null)
        {
            return ConnectResult.Failed(CurlExitCode.NotBuiltIn, "TLS through the WireGuard tunnel is not built in");
        }

        return IPAddress.TryParse(target.Host.Trim('[', ']'), out IPAddress? destination)
            ? await ConnectToAsync(destination, target, cancellationToken).ConfigureAwait(false)
            : await ResolveAndConnectAsync(target, cancellationToken).ConfigureAwait(false);
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

    private static ConnectResult Unresolved(ConnectTarget target) =>
        ConnectResult.Failed(CurlExitCode.CouldntResolveHost, $"Could not resolve host: {target.Host}");

    private static IPAddress? AddressOrNull(string text) => IPAddress.TryParse(text, out IPAddress? address) ? address : null;

    private static async ValueTask<byte[]?> ReadExactlyAsync(IConnection connection, int count, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[count];
        for (int filled = 0; filled < count;)
        {
            int read = await connection.ReadAsync(buffer.AsMemory(filled), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            filled += read;
        }

        return buffer;
    }

    /// <summary>
    /// Resolves the target's host name by DNS over TCP to the configuration's first <c>DNS</c> address, through its
    /// own tunnel (ADR-1161), then connects to the first address of the answer; the system resolver is never asked.
    /// </summary>
    private async ValueTask<ConnectResult> ResolveAndConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        IPAddress? destination = await ResolveThroughTunnelAsync(target.Host, cancellationToken).ConfigureAwait(false);
        return destination is null ? Unresolved(target) : await ConnectToAsync(destination, target, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves <paramref name="host" /> by DNS over TCP to the configuration's first <c>DNS</c> address through its own
    /// tunnel (ADR-1161), giving the first address of the answer.
    /// </summary>
    /// <param name="host">The host name.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The address, or <see langword="null" /> when there is no DNS server to ask or no answer.</returns>
    internal async ValueTask<IPAddress?> ResolveThroughTunnelAsync(string host, CancellationToken cancellationToken)
    {
        IPAddress? server = configuration.Interface.DnsServers.Select(AddressOrNull).FirstOrDefault(address => address is not null);
        return server is null ? null : await ResolveAsync(server, host, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Asks <paramref name="server" /> for <paramref name="host" />'s A record, or AAAA when the interface has no IPv4 address.</summary>
    private async ValueTask<IPAddress?> ResolveAsync(IPAddress server, string host, CancellationToken cancellationToken)
    {
        ushort type = configuration.Interface.Addresses.Any(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
            ? WireGuardDnsMessage.AddressType
            : WireGuardDnsMessage.Ipv6AddressType;
        byte[] idBytes = new byte[2];
        random.GetBytes(idBytes);
        ushort id = BitConverter.ToUInt16(idBytes);
        byte[] query;
        try
        {
            query = WireGuardDnsMessage.EncodeQuery(id, host, type);
        }
        catch (ArgumentException)
        {
            return null;
        }

        ConnectResult opened = await ConnectToAsync(server, new ConnectTarget(server.ToString(), WireGuardDnsMessage.Port, false), cancellationToken).ConfigureAwait(false);
        return opened.Connection is { } connection
            ? await ExchangeAsync(connection, query, id, type, cancellationToken).ConfigureAwait(false)
            : null;
    }

    private static async ValueTask<IPAddress?> ExchangeAsync(IConnection connection, byte[] query, ushort id, ushort type, CancellationToken cancellationToken)
    {
        await using (connection.ConfigureAwait(false))
        {
            try
            {
                await connection.WriteAsync(query, cancellationToken).ConfigureAwait(false);
                await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
                byte[]? length = await ReadExactlyAsync(connection, 2, cancellationToken).ConfigureAwait(false);
                byte[]? response = length is null ? null : await ReadExactlyAsync(connection, (length[0] << 8) | length[1], cancellationToken).ConfigureAwait(false);
                return response is null ? null : WireGuardDnsMessage.AddressesIn(response, id, type).FirstOrDefault();
            }
            catch (IOException)
            {
                return null;
            }
        }
    }

    private async ValueTask<ConnectResult> ConnectToAsync(IPAddress destination, ConnectTarget target, CancellationToken cancellationToken)
    {
        WireGuardPeer? peer = WireGuardRoute.PeerFor(configuration.Peers, destination);
        IPAddress? source = WireGuardRoute.SourceFor(configuration.Interface.Addresses, destination);
        return peer is null || source is null
            ? ConnectResult.Failed(CurlExitCode.CouldntConnect, $"Failed to connect to {target.Host} port {target.Port}: no WireGuard peer's AllowedIPs and interface Address cover it")
            : await ConnectThroughAsync(peer, source, new IPEndPoint(destination, target.Port), target, cancellationToken).ConfigureAwait(false);
    }

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
