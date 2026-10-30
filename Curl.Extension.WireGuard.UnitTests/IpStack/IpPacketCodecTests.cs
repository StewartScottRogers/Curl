using System.Net;
using Curl.Extension.WireGuard.IpStack;

namespace Curl.Extension.WireGuard.UnitTests.IpStack;

[TestClass]
public sealed class IpPacketCodecTests
{
    private static readonly IPAddress V4Source = IPAddress.Parse("10.0.0.2");
    private static readonly IPAddress V4Destination = IPAddress.Parse("10.0.0.1");
    private static readonly IPAddress V6Source = IPAddress.Parse("fd00::2");
    private static readonly IPAddress V6Destination = IPAddress.Parse("fd00::1");

    [TestMethod]
    public void Build_IPv4_WritesAnUnfragmentedHeaderWithACorrectChecksum()
    {
        var bytes = IpPacketCodec.Build(new IpPacket(V4Source, V4Destination, 6, [1, 2, 3]));

        Assert.HasCount(23, bytes);
        CollectionAssert.AreEqual(Convert.FromHexString("4500001700004000400600000A0000020A000001010203")[..10], bytes[..10]);
        Assert.AreEqual((ushort)0, InternetChecksum.Compute(bytes.AsSpan(0, 20)));
        CollectionAssert.AreEqual(Convert.FromHexString("0A0000020A000001010203"), bytes[12..]);
    }

    [TestMethod]
    public void Build_IPv6_WritesTheFixedHeader()
    {
        var bytes = IpPacketCodec.Build(new IpPacket(V6Source, V6Destination, 6, [9]));

        CollectionAssert.AreEqual(Convert.FromHexString("6000000000010640"), bytes[..8]);
        CollectionAssert.AreEqual(V6Source.GetAddressBytes(), bytes[8..24]);
        CollectionAssert.AreEqual(V6Destination.GetAddressBytes(), bytes[24..40]);
        Assert.AreEqual((byte)9, bytes[40]);
    }

    [TestMethod]
    [DataRow("10.0.0.2", "10.0.0.1")]
    [DataRow("fd00::2", "fd00::1")]
    public void TryParse_BuiltPacket_GivesItBack(string source, string destination)
    {
        IpPacket packet = new(IPAddress.Parse(source), IPAddress.Parse(destination), 6, [4, 5, 6]);

        var parsed = IpPacketCodec.TryParse(IpPacketCodec.Build(packet), out var read);

        Assert.IsTrue(parsed);
        Assert.AreEqual(packet.Source, read!.Source);
        Assert.AreEqual(packet.Destination, read.Destination);
        Assert.AreEqual((byte)6, read.Protocol);
        CollectionAssert.AreEqual(packet.Payload, read.Payload);
    }

    [TestMethod]
    public void TryParse_IPv4WithPaddingPastTotalLength_StopsAtTotalLength()
    {
        var bytes = IpPacketCodec.Build(new IpPacket(V4Source, V4Destination, 6, [7])).Concat(new byte[] { 0, 0 }).ToArray();

        IpPacketCodec.TryParse(bytes, out var read);

        CollectionAssert.AreEqual(new byte[] { 7 }, read!.Payload);
    }

    [TestMethod]
    [DataRow("empty")]
    [DataRow("version 5")]
    [DataRow("IPv4 shorter than a header")]
    [DataRow("IPv4 header length under 20")]
    [DataRow("IPv4 total length under the header")]
    [DataRow("IPv4 total length past the bytes")]
    [DataRow("IPv4 more fragments")]
    [DataRow("IPv4 fragment offset")]
    [DataRow("IPv4 wrong checksum")]
    [DataRow("IPv6 shorter than a header")]
    [DataRow("IPv6 payload length past the bytes")]
    public void TryParse_Malformed_Refuses(string kind)
    {
        var bytes = Malformed(kind);

        var parsed = IpPacketCodec.TryParse(bytes, out var packet);

        Assert.IsFalse(parsed);
        Assert.IsNull(packet);
    }

    [TestMethod]
    public void Build_MixedFamilies_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => IpPacketCodec.Build(new IpPacket(V4Source, V6Destination, 6, [])));
    }

    [TestMethod]
    public void Build_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => IpPacketCodec.Build(null!));
    }

    [TestMethod]
    public void PseudoHeaderSum_NullAddress_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => IpPacketCodec.PseudoHeaderSum(null!, V4Destination, 6, 0));
        Assert.ThrowsExactly<ArgumentNullException>(() => IpPacketCodec.PseudoHeaderSum(V4Source, null!, 6, 0));
    }

    [TestMethod]
    public void PseudoHeaderSum_IPv6LengthOver16Bits_AddsBothHalves()
    {
        var sum = IpPacketCodec.PseudoHeaderSum(IPAddress.IPv6Any, IPAddress.IPv6Any, 6, 0x12345);

        Assert.AreEqual(6u + 0x1u + 0x2345u, sum);
    }

    private static byte[] Malformed(string kind)
    {
        var v4 = IpPacketCodec.Build(new IpPacket(V4Source, V4Destination, 6, [1, 2, 3, 4]));
        var v6 = IpPacketCodec.Build(new IpPacket(V6Source, V6Destination, 6, [1, 2, 3, 4]));
        switch (kind)
        {
            case "empty":
                return [];
            case "version 5":
                v4[0] = 0x55;
                return v4;
            case "IPv4 shorter than a header":
                return v4[..19];
            case "IPv4 header length under 20":
                return Rechecksummed(v4, 0, 0x44);
            case "IPv4 total length under the header":
                return Rechecksummed(v4, 3, 19);
            case "IPv4 total length past the bytes":
                return Rechecksummed(v4, 3, 25);
            case "IPv4 more fragments":
                return Rechecksummed(v4, 6, 0x20);
            case "IPv4 fragment offset":
                return Rechecksummed(v4, 7, 0x01);
            case "IPv4 wrong checksum":
                v4[10] ^= 0xFF;
                return v4;
            case "IPv6 shorter than a header":
                return v6[..39];
            default:
                v6[5] = 5;
                return v6;
        }
    }

    private static byte[] Rechecksummed(byte[] packet, int index, byte value)
    {
        packet[index] = value;
        packet[10] = 0;
        packet[11] = 0;
        var checksum = InternetChecksum.Compute(packet.AsSpan(0, 20));
        packet[10] = (byte)(checksum >> 8);
        packet[11] = (byte)checksum;
        return packet;
    }
}
