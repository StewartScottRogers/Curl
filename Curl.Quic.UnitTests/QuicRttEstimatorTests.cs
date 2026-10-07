using System.Globalization;
using Curl.Testing;

namespace Curl.Quic;

[TestClass]
public sealed class QuicRttEstimatorTests
{
    private static readonly TimeSpan MaxAckDelay = TimeSpan.FromMilliseconds(25);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Constructor_NoSample_UsesTheInitialRtt()
    {
        Diagnostics.Arrange("samples", 0);

        QuicRttEstimator rtt = new();

        Diagnostics.Act("has sample", rtt.HasSample);
        Diagnostics.Act("smoothed rtt ms", Ms(rtt.SmoothedRtt));
        Diagnostics.Act("rtt variation ms", Ms(rtt.RttVariation));
        Diagnostics.Act("min rtt ms", Ms(rtt.MinRtt));
        Diagnostics.Act("probe timeout ms", Ms(rtt.ProbeTimeout));
        Diagnostics.Act("loss delay ms", Ms(rtt.LossDelay));
        Diagnostics.Assert("has sample", false, rtt.HasSample);
        Assert.IsFalse(rtt.HasSample);
        Diagnostics.Assert("smoothed rtt ms", "333", Ms(rtt.SmoothedRtt));
        Assert.AreEqual(TimeSpan.FromMilliseconds(333), rtt.SmoothedRtt);
        Diagnostics.Assert("rtt variation ms", "166.5", Ms(rtt.RttVariation));
        Assert.AreEqual(TimeSpan.FromMilliseconds(166.5), rtt.RttVariation);
        Diagnostics.Assert("min rtt ms", "0", Ms(rtt.MinRtt));
        Assert.AreEqual(TimeSpan.Zero, rtt.MinRtt);

        // Section 6.2.1: 333 + 4 x 166.5.
        Diagnostics.Assert("probe timeout ms", "999", Ms(rtt.ProbeTimeout));
        Assert.AreEqual(TimeSpan.FromMilliseconds(999), rtt.ProbeTimeout);

        // Section 6.1.2: 9/8 x 333.
        Diagnostics.Assert("loss delay ms", "374.625", Ms(rtt.LossDelay));
        Assert.AreEqual(TimeSpan.FromMilliseconds(374.625), rtt.LossDelay);
    }

    [TestMethod]
    public void Update_ScriptedSamples_TracksSection5()
    {
        Diagnostics.Arrange("max ack delay ms", Ms(MaxAckDelay));
        QuicRttEstimator rtt = new();

        // First sample: smoothed = sample, variation = half of it, and the ACK delay is ignored (section 5.3).
        Diagnostics.Arrange("sample 1 (latest ms, ack delay ms, confirmed)", "100, 10, true");
        rtt.Update(Milliseconds(100), Milliseconds(10), MaxAckDelay, handshakeConfirmed: true);
        AssertRtt(rtt, latest: 100, min: 100, smoothed: 100, variation: 50);

        // 120 - 10 = 110: variation 3/4 x 50 + 1/4 x 10 = 40, smoothed 7/8 x 100 + 1/8 x 110 = 101.25.
        Diagnostics.Arrange("sample 2 (latest ms, ack delay ms, confirmed)", "120, 10, true");
        rtt.Update(Milliseconds(120), Milliseconds(10), MaxAckDelay, handshakeConfirmed: true);
        AssertRtt(rtt, latest: 120, min: 100, smoothed: 101.25, variation: 40);

        // Delay capped at max_ack_delay 25 once confirmed; 80 < 80 + 25, so the sample is used whole.
        Diagnostics.Arrange("sample 3 (latest ms, ack delay ms, confirmed)", "80, 50, true");
        rtt.Update(Milliseconds(80), Milliseconds(50), MaxAckDelay, handshakeConfirmed: true);
        AssertRtt(rtt, latest: 80, min: 80, smoothed: 98.59375, variation: 35.3125);

        // Before confirmation the delay is not capped: 150 - 50 = 100.
        Diagnostics.Arrange("sample 4 (latest ms, ack delay ms, confirmed)", "150, 50, false");
        rtt.Update(Milliseconds(150), Milliseconds(50), MaxAckDelay, handshakeConfirmed: false);
        AssertRtt(rtt, latest: 150, min: 80, smoothed: 98.76953125, variation: 26.8359375);
        Diagnostics.Assert("has sample", true, rtt.HasSample);
        Assert.IsTrue(rtt.HasSample);
    }

    [TestMethod]
    public void LossDelay_TinySamples_IsAtLeastTheGranularity()
    {
        Diagnostics.Arrange("sample ms", 0);
        QuicRttEstimator rtt = new();

        rtt.Update(TimeSpan.Zero, TimeSpan.Zero, MaxAckDelay, handshakeConfirmed: false);

        Diagnostics.Act("loss delay ms", Ms(rtt.LossDelay));
        Diagnostics.Act("probe timeout ms", Ms(rtt.ProbeTimeout));
        Diagnostics.Assert("loss delay ms", Ms(QuicRttEstimator.Granularity), Ms(rtt.LossDelay));
        Assert.AreEqual(QuicRttEstimator.Granularity, rtt.LossDelay);
        Diagnostics.Assert("probe timeout ms", Ms(QuicRttEstimator.Granularity), Ms(rtt.ProbeTimeout));
        Assert.AreEqual(QuicRttEstimator.Granularity, rtt.ProbeTimeout);
    }

    private static string Ms(TimeSpan value) => value.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);

    private static TimeSpan Milliseconds(double milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    private void AssertRtt(QuicRttEstimator rtt, double latest, double min, double smoothed, double variation)
    {
        var actual = $"{Ms(rtt.LatestRtt)}, {Ms(rtt.MinRtt)}, {Ms(rtt.SmoothedRtt)}, {Ms(rtt.RttVariation)}";
        Diagnostics.Act("latest, min, smoothed, variation ms", actual);
        Diagnostics.Assert("latest, min, smoothed, variation ms", string.Create(CultureInfo.InvariantCulture, $"{latest}, {min}, {smoothed}, {variation}"), actual);
        Assert.AreEqual(latest, rtt.LatestRtt.TotalMilliseconds, 0.001);
        Assert.AreEqual(min, rtt.MinRtt.TotalMilliseconds, 0.001);
        Assert.AreEqual(smoothed, rtt.SmoothedRtt.TotalMilliseconds, 0.001);
        Assert.AreEqual(variation, rtt.RttVariation.TotalMilliseconds, 0.001);
    }
}
