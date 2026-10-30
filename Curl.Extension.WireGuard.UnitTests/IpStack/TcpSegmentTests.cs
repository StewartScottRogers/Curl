using Curl.Extension.WireGuard.IpStack;

namespace Curl.Extension.WireGuard.UnitTests.IpStack;

[TestClass]
public sealed class TcpSegmentTests
{
    [TestMethod]
    [DataRow(TcpFlags.Ack, 3, 3u)]
    [DataRow(TcpFlags.Syn, 0, 1u)]
    [DataRow(TcpFlags.Fin | TcpFlags.Ack, 2, 3u)]
    [DataRow(TcpFlags.Syn | TcpFlags.Fin, 0, 2u)]
    public void SequenceLength_CountsDataSynAndFin(TcpFlags flags, int length, uint expected)
    {
        TcpSegment segment = new(1, 2, 3, 4, flags, 5, null, new byte[length]);

        Assert.AreEqual(expected, segment.SequenceLength);
    }
}
