using Curl.Protocol.Abstractions;

namespace Curl.Core;

[TestClass]
public sealed class RedirectHopTimingsTests
{
    [TestMethod]
    public void Merge_TwoHops_EachPhaseAddsItsTimeFromItsOwnHopsStart()
    {
        RedirectHopTimings timings = new(new MillisecondTimeProvider());
        TransferTimings first = new(1000, new ConnectTimings(1000, 1100, 1200, 1300), 1400, 1500, 2000, 2500);
        TransferTimings last = new(3500, new ConnectTimings(3500, 3600, 3700, 3800), 3900, 4000, 4500, 6000);
        timings.Add(first);
        timings.Add(last);

        TransferTimings merged = timings.Merge(last)!;

        Assert.AreEqual(1000, merged.Started);
        Assert.AreEqual(3500, merged.Connect!.Started);
        Assert.AreEqual(1200, merged.Connect.NameResolved);
        Assert.AreEqual(1400, merged.Connect.Connected);
        Assert.AreEqual(1600, merged.Connect.TlsHandshakeCompleted);
        Assert.AreEqual(1800, merged.RequestReady);
        Assert.AreEqual(2000, merged.RequestSent);
        Assert.AreEqual(3000, merged.FirstByteReceived);
        Assert.AreEqual(6000, merged.Completed);
        Assert.AreEqual(TimeSpan.FromMilliseconds(2500), merged.RedirectDuration);
    }

    [TestMethod]
    public void Merge_PhaseOnlyAnEarlierHopReached_KeepsItsTime()
    {
        RedirectHopTimings timings = new(new MillisecondTimeProvider());
        TransferTimings last = new(3000, null, null, null, 3200, 3300);
        timings.Add(new TransferTimings(1000, new ConnectTimings(1000, 1050, null, null), null, null, null, 1500));
        timings.Add(last);

        TransferTimings merged = timings.Merge(last)!;

        Assert.AreEqual(1000, merged.Connect!.Started);
        Assert.AreEqual(1050, merged.Connect.NameResolved);
        Assert.IsNull(merged.Connect.Connected);
        Assert.IsNull(merged.Connect.TlsHandshakeCompleted);
        Assert.IsNull(merged.RequestReady);
        Assert.IsNull(merged.RequestSent);
        Assert.AreEqual(1200, merged.FirstByteReceived);
    }

    [TestMethod]
    public void Merge_NoHopRecordedConnectTimings_LeavesThemNull()
    {
        RedirectHopTimings timings = new(new MillisecondTimeProvider());
        TransferTimings last = new(2000, null, 2100, null, null, 2200);
        timings.Add(null);
        timings.Add(last);

        TransferTimings merged = timings.Merge(last)!;

        Assert.IsNull(merged.Connect);
        Assert.AreEqual(2000, merged.Started);
        Assert.AreEqual(2100, merged.RequestReady);
        Assert.AreEqual(TimeSpan.Zero, merged.RedirectDuration);
    }

    [TestMethod]
    public void Merge_NothingAdded_MeasuresFromTheLastHopsOwnStart()
    {
        RedirectHopTimings timings = new(new MillisecondTimeProvider());

        TransferTimings merged = timings.Merge(new TransferTimings(500, null, 600, null, null, 700))!;

        Assert.AreEqual(500, merged.Started);
        Assert.IsNull(merged.RequestReady);
        Assert.AreEqual(TimeSpan.Zero, merged.RedirectDuration);
    }

    [TestMethod]
    public void Merge_NoLastTimings_IsNull()
    {
        RedirectHopTimings timings = new(new MillisecondTimeProvider());
        timings.Add(new TransferTimings(1, null, null, null, null, 2));

        Assert.IsNull(timings.Merge(null));
    }

    private sealed class MillisecondTimeProvider : TimeProvider
    {
        public override long TimestampFrequency => 1000;
    }
}
