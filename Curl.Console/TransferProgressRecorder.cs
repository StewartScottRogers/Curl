using System.Text;
using Curl.Output;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// The <see cref="ITransferProgress" /> sink one transfer's handler reports to. It records
/// whether the handler said the transfer got past connect or open, which decides whether a
/// failed transfer still prints the progress meter (task BL-130), and draws the meter's
/// status lines from the byte reports on the clock it is given, when and
/// as curl 8.21.0's <c>progress_calc</c> and <c>progress_meter</c> draw them (task BL-131).
/// Under <c>-#</c> it also passes every report on to the transfer's
/// <see cref="ProgressBarRecorder" /> (task BL-132). Under <c>-L</c> each hop of the chain is
/// drawn on a status line of its own (task BL-277, ADR-0086). Under <c>-Z</c> every byte report is also
/// passed on to the transfer's share of the run's combined meter (task BL-521, ADR-0155).
/// </summary>
/// <remarks>
/// The first draw is made when the recorder is made, with every counter zero, so it is
/// <see cref="ProgressMeterLines.ZeroStatusLine" />. After that a report draws a line only once a
/// second has passed since the last speed sample; <see cref="Finish" /> makes the draws curl
/// makes once the transfer is done. curl also skips a draw in the same whole second as the
/// last one, which cannot happen here: every draw of a running transfer follows a new sample,
/// taken a second or more after the one before.
/// <para>
/// <c>RedirectFollower</c> hands every hop the same sink, and the handler of each hop reports
/// "transfer started" once for it, so a repeated <see cref="ReportTransferStarted" /> is the
/// next hop starting: the hop before it is finished with <see cref="RedirectDoneDrawCount" />
/// draws and ended with a newline, and the new hop's counters start again from zero at a new
/// zero line, as curl 8.21.0 draws them (measured 2026-09-27, BL-277 Notes).
/// </para>
/// <para>
/// Given a live writer, the recorder hands it the lines drawn so far once the handler first
/// reports the transfer started, and after that every line as a report draws it, so a
/// terminal sees the meter move (task BL-383, ADR-0099). Before that report the lines are
/// only kept: a transfer that fails before it starts shows no meter (task BL-130).
/// </para>
/// </remarks>
internal sealed class TransferProgressRecorder : ITransferProgress
{
    /// <summary>
    /// How many speed samples curl keeps (<c>CURL_SPEED_RECORDS</c>), for a current speed over
    /// the last five seconds.
    /// </summary>
    private const int SpeedSampleCount = 6;

    /// <summary>
    /// How many times curl 8.21.0 draws the status line once a transfer is done: after the
    /// transfer state, after the done state, and in <c>Curl_pgrsDone</c>. Measured on a
    /// ten-byte HTTP download (BL-131 Notes).
    /// </summary>
    private const int DoneDrawCount = 3;

    /// <summary>
    /// How many times curl 8.21.0 draws the status line once a hop that answered with a
    /// redirect is done, whether <c>-L</c> follows it or <c>--max-redirs</c> refuses it with
    /// exit 47: two, after the zero line, whatever bytes it reported. Measured on a
    /// <c>302</c> with an empty body (BL-277 Notes).
    /// </summary>
    private const int RedirectDoneDrawCount = 2;

    /// <summary>
    /// How many times curl 8.22.0 draws the status line of an attempt whose reused connection
    /// died before its response began, before the resend on a fresh connection starts a new one:
    /// one, <c>Curl_pgrsDone</c>'s forced update. Measured on upstream test675 (BL-2200 Notes).
    /// </summary>
    private const int DiedConnectionDoneDrawCount = 1;

    /// <summary>The clock every draw and speed sample reads.</summary>
    private readonly TimeProvider timeProvider;

    /// <summary>The <c>-#</c> bar every report is passed on to, or <see langword="null" /> when it is not shown.</summary>
    private readonly ProgressBarRecorder? progressBar;

    /// <summary>The bytes moved (received plus sent) at each kept speed sample, a ring of <see cref="SpeedSampleCount" />.</summary>
    private readonly long[] sampleBytes = new long[SpeedSampleCount];

    /// <summary>The timestamp of each kept speed sample, parallel to <see cref="sampleBytes" />.</summary>
    private readonly long[] sampleTimes = new long[SpeedSampleCount];

