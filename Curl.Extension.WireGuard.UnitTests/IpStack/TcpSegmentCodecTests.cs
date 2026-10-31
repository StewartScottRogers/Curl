using System.Net;
using Curl.Extension.WireGuard.IpStack;

namespace Curl.Extension.WireGuard.UnitTests.IpStack;

[TestClass]
public sealed class TcpSegmentCodecTests
{
    private static readonly IPAddress Source = IPAddress.Parse("10.0.0.2");
    private static readonly IPAddress Destination = IPAddress.Parse("10.0.0.1");

    [TestMethod]
    [DataRow("10.0.0.2", "10.0.0.1")]
    [DataRow("fd00::2", "fd00::1")]
    public void TryParse_BuiltSegment_GivesItBack(string source, string destination)
    {
        TcpSegment segment = new(50000, 80, 0xFFFFFFF0, 7, TcpFlags.Syn | TcpFlags.Ack, 1024, 1360, [1, 2, 3]);
        var bytes = TcpSegmentCodec.Build(segment, IPAddress.Parse(source), IPAddress.Parse(destination));

        var parsed = TcpSegmentCodec.TryParse(bytes, IPAddress.Parse(source), IPAddress.Parse(destination), out var read);

        Assert.IsTrue(parsed);
        Assert.AreEqual(segment with { Payload = read!.Payload }, read);
        CollectionAssert.AreEqual(segment.Payload, read.Payload);
    }

    [TestMethod]
    public void Build_MaximumSegmentSize_WritesTheOptionAndDataOffsetSix()
    {
        var bytes = TcpSegmentCodec.Build(new TcpSegment(1, 2, 3, 4, TcpFlags.Syn, 5, 0x0550, []), Source, Destination);

        Assert.HasCount(24, bytes);
        Assert.AreEqual((byte)0x60, bytes[12]);
        CollectionAssert.AreEqual(new byte[] { 2, 4, 0x05, 0x50 }, bytes[20..]);
    }

    [TestMethod]
    public void Build_NoOptions_WritesDataOffsetFiveAndThePseudoHeaderChecksum()
    {
        var bytes = TcpSegmentCodec.Build(new TcpSegment(1, 2, 3, 4, TcpFlags.Ack, 5, null, [0xAB]), Source, Destination);

        Assert.AreEqual((byte)0x50, bytes[12]);
        var sum = IpPacketCodec.PseudoHeaderSum(Source, Destination, IpPacketCodec.TcpProtocol, bytes.Length);
        Assert.AreEqual((ushort)0, InternetChecksum.Finish(InternetChecksum.Add(sum, bytes)));
    }

    [TestMethod]
    public void Build_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => TcpSegmentCodec.Build(null!, Source, Destination));
    }

    [TestMethod]
    [DataRow("shorter than a header")]
    [DataRow("data offset under 20")]
    [DataRow("data offset past the bytes")]
    [DataRow("wrong checksum")]
    [DataRow("other addresses")]
    public void TryParse_Malformed_Refuses(string kind)
    {
        var bytes = TcpSegmentCodec.Build(new TcpSegment(1, 2, 3, 4, TcpFlags.Ack, 5, null, [1, 2]), Source, Destination);
        var destination = Destination;
        switch (kind)
        {
            case "shorter than a header":
                bytes = bytes[..19];
                break;
            case "data offset under 20":
                bytes = WithHeaderWord(bytes, 0x40);
                break;
            case "data offset past the bytes":
                bytes = WithHeaderWord(bytes, 0x60);
                break;
            case "wrong checksum":
                bytes[^1] ^= 1;
                break;
            default:
                destination = IPAddress.Parse("10.0.0.9");
                break;
        }

        var parsed = TcpSegmentCodec.TryParse(bytes, Source, destination, out var segment);

        Assert.IsFalse(parsed);
        Assert.IsNull(segment);
    }

    [TestMethod]
    [DataRow("01 01 02 04 05 B4", (ushort)1460)]
    [DataRow("04 02 02 04 05 B4", (ushort)1460)]
    [DataRow("02 03 00 02 04 05 B4", (ushort)1460)]
    [DataRow("00 02 04 05 B4", null)]
    [DataRow("04", null)]
    [DataRow("04 01", null)]
    [DataRow("04 09", null)]
    [DataRow("01 01 01 04", null)]
    [DataRow("01 01 01 01", null)]
    public void TryParse_Options_FindsTheMaximumSegmentSize(string options, ushort? expected)
    {
        var bytes = WithOptions(Convert.FromHexString(options.Replace(" ", string.Empty, StringComparison.Ordinal)));

        TcpSegmentCodec.TryParse(bytes, Source, Destination, out var segment);

        Assert.AreEqual(expected, segment!.MaximumSegmentSize);
    }

    private static byte[] WithOptions(byte[] options)
    {
        var padded = new byte[(options.Length + 3) / 4 * 4];
        options.CopyTo(padded, 0);
        var header = TcpSegmentCodec.Build(new TcpSegment(1, 2, 3, 4, TcpFlags.Syn, 5, null, []), Source, Destination);
        var bytes = header.Concat(padded).ToArray();
        return WithHeaderWord(bytes, (byte)((20 + padded.Length) / 4 << 4));
    }

    private static byte[] WithHeaderWord(byte[] bytes, byte dataOffset)
    {
        bytes[12] = dataOffset;
        bytes[16] = 0;
        bytes[17] = 0;
        var sum = IpPacketCodec.PseudoHeaderSum(Source, Destination, IpPacketCodec.TcpProtocol, bytes.Length);
        var checksum = InternetChecksum.Finish(InternetChecksum.Add(sum, bytes));
        bytes[16] = (byte)(checksum >> 8);
        bytes[17] = (byte)checksum;
        return bytes;
    }
}
