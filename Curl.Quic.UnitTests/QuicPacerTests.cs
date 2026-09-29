using static Curl.Quic.QuicLossRecoveryTests;

namespace Curl.Quic;

[TestClass]
public sealed class QuicPacerTests
{
    [TestMethod]
    public void TimeUntilSend_BurstSpent_WaitsForTheBucketToRefillAtTheWindowOverTheRtt()
    {
        QuicPacer pacer = new(1200);
        for (var packet = 0; packet < QuicPacer.BurstDatagrams; packet++)
        {
            Assert.AreEqual(TimeSpan.Zero, pacer.TimeUntilSend(TimeSpan.Zero, 1200, 12000, Ms(100)));
            pacer.OnPacketSent(TimeSpan.Zero, 1200, 12000, Ms(100));
        }

        // RFC 9002 section 7.7: 1.25 x 12000 bytes per 100 ms is 150000 bytes a second, so 1200 bytes take 8 ms.
        Assert.AreEqual(8, pacer.TimeUntilSend(TimeSpan.Zero, 1200, 12000, Ms(100)).TotalMilliseconds, 1e-6);
        Assert.AreEqual(TimeSpan.Zero, pacer.TimeUntilSend(Ms(8), 1200, 12000, Ms(100)));
    }

    [TestMethod]
    public void TimeUntilSend_LongIdle_HoldsNoMoreThanTheBurst()
    {
        QuicPacer pacer = new(1200);

        for (var packet = 0; packet < QuicPacer.BurstDatagrams; packet++)
        {
            pacer.OnPacketSent(Ms(60000), 1200, 12000, Ms(100));
        }

        Assert.IsGreaterThan(TimeSpan.Zero, pacer.TimeUntilSend(Ms(60000), 1200, 12000, Ms(100)));
    }

    [TestMethod]
    public void TimeUntilSend_RttBelowTheGranularity_PacesAtTheGranularity()
    {
        QuicPacer pacer = new(1200);
        pacer.OnPacketSent(TimeSpan.Zero, 12000, 12000, TimeSpan.Zero);

        // 1.25 x 12000 bytes per millisecond: 1200 bytes take 0.08 ms.
        Assert.AreEqual(0.08, pacer.TimeUntilSend(TimeSpan.Zero, 1200, 12000, TimeSpan.Zero).TotalMilliseconds, 1e-3);
    }
}