    /// <summary>Every status line drawn, returned by <see cref="StatusLines" />.</summary>
    private readonly StringBuilder statusLines = new();

    /// <summary>Writes status lines to standard error as they are drawn, or <see langword="null" /> when the meter is not shown.</summary>
    private readonly Action<string>? writeLive;

    /// <summary>The <c>-v</c> and trace output held while the meter's end is written, or <see langword="null" /> when the run has none.</summary>
    private readonly HoldableStream? eventOutput;

    /// <summary>The transfer's share of the <c>-Z</c> run's combined meter, or <see langword="null" /> outside one.</summary>
    private readonly ParallelTransferProgress? parallelProgress;

    /// <summary>How much of <see cref="statusLines" /> has been handed out by <see cref="TakeUnwrittenStatusLines" />.</summary>
    private int writtenLength;

    /// <summary>The timestamp the Spent column counts from; kept across an authentication retry, moved by a redirect hop.</summary>
    private long start;

    /// <summary>How many speed samples the hop has taken; the ring index of the next is this modulo <see cref="SpeedSampleCount" />.</summary>
    private int sampleCount;

    /// <summary>The body bytes received this hop, as last reported.</summary>
    private long downloaded;

    /// <summary>The expected download size this hop, or <see langword="null" /> when unknown.</summary>
    private long? downloadTotal;

    /// <summary>The body bytes sent this hop, as last reported.</summary>
    private long uploaded;

    /// <summary>The expected upload size this hop, or <see langword="null" /> when unknown.</summary>
    private long? uploadTotal;

    /// <summary>The average download speed since <see cref="start" />, in bytes per second.</summary>
    private long downloadSpeed;

    /// <summary>The average upload speed since <see cref="start" />, in bytes per second.</summary>
    private long uploadSpeed;

    /// <summary>The speed over the kept samples, in bytes per second: the Curr.Speed column.</summary>
    private long currentSpeed;

    /// <summary>The time since <see cref="start" /> at the last calculation, for the Spent column.</summary>
    private long spentMicroseconds;

    /// <summary>Whether the handler reported any bytes this hop, which decides whether <see cref="Finish" /> draws.</summary>
    private bool bytesReported;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransferProgressRecorder" /> class: the
    /// transfer starts now, and the first status line, every counter zero, is drawn.
    /// </summary>
    /// <param name="timeProvider">The clock every draw reads.</param>
    /// <param name="progressBar">The <c>-#</c> bar every report is passed on to, or <see langword="null" /> without one.</param>
    /// <param name="writeLive">
    /// Writes status-line text to standard error while the transfer runs, from the first
    /// <see cref="ReportTransferStarted" /> on; <see langword="null" /> when the meter is not shown.
    /// </param>
    /// <param name="eventOutput">
    /// The stream the run's <c>-v</c> and trace lines go through, held from the handler's
    /// <see cref="ReportTransferDone" /> while the meter is written live, so the runner can write
    /// the meter's end before the lines the handler reports after it; <see langword="null" />
    /// when the run shows none.
    /// </param>
    /// <param name="parallelProgress">
    /// The transfer's share of a <c>-Z</c> run's combined meter, which every byte report is passed on to,
    /// or <see langword="null" /> outside one (task BL-521).
    /// </param>
    internal TransferProgressRecorder(
        TimeProvider timeProvider,
        ProgressBarRecorder? progressBar = null,
        Action<string>? writeLive = null,
        HoldableStream? eventOutput = null,
        ParallelTransferProgress? parallelProgress = null)
    {
        this.timeProvider = timeProvider;
        this.progressBar = progressBar;
        this.writeLive = writeLive;
        this.eventOutput = eventOutput;
        this.parallelProgress = parallelProgress;
        StartHop(restartsClock: true);
    }

    /// <summary>
    /// Gets a value indicating whether the handler reported the transfer past connect or open.
    /// </summary>
    internal bool HasTransferStarted { get; private set; }

    /// <summary>
    /// Gets the status lines drawn so far, each starting with the carriage return curl writes
    /// before it, as curl writes them to standard error. A newline
    /// (<see cref="Environment.NewLine" />) ends each hop but the last, which the caller ends.
    /// </summary>
    internal string StatusLines => statusLines.ToString();

