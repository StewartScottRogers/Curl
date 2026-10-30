using System.Net;
using Curl.Extension.Masque.Wire;

namespace Curl.Extension.Masque.Tunnel;

/// <summary>Checks that a CONNECT-IP tunnel carries UDP in IP packets from its assigned address (RFC 9484 sections 4.7 and 6).</summary>
[TestClass]
public sealed class ConnectIpChannelTests
{
    private static readonly IPAddress Target = IPAddress.Parse("192.0.2.7");

    private static readonly IPAddress Assigned = IPAddress.Parse("198.51.100.5");

    [TestMethod]
    public async Task OpenAsync_ConnectIp_RequestsAnyAddressAndUsesTheOneAssigned()
    {
        var (channel, connection) = await OpenAsync();

        var request = ConnectIpCapsuleCodec.EncodeAddressRequest([new ConnectIpAddress(1, IPAddress.Any, 32)]);
        var requestBytes = MasqueProxyBytes.Capsule(request.Type, request.Value.ToArray());
        CollectionAssert.AreEqual(requestBytes, connection.Written[^requestBytes.Length..]);
        var local = (IPEndPoint)channel.LocalEndPoint!;
        Assert.AreEqual(Assigned, local.Address);
        Assert.IsGreaterThanOrEqualTo(49152, local.Port);
        Assert.AreEqual(new IPEndPoint(Target, 69), channel.ServerEndPoint);
    }

    [TestMethod]
    public async Task OpenAsync_IPv6Target_RequestsAndUsesAnIPv6Address()
    {
        var assigned = IPAddress.Parse("2001:db8:1::5");
        var assign = ConnectIpCapsuleCodec.EncodeAddressAssign([new ConnectIpAddress(1, assigned, 128)]);

        var (channel, connection) = await MasqueProxyBytes.OpenAsync(
            MasqueTunnelKind.ConnectIp, "[2001:db8::7]", 69, [.. MasqueProxyBytes.SwitchingProtocols("connect-ip"), .. MasqueProxyBytes.Capsule(assign.Type, assign.Value.ToArray())]);

        var request = ConnectIpCapsuleCodec.EncodeAddressRequest([new ConnectIpAddress(1, IPAddress.IPv6Any, 128)]);
        var requestBytes = MasqueProxyBytes.Capsule(request.Type, request.Value.ToArray());
        CollectionAssert.AreEqual(requestBytes, connection.Written[^requestBytes.Length..]);
        Assert.AreEqual(assigned, ((IPEndPoint)channel.LocalEndPoint!).Address);
    }

    [TestMethod]
    public async Task SendAsync_Datagram_WritesAnIpUdpPacketFromTheAssignedAddressToTheTarget()
    {
        var (channel, connection) = await OpenAsync();
        var sentBefore = connection.Written.Length;
        var destination = new IPEndPoint(Target, 1234);

        await channel.SendAsync(new byte[] { 9, 8 }, destination, CancellationToken.None);

        var packet = IpUdpPacket.Build(new IpUdpDatagram((IPEndPoint)channel.LocalEndPoint!, destination, new byte[] { 9, 8 }));
        CollectionAssert.AreEqual(MasqueProxyBytes.Datagram(packet), connection.Written[sentBefore..]);
    }

    [TestMethod]
    public async Task SendAsync_HostNameDestination_ThrowsArgumentException()
    {
        var (channel, _) = await OpenAsync();

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await channel.SendAsync(new byte[] { 1 }, new DnsEndPoint("tftp.example", 69), CancellationToken.None));
    }

    [TestMethod]
    public async Task ReceiveAsync_PacketsNotFromTheTargetToTheChannel_AreDroppedAndTheTargetsPayloadIsHandedBack()
    {
        var (channel, connection) = await OpenAsync();
        var local = (IPEndPoint)channel.LocalEndPoint!;
        var fromTarget = new IPEndPoint(Target, 3333);
        connection.Enqueue(
            MasqueProxyBytes.Datagram([0x00]),
            MasqueProxyBytes.Datagram(IpUdpPacket.Build(new IpUdpDatagram(new IPEndPoint(IPAddress.Parse("203.0.113.9"), 3333), local, new byte[] { 1 }))),
            MasqueProxyBytes.Datagram(IpUdpPacket.Build(new IpUdpDatagram(fromTarget, new IPEndPoint(Assigned, local.Port ^ 1), new byte[] { 2 }))),
            MasqueProxyBytes.Datagram(IpUdpPacket.Build(new IpUdpDatagram(fromTarget, local, new byte[] { 3, 4, 5 }))));
        var buffer = new byte[2];

        var received = await channel.ReceiveAsync(buffer, CancellationToken.None);

        Assert.AreEqual(2, received.Length);
        CollectionAssert.AreEqual(new byte[] { 3, 4 }, buffer);
        Assert.AreEqual(fromTarget, received.RemoteEndPoint);
    }

    [TestMethod]
    public async Task DisposeAsync_Channel_DisposesTheProxyConnection()
    {
        var (channel, connection) = await OpenAsync();

        await channel.DisposeAsync();

        Assert.IsTrue(connection.IsDisposed);
    }

    // Opens a tunnel whose proxy first advertises a route and assigns only an IPv6 address, both of which an IPv4 target passes over.
    private static Task<(Curl.Protocol.Abstractions.IDatagramChannel Channel, ScriptedProxyConnection Connection)> OpenAsync()
    {
        var route = ConnectIpCapsuleCodec.EncodeRouteAdvertisement([new ConnectIpRoute(Target, Target, 17)]);
        var ipv6Only = ConnectIpCapsuleCodec.EncodeAddressAssign([new ConnectIpAddress(1, IPAddress.Parse("2001:db8::5"), 128)]);
        var assign = ConnectIpCapsuleCodec.EncodeAddressAssign([new ConnectIpAddress(1, Assigned, 32)]);
        return MasqueProxyBytes.OpenAsync(
            MasqueTunnelKind.ConnectIp,
            "192.0.2.7",
            69,
            MasqueProxyBytes.SwitchingProtocols("connect-ip"),
            MasqueProxyBytes.Capsule(route.Type, route.Value.ToArray()),
            MasqueProxyBytes.Capsule(ipv6Only.Type, ipv6Only.Value.ToArray()),
            MasqueProxyBytes.Capsule(assign.Type, assign.Value.ToArray()));
    }
}
