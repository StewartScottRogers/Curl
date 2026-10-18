using Curl.Api.Callbacks;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Hands a transfer's progress to a <c>CURLOPT_XFERINFOFUNCTION</c> or <c>CURLOPT_PROGRESSFUNCTION</c>
/// (ADR-0543): every report the handler makes calls the callback with the download total, the bytes
/// downloaded, the upload total and the bytes uploaded so far, a total the handler does not know as 0,
/// all four from zero when the transfer, or a request within it, starts. A callback that returns
/// anything but 0 or <see cref="ProgressCallbackResult.Continue" /> aborts the transfer, as libcurl's
/// <c>pgrsupdate</c> does: <see cref="Aborted" /> turns <see langword="true" />, <paramref name="abort" />
/// is cancelled, which stops the attempt, and the callback is not called again.
/// </summary>
/// <param name="report">The callback, with libcurl's four counts.</param>
/// <param name="abort">Cancelled when the callback aborts the transfer.</param>
internal sealed class CallbackTransferProgress(Func<long, long, long, long, int> report, CancellationTokenSource abort) : ITransferProgress
{
    /// <summary>libcurl's <c>CURLE_ABORTED_BY_CALLBACK</c> text for a progress callback that aborted the transfer.</summary>
    internal const string CallbackAbortedText = "Callback aborted";

    private long downloadTotal;
    private long downloaded;
    private long uploadTotal;
    private long uploaded;

    /// <summary>Gets a value indicating whether the callback aborted the transfer.</summary>
    internal bool Aborted { get; private set; }

    /// <summary>
    /// The progress a handle's transfer reports to: its <see cref="EasyCallbackOptions.TransferInfo" />, else
    /// its <see cref="EasyCallbackOptions.Progress" /> with the counts as doubles, as libcurl prefers the
    /// xferinfo callback when both are set; <see langword="null" />, for none, under <c>CURLOPT_NOPROGRESS</c>
    /// or with neither set, as libcurl then calls neither.
    /// </summary>
    /// <param name="noProgress">Whether <c>CURLOPT_NOPROGRESS</c> is set.</param>
    /// <param name="callbacks">The handle's callbacks.</param>
    /// <param name="abort">Cancelled when the callback aborts the transfer.</param>
    /// <returns>The progress, or <see langword="null" />.</returns>
    internal static CallbackTransferProgress? Of(bool noProgress, EasyCallbackOptions callbacks, CancellationTokenSource abort)
    {
        if (noProgress)
        {
            return null;
        }

        if (callbacks.TransferInfo is { } transferInfo)
        {
            return new CallbackTransferProgress((dlTotal, dlNow, ulTotal, ulNow) => transferInfo(dlTotal, dlNow, ulTotal, ulNow), abort);
        }

        return callbacks.Progress is { } progress
            ? new CallbackTransferProgress((dlTotal, dlNow, ulTotal, ulNow) => progress(dlTotal, dlNow, ulTotal, ulNow), abort)
            : null;
    }

    /// <inheritdoc />
    public void ReportTransferStarted()
    {
        downloadTotal = 0;
        downloaded = 0;
        uploadTotal = 0;
        uploaded = 0;
        Report();
    }

    /// <inheritdoc />
    public void ReportDownloaded(long bytesSoFar, long? expectedTotal)
    {
        downloaded = bytesSoFar;
        downloadTotal = expectedTotal ?? 0;
        Report();
    }

    /// <inheritdoc />
    public void ReportUploaded(long bytesSoFar, long? expectedTotal)
    {
        uploaded = bytesSoFar;
        uploadTotal = expectedTotal ?? 0;
        Report();
    }

    /// <inheritdoc />
    public void ReportTransferDone() => Report();

    /// <summary>Calls the callback again with the counts it was last given, as libcurl does each time a paused transfer wakes (<see cref="EasyPauseGate.TickWhilePaused" />, BL-2631).</summary>
    internal void ReportWhilePaused() => Report();

    /// <summary>What a paused wait of a perform reporting to <paramref name="progress" /> calls (<see cref="ReportWhilePaused" />), or <see langword="null" /> for no progress callback.</summary>
    /// <param name="progress">The perform's progress callback sink, or <see langword="null" />.</param>
    /// <returns>The tick, or <see langword="null" />.</returns>
    internal static Action? PausedTickOf(CallbackTransferProgress? progress) => progress is null ? null : progress.ReportWhilePaused;

    private void Report()
    {
        if (Aborted)
        {
            return;
        }

        int result = report(downloadTotal, downloaded, uploadTotal, uploaded);
        if (result is not 0 and not ProgressCallbackResult.Continue)
        {
            Aborted = true;
            abort.Cancel();
        }
    }
}