    /// <inheritdoc />
    /// <remarks>
    /// The first report hands the live writer every line drawn so far; a repeat is the next
    /// redirect hop starting, which starts a new status line after the <c>-v</c> lines held
    /// since the hop before it reported done.
    /// </remarks>
    public void ReportTransferStarted() => StartRequest(restartsClock: true, RedirectDoneDrawCount);

    /// <inheritdoc />
    /// <remarks>
    /// Starts a new status line as a redirect hop does, but keeps the transfer's start time, so the
    /// line's Spent and average speeds count on from the first request, as curl 8.21.0 draws an
    /// authentication retry (measured, BL-2131 Notes).
    /// </remarks>
    public void ReportRequestRetried() => StartRequest(restartsClock: false, RedirectDoneDrawCount);

    /// <inheritdoc />
    /// <remarks>
    /// Draws the dead attempt's line once (<c>Curl_pgrsDone</c>'s forced update) where a retry a
    /// response asked for draws it twice, then starts a new line keeping the transfer's start
    /// time, as curl 8.22.0 draws upstream test675 (measured, BL-2200 Notes).
    /// </remarks>
    public void ReportRequestResentOnFreshConnection() => StartRequest(restartsClock: false, DiedConnectionDoneDrawCount);

    /// <summary>
    /// Handles a request starting: the first only marks the transfer started and hands out the lines drawn
    /// so far; a later one ends the hop before it (releasing the held <c>-v</c> lines, drawing it done and
    /// ending its line) and starts the next.
    /// </summary>
    /// <param name="restartsClock">Whether the new hop moves the start time to now.</param>
    /// <param name="doneDrawCount">How many done draws the hop before it gets.</param>
    private void StartRequest(bool restartsClock, int doneDrawCount)
    {
        if (HasTransferStarted)
        {
            eventOutput?.Release();
            DrawDone(doneDrawCount);
            statusLines.Append(Environment.NewLine);
            StartHop(restartsClock);
        }

        HasTransferStarted = true;
        WriteLive();
        progressBar?.ReportTransferStarted();
        parallelProgress?.ReportTransferStarted();
    }

    /// <inheritdoc />
    public void ReportDownloaded(long bytesSoFar, long? expectedTotal)
    {
        downloaded = bytesSoFar;
        downloadTotal = expectedTotal;
        bytesReported = true;
        Draw(done: false);
        WriteLive();
        progressBar?.ReportDownloaded(bytesSoFar, expectedTotal);
        parallelProgress?.ReportDownloaded(bytesSoFar, expectedTotal);
    }

    /// <inheritdoc />
    public void ReportUploaded(long bytesSoFar, long? expectedTotal)
    {
        uploaded = bytesSoFar;
        uploadTotal = expectedTotal;
        bytesReported = true;
        Draw(done: false);
        WriteLive();
        progressBar?.ReportUploaded(bytesSoFar, expectedTotal);
        parallelProgress?.ReportUploaded(bytesSoFar, expectedTotal);
    }

    /// <inheritdoc />
    /// <remarks>
    /// While the meter is written live, holds the <c>-v</c> and trace lines from here on, so the
    /// connection-end line the handler reports next comes after the meter's done status lines and
    /// their newline, which the runner writes once the handler returns with its result and then
    /// releases the lines: curl 8.21.0 draws them in <c>Curl_pgrsDone</c>, before
    /// <c>multi_done</c> reports the connection (task BL-411, ADR-0116).
    /// </remarks>
    public void ReportTransferDone()
    {
        if (writeLive is not null && HasTransferStarted)
        {
            eventOutput?.Hold();
        }
    }

    /// <summary>
    /// Takes the status lines drawn and not yet handed to the live writer: every line when
    /// there is none or the transfer never reported starting, else those drawn since the
    /// last report, such as the ones <see cref="Finish" /> draws.
    /// </summary>
    /// <returns>The lines, each starting with a carriage return, as <see cref="StatusLines" /> gives them.</returns>
    internal string TakeUnwrittenStatusLines()
    {
        string unwritten = statusLines.ToString(writtenLength, statusLines.Length - writtenLength);
        writtenLength = statusLines.Length;

        return unwritten;
    }

