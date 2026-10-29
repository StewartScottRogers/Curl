using System.Text;
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
/// is cancelled, which stops the attempt, and the callback is not called again. A report the callback
/// answers with <see cref="ProgressCallbackResult.Continue" />, and every report of a handle with no
/// callback, is passed on to libcurl's built-in progress meter, drawn on <paramref name="meterOutput" />
/// (BL-4012, ADR-0993).
/// </summary>
/// <param name="report">The callback, with libcurl's four counts.</param>
/// <param name="abort">Cancelled when the callback aborts the transfer.</param>
/// <param name="meterOutput">Where the built-in meter is drawn, or <see langword="null" /> for no meter.</param>
internal sealed class CallbackTransferProgress(Func<long, long, long, long, int> report, CancellationTokenSource abort, CallbackTransferProgress.MeterOutput? meterOutput = null) : ITransferProgress
{
    /// <summary>libcurl's <c>CURLE_ABORTED_BY_CALLBACK</c> text for a progress callback that aborted the transfer.</summary>
    internal const string CallbackAbortedText = "Callback aborted";

    private long downloadTotal;
    private long downloaded;
    private long uploadTotal;
    private long uploaded;

    /// <summary>The built-in meter, made when a report first continues to it.</summary>
    private TransferProgressRecorder? meter;

    /// <summary>Whether the meter's header lines have been written.</summary>
    private bool headerLinesWritten;

    /// <summary>Gets a value indicating whether the callback aborted the transfer.</summary>
    internal bool Aborted { get; private set; }

    /// <summary>
    /// The progress a handle's transfer reports to: its <see cref="EasyCallbackOptions.TransferInfo" />, else
    /// its <see cref="EasyCallbackOptions.Progress" /> with the counts as doubles, as libcurl prefers the
    /// xferinfo callback when both are set, else, with neither, the built-in meter alone, every report
    /// continuing to it; <see langword="null" />, for none, under <c>CURLOPT_NOPROGRESS</c>, as libcurl then
    /// calls neither and draws no meter.
    /// </summary>
    /// <param name="noProgress">Whether <c>CURLOPT_NOPROGRESS</c> is set.</param>
    /// <param name="callbacks">The handle's callbacks.</param>
    /// <param name="abort">Cancelled when the callback aborts the transfer.</param>
    /// <param name="standardError">The handle's standard error, asked for only when the meter writes.</param>
    /// <param name="timeProvider">The clock the meter draws on.</param>
    /// <returns>The progress, or <see langword="null" />.</returns>
    internal static CallbackTransferProgress? Of(bool noProgress, EasyCallbackOptions callbacks, CancellationTokenSource abort, Func<Stream> standardError, TimeProvider timeProvider)
    {
        if (noProgress)
        {
            return null;
        }

        MeterOutput callbackMeter = new(standardError, timeProvider, EndsWithNewLine: false);
        if (callbacks.TransferInfo is { } transferInfo)
        {
            return new CallbackTransferProgress((dlTotal, dlNow, ulTotal, ulNow) => transferInfo(dlTotal, dlNow, ulTotal, ulNow), abort, callbackMeter);
        }

        return callbacks.Progress is { } progress
            ? new CallbackTransferProgress((dlTotal, dlNow, ulTotal, ulNow) => progress(dlTotal, dlNow, ulTotal, ulNow), abort, callbackMeter)
            : new CallbackTransferProgress((_, _, _, _) => ProgressCallbackResult.Continue, abort, callbackMeter with { EndsWithNewLine = true });
    }

    /// <inheritdoc />
    public void ReportTransferStarted()
    {
        downloadTotal = 0;
        downloaded = 0;
        uploadTotal = 0;
        uploaded = 0;
        Report()?.ReportTransferStarted();
    }

    /// <inheritdoc />
    public void ReportDownloaded(long bytesSoFar, long? expectedTotal)
    {
        downloaded = bytesSoFar;
        downloadTotal = expectedTotal ?? 0;
        Report()?.ReportDownloaded(bytesSoFar, expectedTotal);
    }

