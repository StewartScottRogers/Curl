using System.Net;
using Curl.Extension.WireGuard.IpStack;
using Curl.Extension.WireGuard.IpStack.Udp;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WireGuard.UnitTests.IpStack.Udp;

/// <summary>Checks the user-space UDP socket over a scripted inside of the tunnel.</summary>
[TestClass]
public sealed class UserSpaceUdpChannelTests
{
    private static readonly IPEndPoint Client = UdpDatagramCodecTests.Client;

    private static readonly IPEndPoint Server = UdpDatagramCodecTests.Server;

    [TestMethod]
    public async Task SendAsync_Datagram_GoesAsTheRecordedUdpDatagramInAnIpv4Packet()
    {
        ScriptedPacketChannel packets = new();
        UserSpaceUdpChannel channel = new(packets, Client, Server, new RecordingOwner());

        await channel.SendAsync("hi"u8.ToArray(), channel.ServerEndPoint, CancellationToken.None);
        Assert.IsTrue(IpPacketCodec.TryParse(await packets.NextSentAsync(), out IpPacket? packet));

        Assert.AreEqual(Client.Address, packet!.Source);
        Assert.AreEqual(Server.Address, packet.Destination);
        Assert.AreEqual(UdpDatagramCodec.UdpProtocol, packet.Protocol);
        CollectionAssert.AreEqual(UdpDatagramCodecTests.RecordedIpv4Datagram, packet.Payload);
        Assert.AreEqual(Client, channel.LocalEndPoint);
    }

    [TestMethod]
    public async Task SendAsync_DestinationOfAnotherFamilyOrKind_Throws()
    {
        UserSpaceUdpChannel channel = new(new ScriptedPacketChannel(), Client, Server, new RecordingOwner());

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await channel.SendAsync("hi"u8.ToArray(), new IPEndPoint(IPAddress.Parse("fd00::1"), 443), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await channel.SendAsync("hi"u8.ToArray(), new DnsEndPoint("example.com", 443), CancellationToken.None));
    }

    [TestMethod]
    public async Task ReceiveAsync_PacketsNotForThisSocket_AreDroppedUntilOneIs()
    {
        ScriptedPacketChannel packets = new();
        UserSpaceUdpChannel channel = new(packets, Client, Server, new RecordingOwner());
        packets.Deliver([0x00]);
        packets.Deliver(Packet(IPAddress.Parse("10.0.0.9"), UdpDatagramCodec.Build(Server, new IPEndPoint(IPAddress.Parse("10.0.0.9"), Client.Port), "no"u8)));
        packets.Deliver(IpPacketCodec.Build(new IpPacket(Server.Address, Client.Address, IpPacketCodec.TcpProtocol, new byte[20])));
        packets.Deliver(Packet(Client.Address, UdpDatagramCodec.Build(Server, new IPEndPoint(Client.Address, 50001), "no"u8)));
        packets.Deliver(Packet(Client.Address, UdpDatagramCodec.Build(Server, Client, "yes"u8)));
        byte[] buffer = new byte[16];

        DatagramReceived received = await channel.ReceiveAsync(buffer, CancellationToken.None);

        Assert.AreEqual(3, received.Length);
        Assert.AreEqual("yes", System.Text.Encoding.ASCII.GetString(buffer, 0, received.Length));
        Assert.AreEqual(Server, received.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ReceiveAsync_DatagramLargerThanTheBuffer_IsTruncated()
    {
        ScriptedPacketChannel packets = new();
        UserSpaceUdpChannel channel = new(packets, Client, Server, new RecordingOwner());
        packets.Deliver(Packet(Client.Address, UdpDatagramCodec.Build(Server, Client, "hello"u8)));
        byte[] buffer = new byte[2];

        DatagramReceived received = await channel.ReceiveAsync(buffer, CancellationToken.None);

        Assert.AreEqual(2, received.Length);
        CollectionAssert.AreEqual("he"u8.ToArray(), buffer);
    }

    [TestMethod]
    public async Task DisposeAsync_ClosesTheTunnel()
    {
        RecordingOwner owner = new();
        UserSpaceUdpChannel channel = new(new ScriptedPacketChannel(), Client, Server, owner);

        await channel.DisposeAsync();

        Assert.IsTrue(owner.IsDisposed);
    }

    private static byte[] Packet(IPAddress destination, byte[] datagram) =>
        IpPacketCodec.Build(new IpPacket(Server.Address, destination, UdpDatagramCodec.UdpProtocol, datagram));

    private sealed class RecordingOwner : IAsyncDisposable
    {
        public bool IsDisposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
