using System.Buffers.Binary;
using System.Net;
using Curl.Extension.Masque.Wire;

namespace Curl.Extension.Masque;

/// <summary>Checks the IP packets of RFC 9484 section 6 against RFC 768, RFC 791 and RFC 8200.</summary>
[TestClass]
public sealed class IpUdpPacketTests
{
    private static readonly IPEndPoint Client4 = new(IPAddress.Parse("192.0.2.10"), 50000);
    private static readonly IPEndPoint Server4 = new(IPAddress.Parse("198.51.100.7"), 53);
    private static readonly IPEndPoint Client6 = new(IPAddress.Parse("2001:db8::10"), 50000);
    private static readonly IPEndPoint Server6 = new(IPAddress.Parse("2001:db8:1::7"), 443);

    [TestMethod]
    public void Build_IPv4_WritesTheHeaderAndBothChecksums()
    {
        var packet = IpUdpPacket.Build(new(Client4, Server4, new byte[] { 1, 2, 3 }));

        Assert.HasCount(31, packet);
        CollectionAssert.AreEqual(new byte[] { 0x45, 0, 0, 31, 0, 0, 0x40, 0, 64, 17 }, packet[..10]);
        CollectionAssert.AreEqual(new byte[] { 192, 0, 2, 10, 198, 51, 100, 7 }, packet[12..20]);
        CollectionAssert.AreEqual(new byte[] { 0xc3, 0x50, 0, 53, 0, 11 }, packet[20..26]);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, packet[28..]);
        Assert.AreEqual(0xFFFF, OnesComplementSum(packet[..20]), "header checksum");
        Assert.AreEqual(0xFFFF, OnesComplementSum([.. packet[12..20], 0, 17, 0, 11, .. packet[20..]]), "UDP checksum");
    }

    [TestMethod]
    public void Build_IPv6_WritesTheHeaderAndTheUdpChecksum()
    {
        var packet = IpUdpPacket.Build(new(Client6, Server6, new byte[] { 9, 8, 7, 6 }));

        Assert.HasCount(52, packet);
        CollectionAssert.AreEqual(new byte[] { 0x60, 0, 0, 0, 0, 12, 17, 64 }, packet[..8]);
        CollectionAssert.AreEqual(Client6.Address.GetAddressBytes(), packet[8..24]);
        CollectionAssert.AreEqual(Server6.Address.GetAddressBytes(), packet[24..40]);
        CollectionAssert.AreEqual(new byte[] { 0xc3, 0x50, 0x01, 0xbb, 0, 12 }, packet[40..46]);
        Assert.AreEqual(0xFFFF, OnesComplementSum([.. packet[8..40], 0, 0, 0, 12, 0, 0, 0, 17, .. packet[40..]]), "UDP checksum");
    }

    [TestMethod]
    public void Build_ChecksumComputingToZero_SendsAllOnes()
    {
        var found = Enumerable.Range(0, 0x10000)
            .Select(word => IpUdpPacket.Build(new(Client4, Server4, new byte[] { (byte)(word >> 8), (byte)word })))
            .FirstOrDefault(packet => BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(26)) == 0xFFFF);

        Assert.IsNotNull(found);
        Assert.AreEqual(0xFFFF, OnesComplementSum([.. found[12..20], 0, 17, 0, 10, .. found[20..]]));
    }

    [TestMethod]
    public void Build_Refuses_MixedFamiliesOversizePayloadsAndNull()
    {
        Assert.ThrowsExactly<ArgumentException>(() => IpUdpPacket.Build(new(Client4, Server6, Array.Empty<byte>())));
        Assert.ThrowsExactly<ArgumentException>(() => IpUdpPacket.Build(new(Client4, Server4, new byte[65508])));
        Assert.ThrowsExactly<ArgumentException>(() => IpUdpPacket.Build(new(Client6, Server6, new byte[65528])));
        Assert.HasCount(65535, IpUdpPacket.Build(new(Client4, Server4, new byte[65507])));
        Assert.ThrowsExactly<ArgumentNullException>(() => IpUdpPacket.Build(null!));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TryRead_ABuiltPacket_GivesBackTheDatagram(bool isIPv6)
    {
        var (source, destination) = isIPv6 ? (Client6, Server6) : (Client4, Server4);
        byte[] payload = [5, 4, 3, 2, 1];

        Assert.IsTrue(IpUdpPacket.TryRead(IpUdpPacket.Build(new(source, destination, payload)), out var datagram));

        Assert.AreEqual(source, datagram!.Source);
        Assert.AreEqual(destination, datagram.Destination);
        CollectionAssert.AreEqual(payload, datagram.Payload.ToArray());
    }

    [TestMethod]
    public void TryRead_IPv4WithTrailingBytesAndOptions_ReadsOnlyThePacket()
    {
        var built = IpUdpPacket.Build(new(Client4, Server4, new byte[] { 7 }));
        byte[] packet = [0x46, .. built[1..20], 1, 1, 1, 1, .. built[20..], 0xEE];
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), (ushort)(built.Length + 4));

        Assert.IsTrue(IpUdpPacket.TryRead(packet, out var datagram));
        CollectionAssert.AreEqual(new byte[] { 7 }, datagram!.Payload.ToArray());
    }

    [TestMethod]
    [DataRow("empty")]
    [DataRow("version 5")]
    [DataRow("short IPv4")]
    [DataRow("IHL 4")]
    [DataRow("total below header")]
    [DataRow("total past end")]
    [DataRow("TCP")]
    [DataRow("fragment")]
    [DataRow("later fragment")]
    [DataRow("short IPv6")]
    [DataRow("IPv6 payload past end")]
    [DataRow("IPv6 extension header")]
    [DataRow("short UDP")]
    [DataRow("UDP length 4")]
    [DataRow("UDP length past end")]
    public void TryRead_PacketNotCarryingAWholeUdpDatagram_IsIgnored(string damage)
    {
        var packet = Damaged(damage);

        Assert.IsFalse(IpUdpPacket.TryRead(packet, out var datagram));
        Assert.IsNull(datagram);
    }

    private static byte[] Damaged(string damage)
    {
        var v4 = IpUdpPacket.Build(new(Client4, Server4, new byte[] { 1, 2 }));
        var v6 = IpUdpPacket.Build(new(Client6, Server6, new byte[] { 1, 2 }));
        return damage switch
        {
            "empty" => [],
            "version 5" => [0x55, .. v4[1..]],
            "short IPv4" => v4[..19],
            "IHL 4" => [0x44, .. v4[1..]],
            "total below header" => [.. v4[..2], 0, 19, .. v4[4..]],
            "total past end" => [.. v4[..2], 0, 31, .. v4[4..]],
            "TCP" => [.. v4[..9], 6, .. v4[10..]],
            "fragment" => [.. v4[..6], 0x20, .. v4[7..]],
            "later fragment" => [.. v4[..6], 0x40, 1, .. v4[8..]],
            "short IPv6" => v6[..39],
            "IPv6 payload past end" => [.. v6[..4], 0, 11, .. v6[6..]],
            "IPv6 extension header" => [.. v6[..6], 0, .. v6[7..]],
            "short UDP" => [.. v4[..2], 0, 27, .. v4[4..27]],
            "UDP length 4" => [.. v4[..24], 0, 4, .. v4[26..]],
            _ => [.. v4[..24], 0, 11, .. v4[26..]],
        };
    }

    // The folded RFC 1071 sum; a packet whose checksum is right sums to 0xFFFF.
    private static int OnesComplementSum(byte[] bytes)
    {
        var sum = 0;
        for (var index = 0; index < bytes.Length; index += 2)
        {
            sum += (bytes[index] << 8) | (index + 1 < bytes.Length ? bytes[index + 1] : 0);
        }

        while (sum > 0xFFFF)
        {
            sum = (sum & 0xFFFF) + (sum >> 16);
        }

        return sum;
    }
}