    /// <inheritdoc />
    public void ReportUploaded(long bytesSoFar, long? expectedTotal)
    {
        uploaded = bytesSoFar;
        uploadTotal = expectedTotal ?? 0;
        Report()?.ReportUploaded(bytesSoFar, expectedTotal);
    }

    /// <inheritdoc />
    public void ReportTransferDone() => Report()?.ReportTransferDone();

    /// <summary>Calls the callback again with the counts it was last given, as libcurl does each time a paused transfer wakes (<see cref="EasyPauseGate.TickWhilePaused" />, BL-2631).</summary>
    internal void ReportWhilePaused() => Report();

    /// <summary>
    /// Ends the built-in meter, when a report made it: stops its clock and, unless the callback aborted or the
    /// transfer never started, writes the status lines libcurl's <c>Curl_pgrsDone</c> draws and, for a handle
    /// with no callback, the newline libcurl ends the meter with only then.
    /// </summary>
    /// <param name="succeeded">Whether the transfer succeeded.</param>
    internal void FinishMeter(bool succeeded)
    {
        if (meter is null)
        {
            return;
        }

        meter.Finish(succeeded);
        if (Aborted || !meter.HasTransferStarted)
        {
            return;
        }

        WriteMeter(meter.TakeUnwrittenStatusLines());
        WriteMeter(meterOutput!.EndsWithNewLine ? Environment.NewLine : string.Empty);
    }

    /// <summary>What a paused wait of a perform reporting to <paramref name="progress" /> calls (<see cref="ReportWhilePaused" />), or <see langword="null" /> for no progress callback.</summary>
    /// <param name="progress">The perform's progress callback sink, or <see langword="null" />.</param>
    /// <returns>The tick, or <see langword="null" />.</returns>
    internal static Action? PausedTickOf(CallbackTransferProgress? progress) => progress is null ? null : progress.ReportWhilePaused;

    /// <summary>Calls the callback, unless it aborted, and aborts the transfer on an answer that is neither 0 nor continue.</summary>
    /// <returns>The meter to pass the report on to when the callback continued to it, else <see langword="null" />.</returns>
    private TransferProgressRecorder? Report()
    {
        if (Aborted)
        {
            return null;
        }

        int result = report(downloadTotal, downloaded, uploadTotal, uploaded);
        if (result is ProgressCallbackResult.Continue)
        {
            return MeterOf();
        }

        if (result is not 0)
        {
            Aborted = true;
            abort.Cancel();
        }

        return null;
    }

    /// <summary>The built-in meter, made on first use, or <see langword="null" /> when there is nowhere to draw it.</summary>
    /// <returns>The meter, or <see langword="null" />.</returns>
    private TransferProgressRecorder? MeterOf() =>
        meterOutput is null ? null : meter ??= new TransferProgressRecorder(meterOutput.TimeProvider, writeLive: WriteMeter);

    /// <summary>Writes meter text to standard error and flushes it, after the meter's header lines the first time.</summary>
    /// <param name="text">The text; nothing is written when it is empty.</param>
    private void WriteMeter(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        StringBuilder written = new();
        if (!headerLinesWritten)
        {
            headerLinesWritten = true;
            foreach (string headerLine in ProgressMeterLines.HeaderLines(null))
            {
                written.Append(headerLine).Append(Environment.NewLine);
            }
        }

        Stream standardError = meterOutput!.StandardError();
        standardError.Write(Encoding.UTF8.GetBytes(written.Append(text).ToString()));
        standardError.Flush();
    }

    /// <summary>Where a handle's built-in progress meter is drawn (BL-4012).</summary>
    /// <param name="StandardError">The handle's standard error, asked for only when the meter writes.</param>
    /// <param name="TimeProvider">The clock the meter draws on.</param>
    /// <param name="EndsWithNewLine">Whether the meter ends with a newline, as libcurl's does only for a handle with no progress callback.</param>
    internal sealed record MeterOutput(Func<Stream> StandardError, TimeProvider TimeProvider, bool EndsWithNewLine);
}
