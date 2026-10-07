using System.Globalization;
using Curl.Testing;
using static Curl.Quic.QuicLossRecoveryTests;

namespace Curl.Quic;

[TestClass]
public sealed class QuicPacerTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void TimeUntilSend_BurstSpent_WaitsForTheBucketToRefillAtTheWindowOverTheRtt()
    {
        Diagnostics.Arrange("max datagram size", 1200);
        Diagnostics.Arrange("congestion window", 12000);
        Diagnostics.Arrange("smoothed rtt ms", 100);
        Diagnostics.Arrange("burst datagrams", QuicPacer.BurstDatagrams);
        QuicPacer pacer = new(1200);
        for (var packet = 0; packet < QuicPacer.BurstDatagrams; packet++)
        {
            Assert.AreEqual(TimeSpan.Zero, pacer.TimeUntilSend(TimeSpan.Zero, 1200, 12000, Ms(100)));
            pacer.OnPacketSent(TimeSpan.Zero, 1200, 12000, Ms(100));
        }

        // RFC 9002 section 7.7: 1.25 x 12000 bytes per 100 ms is 150000 bytes a second, so 1200 bytes take 8 ms.
        var afterBurst = pacer.TimeUntilSend(TimeSpan.Zero, 1200, 12000, Ms(100));
        var afterRefill = pacer.TimeUntilSend(Ms(8), 1200, 12000, Ms(100));
        Diagnostics.Act("wait after burst ms", Text(afterBurst));
        Diagnostics.Act("wait 8 ms later ms", Text(afterRefill));
        Diagnostics.Assert("wait after burst ms", 8, Text(afterBurst));
        Assert.AreEqual(8, afterBurst.TotalMilliseconds, 1e-6);
        Diagnostics.Assert("wait 8 ms later ms", 0, Text(afterRefill));
        Assert.AreEqual(TimeSpan.Zero, afterRefill);
    }

    [TestMethod]
    public void TimeUntilSend_LongIdle_HoldsNoMoreThanTheBurst()
    {
        Diagnostics.Arrange("now ms", 60000);
        Diagnostics.Arrange("burst datagrams", QuicPacer.BurstDatagrams);
        QuicPacer pacer = new(1200);

        for (var packet = 0; packet < QuicPacer.BurstDatagrams; packet++)
        {
            pacer.OnPacketSent(Ms(60000), 1200, 12000, Ms(100));
        }

        var wait = pacer.TimeUntilSend(Ms(60000), 1200, 12000, Ms(100));
        Diagnostics.Act("wait ms", Text(wait));
        Diagnostics.Assert("wait is above zero", true, wait > TimeSpan.Zero);
        Assert.IsGreaterThan(TimeSpan.Zero, wait);
    }

    [TestMethod]
    public void TimeUntilSend_RttBelowTheGranularity_PacesAtTheGranularity()
    {
        Diagnostics.Arrange("congestion window", 12000);
        Diagnostics.Arrange("smoothed rtt ms", 0);
        QuicPacer pacer = new(1200);
        pacer.OnPacketSent(TimeSpan.Zero, 12000, 12000, TimeSpan.Zero);

        // 1.25 x 12000 bytes per millisecond: 1200 bytes take 0.08 ms.
        var wait = pacer.TimeUntilSend(TimeSpan.Zero, 1200, 12000, TimeSpan.Zero);

        Diagnostics.Act("wait ms", Text(wait));
        Diagnostics.Assert("wait ms (within 0.001)", 0.08, Text(wait));
        Assert.AreEqual(0.08, wait.TotalMilliseconds, 1e-3);
    }

    private static string Text(TimeSpan value) => value.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
}
