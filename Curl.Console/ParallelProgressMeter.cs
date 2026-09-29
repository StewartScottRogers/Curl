using Curl.Output;

namespace Curl.Console;

/// <summary>
/// curl 8.21.0's combined progress meter for a <c>-Z</c> run, in place of each transfer's own
/// (<c>progress_meter</c> in <c>src/tool_progress.c</c>; ADR-0155, BL-521): the header line with the
/// first draw, then a status line of the run's totals whenever the run's loop would draw one - on a
/// byte report, a transfer going live or ending, the runner having started every transfer it can,
/// and a second without any of those - but only once more than 500 ms have passed since the last,
/// and a final line, ended by a line ending, as the run ends.
/// </summary>
/// <remarks>
/// Every draw is made holding the run's <see cref="WriteGate" />, and the figures are read under the
/// meter's own lock, which byte reports take alone, so a draw never splits another write.
/// </remarks>
internal sealed class ParallelProgressMeter : IDisposable
{
    /// <summary>How many speed samples curl keeps (<c>SPEEDCNT</c>).</summary>
    private const int SpeedSampleCount = 10;

    /// <summary>How long after the last draw curl draws again, at the earliest, in milliseconds.</summary>
    private const long DrawIntervalMilliseconds = 500;

    /// <summary>How long curl's loop waits for activity before it draws anyway (its poll timeout).</summary>
    private static readonly TimeSpan IdleDrawDelay = TimeSpan.FromSeconds(1);

    private readonly TimeProvider timeProvider;

    private readonly WriteGate writeGate;

    private readonly Action<string> write;

    private readonly ITimer idleDraw;

    private readonly object figuresLock = new();

    private readonly List<ParallelTransferProgress> transfers = [];

    private readonly (long Downloaded, long Uploaded, long Time)[] speedSamples = new (long, long, long)[SpeedSampleCount];

    private readonly long start;

    private long? lastDraw;

    private int nextSpeedSample;

    private bool speedSamplesWrapped;

    private long transferCount;

    private long endedDownloaded;

    private long endedUploaded;

    private long downloadTotal;

