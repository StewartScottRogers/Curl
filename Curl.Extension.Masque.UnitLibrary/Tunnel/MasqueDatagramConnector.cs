using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Curl.Extension.Masque.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Masque.Tunnel;

/// <summary>
/// Opens datagram channels through a MASQUE proxy: connects to the proxy the URI template
/// names through curl's <see cref="IConnector" /> (TLS for an <c>https</c> template), sends
/// the upgrade request and, on <c>101</c>, hands back a CONNECT-UDP (RFC 9298) or
/// CONNECT-IP (RFC 9484) channel to the target.
/// </summary>
/// <remarks>
/// A proxy that cannot be reached fails with the connector's own exit code and message. A
/// refused upgrade, a proxy that closes the stream or sends what the RFCs do not allow
/// before the tunnel is open, and a write that fails fail with exit 7. A CONNECT-IP target
/// must be an IP address, since the packets carry it; a host name fails with exit 6.
/// </remarks>
/// <param name="connector">curl's connector, which reaches the proxy.</param>
/// <param name="uriTemplate">The proxy's URI template.</param>
/// <param name="kind">Which tunnel to open.</param>
internal sealed class MasqueDatagramConnector(IConnector connector, string uriTemplate, MasqueTunnelKind kind) : IDatagramConnector
{
    /// <summary>The request ID of the one <c>ADDRESS_REQUEST</c> a CONNECT-IP tunnel sends.</summary>
    public const ulong AddressRequestId = 1;

    /// <inheritdoc />
    public async ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            return await OpenTunnelAsync(host.Trim('[', ']'), port, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is MasqueException or IOException or SocketException)
        {
            return DatagramOpenResult.Failed(CurlExitCode.CouldntConnect, exception.Message);
        }
    }

    private async ValueTask<DatagramOpenResult> OpenTunnelAsync(string host, int port, CancellationToken cancellationToken)
    {
        var hasAddress = IPAddress.TryParse(host, out var address);
        if (kind == MasqueTunnelKind.ConnectIp && !hasAddress)
        {
            return DatagramOpenResult.Failed(CurlExitCode.CouldntResolveHost, $"MASQUE CONNECT-IP needs an IP address target, not the host name {host}");
        }

        var proxyUri = ExpandTemplate(host, port, address);
        var connected = await connector.ConnectAsync(new ConnectTarget(proxyUri.DnsSafeHost, proxyUri.Port, proxyUri.Scheme == Uri.UriSchemeHttps), cancellationToken).ConfigureAwait(false);
        if (connected.Connection is null)
        {
            return DatagramOpenResult.Failed(connected.ExitCode, connected.ErrorMessage!);
        }

        var stream = new MasqueTunnelStream(connected.Connection);
        var (channel, failure) = await TryOpenChannelAsync(stream, proxyUri, host, port, address, cancellationToken).ConfigureAwait(false);
        if (channel is null)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw failure!;
        }

        return DatagramOpenResult.Opened(channel);
    }

    // The channel past the upgrade, or what was thrown on the way, so the caller closes the connection before it escapes.
    private async ValueTask<(IDatagramChannel? Channel, Exception? Failure)> TryOpenChannelAsync(
        MasqueTunnelStream stream, Uri proxyUri, string host, int port, IPAddress? address, CancellationToken cancellationToken)
    {
        try
        {
            await UpgradeAsync(stream, proxyUri, cancellationToken).ConfigureAwait(false);
            return (await OpenChannelAsync(stream, host, port, address, cancellationToken).ConfigureAwait(false), null);
        }
        catch (Exception exception)
        {
            return (null, exception);
        }
    }

    // The URI the upgrade goes to; a CONNECT-IP tunnel asks for UDP to the target's address only.
    private Uri ExpandTemplate(string host, int port, IPAddress? address) =>
        kind == MasqueTunnelKind.ConnectUdp
            ? MasqueUriTemplate.ExpandConnectUdp(uriTemplate, host, port)
            : MasqueUriTemplate.ExpandConnectIp(uriTemplate, address!.ToString(), IpUdpPacket.UdpProtocol.ToString(CultureInfo.InvariantCulture));

    private async ValueTask<IDatagramChannel> OpenChannelAsync(MasqueTunnelStream stream, string host, int port, IPAddress? address, CancellationToken cancellationToken)
    {
        if (kind == MasqueTunnelKind.ConnectIp)
        {
            return await OpenConnectIpChannelAsync(stream, new IPEndPoint(address!, port), cancellationToken).ConfigureAwait(false);
        }

        return new ConnectUdpChannel(stream, address is null ? new DnsEndPoint(host, port) : new IPEndPoint(address, port));
    }

    private async ValueTask UpgradeAsync(MasqueTunnelStream stream, Uri proxyUri, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(MasqueUpgrade.BuildRequest(proxyUri, kind), cancellationToken).ConfigureAwait(false);
        var head = await stream.ReadHeadAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new MasqueException($"MASQUE proxy closed the connection before answering the {MasqueUpgrade.UpgradeToken(kind)} upgrade");
        MasqueUpgrade.CheckReply(head, kind);
    }

    // Asks for any one address of the target's family (RFC 9484 section 4.7.2) and waits for the proxy to assign one.
    private static async ValueTask<ConnectIpChannel> OpenConnectIpChannelAsync(MasqueTunnelStream stream, IPEndPoint target, CancellationToken cancellationToken)
    {
        var isIPv6 = target.AddressFamily == AddressFamily.InterNetworkV6;
        var any = new ConnectIpAddress(AddressRequestId, isIPv6 ? IPAddress.IPv6Any : IPAddress.Any, (byte)(isIPv6 ? 128 : 32));
        await stream.WriteCapsuleAsync(ConnectIpCapsuleCodec.EncodeAddressRequest([any]), cancellationToken).ConfigureAwait(false);
        var assigned = await ReadAssignedAddressAsync(stream, target.AddressFamily, cancellationToken).ConfigureAwait(false);
        return new ConnectIpChannel(stream, target, new IPEndPoint(assigned, Random.Shared.Next(49152, 65536)));
    }

    // The first address of the family an ADDRESS_ASSIGN gives; every other capsule before it is dropped.
    private static async ValueTask<IPAddress> ReadAssignedAddressAsync(MasqueTunnelStream stream, AddressFamily family, CancellationToken cancellationToken)
    {
        while (true)
        {
            var capsule = await stream.ReadCapsuleAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new MasqueException("MASQUE proxy closed the connect-ip tunnel before assigning an address");
            var assigned = capsule.Type == ConnectIpCapsuleCodec.AddressAssignType
                ? ConnectIpCapsuleCodec.DecodeAddressAssign(capsule).FirstOrDefault(entry => entry.Address.AddressFamily == family)
                : null;
            if (assigned is not null)
            {
                return assigned.Address;
            }
        }
    }
}
