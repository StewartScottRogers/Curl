using Curl.Api.Callbacks;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

[TestClass]
public sealed class CallbackTransferProgressTests
{
    [TestMethod]
    public void Of_NoProgressSet_GivesNoneEvenWithCallbacks()
    {
        using CancellationTokenSource abort = new();

        Assert.IsNull(CallbackTransferProgress.Of(noProgress: true, new EasyCallbackOptions { TransferInfo = (_, _, _, _) => 0 }, abort, () => Stream.Null, TimeProvider.System));
    }

    [TestMethod]
    public void PausedTickOf_NoProgress_IsNone()
    {
        Assert.IsNull(CallbackTransferProgress.PausedTickOf(null));
    }

    [TestMethod]
    public void PausedTickOf_Progress_CallsTheCallbackAgainWithTheLastCounts()
    {
        using CancellationTokenSource abort = new();
        List<(long, long, long, long)> calls = [];
        CallbackTransferProgress progress = new((dlTotal, dlNow, ulTotal, ulNow) =>
        {
            calls.Add((dlTotal, dlNow, ulTotal, ulNow));
            return 0;
        }, abort);
        progress.ReportUploaded(1, 2);

        CallbackTransferProgress.PausedTickOf(progress)!();

        Assert.HasCount(2, calls);
        Assert.AreEqual((0L, 0L, 2L, 1L), calls[1]);
    }

    [TestMethod]
    public void Of_NeitherCallbackSet_GivesTheBuiltInMeterAlone()
    {
        using CancellationTokenSource abort = new();
        using MemoryStream standardError = new();
        CallbackTransferProgress progress = CallbackTransferProgress.Of(noProgress: false, new EasyCallbackOptions(), abort, () => standardError, TimeProvider.System)!;

        progress.ReportTransferStarted();
        progress.ReportDownloaded(10, 10);
        progress.ReportTransferDone();
        progress.FinishMeter(succeeded: true);

        string written = System.Text.Encoding.UTF8.GetString(standardError.ToArray());
        Assert.StartsWith("  % Total    % Received % Xferd  Average Speed", written);
        Assert.EndsWith(Environment.NewLine, written);
        Assert.IsFalse(progress.Aborted);
        Assert.IsFalse(abort.IsCancellationRequested);
    }

    [TestMethod]
    public void Of_BothCallbacksSet_CallsTheTransferInfoCallbackOnly()
    {
        using CancellationTokenSource abort = new();
        List<string> calls = [];
        EasyCallbackOptions callbacks = new()
        {
            TransferInfo = (dlTotal, dlNow, ulTotal, ulNow) =>
            {
                calls.Add($"xferinfo {dlTotal} {dlNow} {ulTotal} {ulNow}");
                return 0;
            },
            Progress = (_, _, _, _) =>
            {
                calls.Add("progress");
                return 0;
            },
        };

        CallbackTransferProgress.Of(noProgress: false, callbacks, abort, () => Stream.Null, TimeProvider.System)!.ReportDownloaded(3, 10);

        CollectionAssert.AreEqual(new[] { "xferinfo 10 3 0 0" }, calls);
    }

    [TestMethod]
    public void Of_ProgressCallbackOnly_CallsItWithTheCountsAsDoubles()
    {
        using CancellationTokenSource abort = new();
        List<double[]> calls = [];
        EasyCallbackOptions callbacks = new()
        {
            Progress = (dlTotal, dlNow, ulTotal, ulNow) =>
            {
                calls.Add([dlTotal, dlNow, ulTotal, ulNow]);
                return 0;
            },
        };

        CallbackTransferProgress.Of(noProgress: false, callbacks, abort, () => Stream.Null, TimeProvider.System)!.ReportUploaded(4, 8);

        CollectionAssert.AreEqual(new[] { 0d, 0d, 8d, 4d }, calls.Single());
    }

    [TestMethod]
    public void Reports_CallTheCallbackWithTheRunningCountsAndUnknownTotalsAsZero()
    {
        using CancellationTokenSource abort = new();
        List<string> calls = [];
        ITransferProgress progress = Recording(calls, abort, abortAt: -1);

        progress.ReportTransferStarted();
        progress.ReportUploaded(2, null);
        progress.ReportDownloaded(5, 5);
        progress.ReportTransferDone();
        progress.ReportTransferStarted();

        CollectionAssert.AreEqual(new[] { "0 0 0 0", "0 0 0 2", "5 5 0 2", "5 5 0 2", "0 0 0 0" }, calls);
        Assert.IsFalse(abort.IsCancellationRequested);
    }

    [TestMethod]
    public void Report_CallbackReturnsContinue_CarriesOn()
    {
        using CancellationTokenSource abort = new();
        CallbackTransferProgress progress = new((_, _, _, _) => ProgressCallbackResult.Continue, abort);

        progress.ReportDownloaded(1, null);

        Assert.IsFalse(progress.Aborted);
        Assert.IsFalse(abort.IsCancellationRequested);
    }

    [TestMethod]
    public void Report_CallbackReturnsNonZero_AbortsAndIsNotCalledAgain()
    {
        using CancellationTokenSource abort = new();
        List<string> calls = [];
        CallbackTransferProgress progress = Recording(calls, abort, abortAt: 2);

        progress.ReportTransferStarted();
        progress.ReportDownloaded(1, null);
        progress.ReportDownloaded(2, null);

        Assert.IsTrue(progress.Aborted);
        Assert.IsTrue(abort.IsCancellationRequested);
        Assert.HasCount(2, calls);
    }

    private static CallbackTransferProgress Recording(List<string> calls, CancellationTokenSource abort, int abortAt) =>
        new(
            (dlTotal, dlNow, ulTotal, ulNow) =>
            {
                calls.Add($"{dlTotal} {dlNow} {ulTotal} {ulNow}");
                return calls.Count == abortAt ? 1 : 0;
            },
            abort);
}