    /// <summary>
    /// Hands the live writer, when there is one and the transfer has reported starting, the
    /// lines drawn since it was last handed any.
    /// </summary>
    private void WriteLive()
    {
        if (writeLive is not null && HasTransferStarted && statusLines.Length > writtenLength)
        {
            writeLive(TakeUnwrittenStatusLines());
        }
    }

    /// <summary>
    /// Makes the draws curl makes as a transfer ends, when the handler reported any bytes: a
    /// transfer that succeeded is done, and is drawn <see cref="DoneDrawCount" /> times; one
    /// that failed gets only <c>Curl_pgrsDone</c>'s update, which, the transfer not being done,
    /// draws only when a second has passed since the last speed sample. A handler that
    /// reported no bytes, as <c>file://</c>'s does not, gets no more draws.
    /// </summary>
    /// <param name="succeeded">Whether the transfer succeeded.</param>
    internal void Finish(bool succeeded)
    {
        if (!bytesReported)
        {
            return;
        }

        for (int draw = 0; draw < (succeeded ? DoneDrawCount : 1); draw++)
        {
            Draw(succeeded);
        }
    }

    /// <summary>
    /// Makes the draws curl makes as a hop that answered with a redirect ends: the one before
    /// the next hop, or the one <c>--max-redirs</c> refuses to follow with exit 47.
    /// </summary>
    internal void FinishRedirectHop() => DrawDone(RedirectDoneDrawCount);

    /// <summary>Draws the status line <paramref name="count" /> times as a done transfer's.</summary>
    /// <param name="count">How many draws.</param>
    private void DrawDone(int count)
    {
        for (int draw = 0; draw < count; draw++)
        {
            Draw(done: true);
        }
    }

    /// <summary>
    /// Starts a hop now: every counter and speed sample back at zero, and the zero status line
    /// drawn. The start time moves to now only when <paramref name="restartsClock" /> says so.
    /// </summary>
    /// <param name="restartsClock">Whether the hop starts the clock again: a redirect hop does, an authentication retry does not.</param>
    private void StartHop(bool restartsClock)
    {
        if (restartsClock)
        {
            start = timeProvider.GetTimestamp();
        }

        sampleCount = 0;
        downloaded = 0;
        downloadTotal = null;
        uploaded = 0;
        uploadTotal = null;
        currentSpeed = 0;
        bytesReported = false;
        Draw(done: false);
    }

    /// <summary>
    /// Updates the speeds as <c>progress_calc</c> does and, when it says so, appends a status line.
    /// </summary>
    /// <param name="done">Whether the transfer is done (curl's <c>req.done</c>).</param>
    private void Draw(bool done)
    {
        if (Calculate(timeProvider.GetTimestamp(), done))
        {
            statusLines.Append(StatusLine());
        }
    }

    /// <summary>
    /// Updates the average and current speeds at <paramref name="now" /> as curl 8.21.0's
    /// <c>progress_calc</c> does.
    /// </summary>
    /// <param name="now">The clock's timestamp.</param>
    /// <param name="done">Whether the transfer is done.</param>
    /// <returns><see langword="true" /> when the status line is to be drawn.</returns>
    private bool Calculate(long now, bool done)
    {
        spentMicroseconds = Microseconds(start, now);
        downloadSpeed = ProgressMeterFields.Speed(downloaded, spentMicroseconds);
        uploadSpeed = ProgressMeterFields.Speed(uploaded, spentMicroseconds);
        if (sampleCount == 0)
        {
            sampleBytes[0] = downloaded + uploaded;
            sampleTimes[0] = now;
            sampleCount = 1;
            currentSpeed = uploadSpeed + downloadSpeed;

            return true;
        }

        if (!TrySample(now, done, out int latest))
        {
            return false;
        }

        currentSpeed = CurrentSpeed(latest);

        return true;
    }

