using System.Text;
using Curl.Api.Callbacks;
using Curl.Console;

namespace Curl.Engine.UnitTests;

/// <summary>
/// Pins what <see cref="CallbackTransferProgress" /> hands libcurl's built-in progress meter: a report answered with
/// <see cref="ProgressCallbackResult.Continue" />, and every report of a handle with no callback, reach it; one
/// answered with 0 does not; and <see cref="CallbackTransferProgress.FinishMeter" /> writes nothing more after an
/// abort or a transfer that never started (BL-4057, ADR-0993).
/// </summary>
[TestClass]
public sealed class CallbackTransferProgressMeterTests
{
    private const string FirstHeaderStart = "  % Total    % Received % Xferd  Average Speed";

    private readonly ManualTimeProvider clock = new();

    private readonly MemoryStream standardError = new();

    private string Written => Encoding.UTF8.GetString(standardError.ToArray());

    [TestCleanup]
    public void DisposeStandardError() => standardError.Dispose();

    [TestMethod]
    public void FinishMeter_CallbackAnswersContinue_DrawsTheMeterWithNoFinalNewLine()
    {
        using CancellationTokenSource abort = new();
        CallbackTransferProgress progress = ProgressOf(new EasyCallbackOptions { TransferInfo = (_, _, _, _) => ProgressCallbackResult.Continue }, abort);

        DownloadTenBytes(progress);

        Assert.StartsWith(FirstHeaderStart, Written);
        Assert.Contains("\r100 ", Written);
        Assert.IsFalse(Written.EndsWith(Environment.NewLine, StringComparison.Ordinal));
    }

    [TestMethod]
    public void FinishMeter_NoCallback_DrawsTheMeterEndingWithANewLine()
    {
        using CancellationTokenSource abort = new();
        CallbackTransferProgress progress = ProgressOf(new EasyCallbackOptions(), abort);

        DownloadTenBytes(progress);

        Assert.StartsWith(FirstHeaderStart, Written);
        Assert.Contains("\r100 ", Written);
        Assert.EndsWith(Environment.NewLine, Written);
    }

    [TestMethod]
    public void FinishMeter_CallbackAnswersZero_WritesNothing()
    {
        using CancellationTokenSource abort = new();
        CallbackTransferProgress progress = ProgressOf(new EasyCallbackOptions { TransferInfo = (_, _, _, _) => 0 }, abort);

        DownloadTenBytes(progress);

        Assert.AreEqual(string.Empty, Written);
    }

    [TestMethod]
    public void FinishMeter_CallbackAbortedAfterContinuing_WritesNoStatusLines()
    {
        using CancellationTokenSource abort = new();
        int answer = ProgressCallbackResult.Continue;
        CallbackTransferProgress progress = ProgressOf(new EasyCallbackOptions { TransferInfo = (_, _, _, _) => answer }, abort);
        progress.ReportTransferStarted();
        answer = 1;
        progress.ReportDownloaded(4, 10);
        long writtenBeforeFinish = standardError.Length;

        progress.FinishMeter(succeeded: false);

        Assert.IsTrue(progress.Aborted);
        Assert.AreEqual(writtenBeforeFinish, standardError.Length);
    }

    [TestMethod]
    public void FinishMeter_TransferNeverStarted_WritesNothing()
    {
        using CancellationTokenSource abort = new();
        CallbackTransferProgress progress = ProgressOf(new EasyCallbackOptions(), abort);
        progress.ReportDownloaded(4, 10);
        long writtenBeforeFinish = standardError.Length;

        progress.FinishMeter(succeeded: false);

        Assert.AreEqual(writtenBeforeFinish, standardError.Length);
    }

    [TestMethod]
    public void FinishMeter_UploadReportContinues_DrawsTheUploadOnTheMeter()
    {
        using CancellationTokenSource abort = new();
        CallbackTransferProgress progress = ProgressOf(new EasyCallbackOptions(), abort);
        progress.ReportTransferStarted();
        clock.Advance(40);
        progress.ReportUploaded(8, 8);
        progress.ReportTransferDone();

        progress.FinishMeter(succeeded: true);

        Assert.StartsWith(FirstHeaderStart, Written);
        Assert.Contains("\r100      8   0      0 100      8      0    200 ", Written);
    }

    private CallbackTransferProgress ProgressOf(EasyCallbackOptions callbacks, CancellationTokenSource abort) =>
        CallbackTransferProgress.Of(noProgress: false, callbacks, abort, () => standardError, clock)!;

    private void DownloadTenBytes(CallbackTransferProgress progress)
    {
        progress.ReportTransferStarted();
        clock.Advance(40);
        progress.ReportDownloaded(10, 10);
        progress.ReportTransferDone();
        progress.FinishMeter(succeeded: true);
    }
}
