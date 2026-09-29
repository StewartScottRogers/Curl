namespace Curl.Quic;

[TestClass]
public sealed class QuicRttEstimatorTests
{
    private static readonly TimeSpan MaxAckDelay = TimeSpan.FromMilliseconds(25);

    [TestMethod]
    public void Constructor_NoSample_UsesTheInitialRtt()
    {
        QuicRttEstimator rtt = new();

        Assert.IsFalse(rtt.HasSample);
        Assert.AreEqual(TimeSpan.FromMilliseconds(333), rtt.SmoothedRtt);
        Assert.AreEqual(TimeSpan.FromMilliseconds(166.5), rtt.RttVariation);
        Assert.AreEqual(TimeSpan.Zero, rtt.MinRtt);

        // Section 6.2.1: 333 + 4 x 166.5.
        Assert.AreEqual(TimeSpan.FromMilliseconds(999), rtt.ProbeTimeout);

        // Section 6.1.2: 9/8 x 333.
        Assert.AreEqual(TimeSpan.FromMilliseconds(374.625), rtt.LossDelay);
    }

    [TestMethod]
    public void Update_ScriptedSamples_TracksSection5()
    {
        QuicRttEstimator rtt = new();

        // First sample: smoothed = sample, variation = half of it, and the ACK delay is ignored (section 5.3).
        rtt.Update(Milliseconds(100), Milliseconds(10), MaxAckDelay, handshakeConfirmed: true);
        AssertRtt(rtt, latest: 100, min: 100, smoothed: 100, variation: 50);

        // 120 - 10 = 110: variation 3/4 x 50 + 1/4 x 10 = 40, smoothed 7/8 x 100 + 1/8 x 110 = 101.25.
        rtt.Update(Milliseconds(120), Milliseconds(10), MaxAckDelay, handshakeConfirmed: true);
        AssertRtt(rtt, latest: 120, min: 100, smoothed: 101.25, variation: 40);

        // Delay capped at max_ack_delay 25 once confirmed; 80 < 80 + 25, so the sample is used whole.
        rtt.Update(Milliseconds(80), Milliseconds(50), MaxAckDelay, handshakeConfirmed: true);
        AssertRtt(rtt, latest: 80, min: 80, smoothed: 98.59375, variation: 35.3125);

        // Before confirmation the delay is not capped: 150 - 50 = 100.
        rtt.Update(Milliseconds(150), Milliseconds(50), MaxAckDelay, handshakeConfirmed: false);
        AssertRtt(rtt, latest: 150, min: 80, smoothed: 98.76953125, variation: 26.8359375);
        Assert.IsTrue(rtt.HasSample);
    }

    [TestMethod]
    public void LossDelay_TinySamples_IsAtLeastTheGranularity()
    {
        QuicRttEstimator rtt = new();

        rtt.Update(TimeSpan.Zero, TimeSpan.Zero, MaxAckDelay, handshakeConfirmed: false);

        Assert.AreEqual(QuicRttEstimator.Granularity, rtt.LossDelay);
        Assert.AreEqual(QuicRttEstimator.Granularity, rtt.ProbeTimeout);
    }

    private static TimeSpan Milliseconds(double milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    private static void AssertRtt(QuicRttEstimator rtt, double latest, double min, double smoothed, double variation)
    {
        Assert.AreEqual(latest, rtt.LatestRtt.TotalMilliseconds, 0.001);
        Assert.AreEqual(min, rtt.MinRtt.TotalMilliseconds, 0.001);
        Assert.AreEqual(smoothed, rtt.SmoothedRtt.TotalMilliseconds, 0.001);
        Assert.AreEqual(variation, rtt.RttVariation.TotalMilliseconds, 0.001);
    }
}