    /// <summary>
    /// Takes a new speed sample when a second has passed since the latest one; else, once the
    /// transfer is done and has no current speed yet, overwrites the latest one.
    /// </summary>
    /// <param name="now">The clock's timestamp.</param>
    /// <param name="done">Whether the transfer is done.</param>
    /// <param name="latest">The index of the latest sample.</param>
    /// <returns>
    /// <see langword="false" /> while the transfer is still running and no second has passed,
    /// when curl waits for more time before drawing.
    /// </returns>
    private bool TrySample(long now, bool done, out int latest)
    {
        int next = sampleCount % SpeedSampleCount;
        latest = (next + SpeedSampleCount - 1) % SpeedSampleCount;
        if (Microseconds(sampleTimes[latest], now) >= 1000000)
        {
            sampleCount++;
            latest = next;
        }
        else if (!done)
        {
            return false;
        }
        else if (currentSpeed != 0)
        {
            return true;
        }

        sampleBytes[latest] = downloaded + uploaded;
        sampleTimes[latest] = now;

        return true;
    }

    /// <summary>
    /// Computes the current speed from the oldest kept sample to <paramref name="latest" />.
    /// </summary>
    /// <param name="latest">The index of the latest sample.</param>
    /// <returns>The speed in bytes per second.</returns>
    private long CurrentSpeed(int latest)
    {
        int oldest = sampleCount < SpeedSampleCount ? 0 : (latest + 1) % SpeedSampleCount;
        long amount = sampleBytes[latest] - sampleBytes[oldest];
        long duration = Math.Max(1, Microseconds(sampleTimes[oldest], sampleTimes[latest]));

        return amount > long.MaxValue / 1000000
            ? (long)(amount * 1000000.0 / duration)
            : amount * 1000000 / duration;
    }

    /// <summary>
    /// Formats the status line as curl 8.21.0's <c>progress_meter</c> does.
    /// </summary>
    /// <returns>The line, starting with a carriage return.</returns>
    private string StatusLine()
    {
        long spentSeconds = spentMicroseconds / 1000000;
        long downloadPercent = Estimate(downloadTotal, downloaded, downloadSpeed, out long downloadSeconds);
        long uploadPercent = Estimate(uploadTotal, uploaded, uploadSpeed, out long uploadSeconds);
        long totalSeconds = Math.Max(uploadSeconds, downloadSeconds);
        long expected = CappedSum(uploadTotal ?? uploaded, downloadTotal ?? downloaded);
        long totalPercent = ProgressMeterFields.Percent(expected, downloaded + uploaded);

        return $"\r{totalPercent,3} {ProgressMeterFields.Size(expected)} "
            + $"{downloadPercent,3} {ProgressMeterFields.Size(downloaded)} "
            + $"{uploadPercent,3} {ProgressMeterFields.Size(uploaded)} "
            + $"{ProgressMeterFields.Size(downloadSpeed)} {ProgressMeterFields.Size(uploadSpeed)} "
            + $"{ProgressMeterFields.Time(totalSeconds)} {ProgressMeterFields.Time(spentSeconds)} "
            + $"{ProgressMeterFields.Time(totalSeconds > 0 ? totalSeconds - spentSeconds : 0)} "
            + ProgressMeterFields.Size(currentSpeed);
    }

    /// <summary>
    /// Estimates one direction's percentage and total time as <c>pgrs_estimates</c> does: both
    /// zero unless its size is known and its average speed is above zero.
    /// </summary>
    /// <param name="total">The expected size, or <see langword="null" /> when unknown.</param>
    /// <param name="current">The size so far.</param>
    /// <param name="speed">The average speed.</param>
    /// <param name="seconds">The expected total time in seconds.</param>
    /// <returns>The percentage.</returns>
    private static long Estimate(long? total, long current, long speed, out long seconds)
    {
        if (total is not { } size || speed <= 0)
        {
            seconds = 0;

            return 0;
        }

        seconds = size / speed;

        return ProgressMeterFields.Percent(size, current);
    }

    /// <summary>Adds two sizes, capped at <see cref="long.MaxValue" /> as curl caps the expected total.</summary>
    /// <param name="first">The first size.</param>
    /// <param name="second">The second size.</param>
    /// <returns>The sum.</returns>
    private static long CappedSum(long first, long second) =>
        long.MaxValue - first < second ? long.MaxValue : first + second;

    /// <summary>The whole microseconds between two of the clock's timestamps.</summary>
    /// <param name="from">The earlier timestamp.</param>
    /// <param name="to">The later timestamp.</param>
    /// <returns>The microseconds.</returns>
    private long Microseconds(long from, long to) =>
        (long)timeProvider.GetElapsedTime(from, to).TotalMicroseconds;
}