    private long uploadTotal;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParallelProgressMeter" /> class: the run starts now,
    /// and nothing is drawn until the first <see cref="DrawIfDue" />.
    /// </summary>
    /// <param name="timeProvider">The clock every draw reads, and the idle draw's timer runs on.</param>
    /// <param name="writeGate">The run's gate, held for every draw.</param>
    /// <param name="write">Writes the meter's text to standard error.</param>
    internal ParallelProgressMeter(TimeProvider timeProvider, WriteGate writeGate, Action<string> write)
    {
        this.timeProvider = timeProvider;
        this.writeGate = writeGate;
        this.write = write;
        start = timeProvider.GetTimestamp();
        idleDraw = timeProvider.CreateTimer(_ => DrawIfDue(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Counts a transfer the run takes on, not yet live.</summary>
    /// <returns>The sink its handler's reports go to, and whose live span the runner marks.</returns>
    internal ParallelTransferProgress AddTransfer()
    {
        lock (figuresLock)
        {
            ParallelTransferProgress transfer = new(this, figuresLock);
            transfers.Add(transfer);
            transferCount++;

            return transfer;
        }
    }

    /// <summary>
    /// Ends a transfer: its bytes and sizes count on among the ended transfers', it no longer counts
    /// as live, and a status line is drawn when due.
    /// </summary>
    /// <param name="transfer">The transfer, as <see cref="AddTransfer" /> gave it.</param>
    internal void EndTransfer(ParallelTransferProgress transfer)
    {
        lock (figuresLock)
        {
            transfers.Remove(transfer);
            endedDownloaded += transfer.Downloaded;
            endedUploaded += transfer.Uploaded;
            AddTotals(transfer);
        }

        DrawIfDue();
    }

    /// <summary>
    /// Draws a status line, after the header line the first time, when more than 500 ms have passed
    /// since the last draw or none has been made, then waits a second for activity before the next.
    /// </summary>
    internal void DrawIfDue() => writeGate.RunExclusive(() => Draw(final: false));

    /// <summary>
    /// Stops the idle draw and draws the final status line, ended by <see cref="Environment.NewLine" />,
    /// whenever the last was drawn.
    /// </summary>
    internal void DrawFinal()
    {
        idleDraw.Dispose();
        writeGate.RunExclusive(() => Draw(final: true));
    }

    /// <inheritdoc />
    public void Dispose() => idleDraw.Dispose();

    /// <summary>Draws a status line, holding the run's gate, as <see cref="DrawIfDue" /> and <see cref="DrawFinal" /> describe.</summary>
    /// <param name="final">Whether this is the run's final line, which is always drawn.</param>
    private void Draw(bool final)
    {
        string text;
        lock (figuresLock)
        {
            long now = timeProvider.GetTimestamp();
            if (!final && !IsDue(now))
            {
                return;
            }

            text = (lastDraw is null ? ParallelProgressMeterText.HeaderLine + Environment.NewLine : string.Empty)
                + ParallelProgressMeterText.StatusLine(Figures(now), final ? Environment.NewLine : null);
            lastDraw = now;
        }

        idleDraw.Change(final ? Timeout.InfiniteTimeSpan : IdleDrawDelay, Timeout.InfiniteTimeSpan);
        write(text);
    }

    /// <summary>Tells whether a line is due: none has been drawn, or the last more than 500 ms ago.</summary>
    /// <param name="now">The clock's timestamp.</param>
    /// <returns><see langword="true" /> when a line is due.</returns>
    private bool IsDue(long now) => lastDraw is not { } last || Milliseconds(last, now) > DrawIntervalMilliseconds;

    /// <summary>Computes the figures at <paramref name="now" />, holding the lock, as <c>progress_meter</c> does.</summary>
    /// <param name="now">The clock's timestamp.</param>
    /// <returns>The figures.</returns>
    private ParallelProgressFigures Figures(long now)
    {
        RunningTotals running = SumRunningTransfers();
        long downloaded = endedDownloaded + running.Downloaded;
        long uploaded = endedUploaded + running.Uploaded;
        long speed = Speed(now, downloaded, uploaded);
        bool estimated = running.DownloadKnown && speed > 0;

        return new ParallelProgressFigures(
            Percent(running.DownloadKnown, downloaded, downloadTotal),
            Percent(running.UploadKnown, uploaded, uploadTotal),
            downloaded,
            uploaded,
            transferCount,
            running.Live,
            estimated ? downloadTotal / speed : 0,
            Milliseconds(start, now) / 1000,
            estimated ? (downloadTotal - downloaded) / speed : 0,
            speed);
    }

    /// <summary>
    /// Sums the transfers not yet ended, adding each one's newly known sizes to the run's totals, as
    /// <c>progress_meter</c>'s loop over the transfers does.
    /// </summary>
    /// <returns>Their bytes, whether every one's sizes are known, and how many are live.</returns>
    private RunningTotals SumRunningTransfers()
    {
        RunningTotals totals = new(0, 0, true, true, 0);
        foreach (ParallelTransferProgress transfer in transfers)
        {
            AddTotals(transfer);
            totals = new RunningTotals(
                totals.Downloaded + transfer.Downloaded,
                totals.Uploaded + transfer.Uploaded,
                totals.DownloadKnown && transfer.DownloadTotal != 0,
                totals.UploadKnown && transfer.UploadTotal != 0,
                totals.Live + (transfer.IsLive ? 1 : 0));
        }

        return totals;
    }

    /// <summary>A share of a total as <c>progress_meter</c> computes it: unknown unless every size is known and the total is not zero.</summary>
    /// <param name="known">Whether every transfer's size is known.</param>
    /// <param name="current">The bytes so far.</param>
    /// <param name="total">The run's total size.</param>
    /// <returns>The percentage, or <see langword="null" /> when unknown.</returns>
    private static long? Percent(bool known, long current, long total) =>
        known && total != 0 ? current * 100 / total : null;

    /// <summary>
    /// Adds a transfer's known sizes to the run's totals, each once, as curl adds <c>dltotal</c> and
    /// <c>ultotal</c> once they are known.
    /// </summary>
    /// <param name="transfer">The transfer.</param>
    private void AddTotals(ParallelTransferProgress transfer)
    {
        if (transfer.DownloadTotal != 0 && !transfer.DownloadTotalAdded)
        {
            downloadTotal += transfer.DownloadTotal;
            transfer.DownloadTotalAdded = true;
        }

        if (transfer.UploadTotal != 0 && !transfer.UploadTotalAdded)
        {
            uploadTotal += transfer.UploadTotal;
            transfer.UploadTotalAdded = true;
        }
    }

    /// <summary>
    /// Stores a speed sample and computes the current speed as <c>progress_meter</c> does: over the
    /// last ten samples once there are ten, else since the run started; the higher direction's.
    /// </summary>
    /// <param name="now">The clock's timestamp.</param>
    /// <param name="downloaded">The bytes received so far.</param>
    /// <param name="uploaded">The bytes sent so far.</param>
    /// <returns>The speed in bytes per second.</returns>
    private long Speed(long now, long downloaded, long uploaded)
    {
        speedSamples[nextSpeedSample] = (downloaded, uploaded, now);
        nextSpeedSample = (nextSpeedSample + 1) % SpeedSampleCount;
        speedSamplesWrapped |= nextSpeedSample == 0;
        (long Downloaded, long Uploaded, long Time) since = speedSamplesWrapped ? speedSamples[nextSpeedSample] : (0, 0, start);
        double seconds = Math.Max(1, Milliseconds(since.Time, now)) / 1000.0;

        return Math.Max((long)((downloaded - since.Downloaded) / seconds), (long)((uploaded - since.Uploaded) / seconds));
    }

    /// <summary>The whole milliseconds between two of the clock's timestamps.</summary>
    /// <param name="from">The earlier timestamp.</param>
    /// <param name="to">The later timestamp.</param>
    /// <returns>The milliseconds.</returns>
    private long Milliseconds(long from, long to) => (long)timeProvider.GetElapsedTime(from, to).TotalMilliseconds;

    /// <summary>The sums over the transfers not yet ended.</summary>
    /// <param name="Downloaded">Their bytes received.</param>
    /// <param name="Uploaded">Their bytes sent.</param>
    /// <param name="DownloadKnown">Whether every one's download size is known.</param>
    /// <param name="UploadKnown">Whether every one's upload size is known.</param>
    /// <param name="Live">How many are live.</param>
    private readonly record struct RunningTotals(long Downloaded, long Uploaded, bool DownloadKnown, bool UploadKnown, long Live);
}
