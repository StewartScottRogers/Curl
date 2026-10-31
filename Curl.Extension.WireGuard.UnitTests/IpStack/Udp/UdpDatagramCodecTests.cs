using System.Net;
using Curl.Extension.WireGuard.IpStack;
using Curl.Extension.WireGuard.IpStack.Udp;

namespace Curl.Extension.WireGuard.UnitTests.IpStack.Udp;

/// <summary>
/// Checks UDP datagrams against RFC 768. The recorded datagrams carry "hi" from port 50000 to 443; their checksums
/// were summed by hand over the pseudo-header (RFC 768, RFC 8200 section 8.1), header and data.
/// </summary>
[TestClass]
public sealed class UdpDatagramCodecTests
{
    /// <summary>10.0.0.2:50000 to 10.0.0.1:443, "hi": the sum 0x419D complemented is 0xBE62.</summary>
    internal static readonly byte[] RecordedIpv4Datagram = [0xC3, 0x50, 0x01, 0xBB, 0x00, 0x0A, 0xBE, 0x62, 0x68, 0x69];

    /// <summary>[fd00::2]:50000 to [fd00::1]:443, "hi": the sum 0x279F complemented is 0xD860.</summary>
    internal static readonly byte[] RecordedIpv6Datagram = [0xC3, 0x50, 0x01, 0xBB, 0x00, 0x0A, 0xD8, 0x60, 0x68, 0x69];

    internal static readonly IPEndPoint Client = new(IPAddress.Parse("10.0.0.2"), 50000);

    internal static readonly IPEndPoint Server = new(IPAddress.Parse("10.0.0.1"), 443);

    private static readonly IPEndPoint Ipv6Client = new(IPAddress.Parse("fd00::2"), 50000);

    private static readonly IPEndPoint Ipv6Server = new(IPAddress.Parse("fd00::1"), 443);

    [TestMethod]
    public void Build_Ipv4_GivesTheRecordedDatagram()
    {
        byte[] datagram = UdpDatagramCodec.Build(Client, Server, "hi"u8);

        CollectionAssert.AreEqual(RecordedIpv4Datagram, datagram);
    }

    [TestMethod]
    public void Build_Ipv6_GivesTheRecordedDatagram()
    {
        byte[] datagram = UdpDatagramCodec.Build(Ipv6Client, Ipv6Server, "hi"u8);

        CollectionAssert.AreEqual(RecordedIpv6Datagram, datagram);
    }

    [TestMethod]
    public void Build_ChecksumComputesToZero_SendsAllOnes()
    {
        // Without data the sum folds to 0xD933; the word 0x26CC brings it to 0xFFFF, whose complement is zero.
        byte[] datagram = UdpDatagramCodec.Build(Client, Server, [0x26, 0xCC]);

        Assert.AreEqual(0xFF, datagram[6]);
        Assert.AreEqual(0xFF, datagram[7]);
        Assert.IsTrue(UdpDatagramCodec.TryParse(new IpPacket(Client.Address, Server.Address, UdpDatagramCodec.UdpProtocol, datagram), out _));
    }

    [TestMethod]
    public void Build_NullEndPoint_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UdpDatagramCodec.Build(null!, Server, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => UdpDatagramCodec.Build(Client, null!, []));
    }

    [TestMethod]
    public void TryParse_RecordedIpv6Datagram_ReadsPortsAndData()
    {
        bool parsed = UdpDatagramCodec.TryParse(new IpPacket(Ipv6Client.Address, Ipv6Server.Address, UdpDatagramCodec.UdpProtocol, RecordedIpv6Datagram), out UdpDatagram? datagram);

        Assert.IsTrue(parsed);
        Assert.AreEqual((ushort)50000, datagram!.SourcePort);
        Assert.AreEqual((ushort)443, datagram.DestinationPort);
        CollectionAssert.AreEqual("hi"u8.ToArray(), datagram.Payload);
    }

    [TestMethod]
    public void TryParse_BytesBeyondTheLengthField_AreNotData()
    {
        byte[] padded = [.. RecordedIpv4Datagram, 0, 0];

        bool parsed = UdpDatagramCodec.TryParse(new IpPacket(Client.Address, Server.Address, UdpDatagramCodec.UdpProtocol, padded), out UdpDatagram? datagram);

        Assert.IsTrue(parsed);
        CollectionAssert.AreEqual("hi"u8.ToArray(), datagram!.Payload);
    }

    [TestMethod]
    public void TryParse_ZeroChecksumOverIpv4_IsAcceptedAsNoneComputed()
    {
        byte[] unchecked_ = [0xC3, 0x50, 0x01, 0xBB, 0x00, 0x0A, 0x00, 0x00, 0x68, 0x69];

        Assert.IsTrue(UdpDatagramCodec.TryParse(new IpPacket(Client.Address, Server.Address, UdpDatagramCodec.UdpProtocol, unchecked_), out _));
    }

    [TestMethod]
    public void TryParse_ZeroChecksumOverIpv6_IsRefused()
    {
        byte[] unchecked_ = [0xC3, 0x50, 0x01, 0xBB, 0x00, 0x0A, 0x00, 0x00, 0x68, 0x69];

        Assert.IsFalse(UdpDatagramCodec.TryParse(new IpPacket(Ipv6Client.Address, Ipv6Server.Address, UdpDatagramCodec.UdpProtocol, unchecked_), out UdpDatagram? datagram));
        Assert.IsNull(datagram);
    }

    [TestMethod]
    [DataRow(new byte[] { 0xC3, 0x50, 0x01, 0xBB, 0x00, 0x0A, 0xBE, 0x63, 0x68, 0x69 }, UdpDatagramCodec.UdpProtocol, DisplayName = "a wrong checksum")]
    [DataRow(new byte[] { 0xC3, 0x50, 0x01, 0xBB, 0x00, 0x0A, 0xBE, 0x62, 0x68, 0x69 }, IpPacketCodec.TcpProtocol, DisplayName = "not UDP")]
    [DataRow(new byte[] { 0xC3, 0x50, 0x01, 0xBB, 0x00, 0x0A, 0xBE }, UdpDatagramCodec.UdpProtocol, DisplayName = "shorter than the header")]
    [DataRow(new byte[] { 0xC3, 0x50, 0x01, 0xBB, 0x00, 0x0B, 0xBE, 0x62, 0x68, 0x69 }, UdpDatagramCodec.UdpProtocol, DisplayName = "a length past the packet")]
    [DataRow(new byte[] { 0xC3, 0x50, 0x01, 0xBB, 0x00, 0x07, 0xBE, 0x62, 0x68, 0x69 }, UdpDatagramCodec.UdpProtocol, DisplayName = "a length inside the header")]
    public void TryParse_UnsoundDatagram_IsRefused(byte[] bytes, byte protocol)
    {
        Assert.IsFalse(UdpDatagramCodec.TryParse(new IpPacket(Client.Address, Server.Address, protocol, bytes), out _));
    }

    [TestMethod]
    public void TryParse_NullPacket_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UdpDatagramCodec.TryParse(null!, out _));
    }
}
