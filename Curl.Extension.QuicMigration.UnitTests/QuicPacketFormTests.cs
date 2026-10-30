namespace Curl.Extension.QuicMigration;

/// <summary>Checks <see cref="QuicPacketForm" /> against RFC 9000 section 17.2 (long header) and section 17.3.1 (1-RTT packet).</summary>
[TestClass]
public sealed class QuicPacketFormTests
{
    [TestMethod]
    [DataRow(new byte[] { 0x40 }, true)]
    [DataRow(new byte[] { 0x7F, 0x01 }, true)]
    [DataRow(new byte[] { 0xC0 }, false)]
    [DataRow(new byte[] { 0x00 }, false)]
    [DataRow(new byte[0], false)]
    public void IsOneRtt_Section17_3_1_IsAShortHeaderWithTheFixedBit(byte[] datagram, bool isOneRtt) =>
        Assert.AreEqual(isOneRtt, QuicPacketForm.IsOneRtt(datagram));
}
