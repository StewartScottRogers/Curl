using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// One transfer's share of a <c>-Z</c> run's <see cref="ParallelProgressMeter" />: the bytes and sizes
/// its handler reports, as curl's <c>xferinfo_cb</c> keeps them on its <c>per_transfer</c>, and whether
/// it is live. Each report asks the meter to draw, as activity wakes curl's loop.
/// </summary>
/// <param name="meter">The run's meter.</param>
/// <param name="figuresLock">The meter's lock, taken for every change.</param>
internal sealed class ParallelTransferProgress(ParallelProgressMeter meter, object figuresLock) : ITransferProgress
{
    /// <summary>Gets the bytes received so far.</summary>
    internal long Downloaded { get; private set; }

    /// <summary>Gets the expected download size; zero while unknown, as curl's <c>dltotal</c>.</summary>
    internal long DownloadTotal { get; private set; }

    /// <summary>Gets the bytes sent so far.</summary>
    internal long Uploaded { get; private set; }

    /// <summary>Gets the expected upload size; zero while unknown, as curl's <c>ultotal</c>.</summary>
    internal long UploadTotal { get; private set; }

    /// <summary>Gets or sets a value indicating whether the meter's run total holds <see cref="DownloadTotal" />.</summary>
    internal bool DownloadTotalAdded { get; set; }

    /// <summary>Gets or sets a value indicating whether the meter's run total holds <see cref="UploadTotal" />.</summary>
    internal bool UploadTotalAdded { get; set; }

    /// <summary>Gets a value indicating whether the transfer is running, as curl's <c>added</c>.</summary>
    internal bool IsLive { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the transfer is binding its <c>--interface</c> and not yet past
    /// connect, which blocks curl's loop, so the meter draws nothing until it ends or reports itself
    /// started (AF-0237, ADR-0661).
    /// </summary>
    internal bool HoldsTheLoop { get; private set; }

    /// <summary>
    /// Marks the transfer running, once it may connect. Nothing is drawn: curl draws once it has added
    /// every transfer it can, which the runner says (<see cref="ParallelRun.WaitForFreeSlotAsync" />).
    /// </summary>
    /// <param name="bindsInterface">
    /// Whether <c>--interface</c> was given, so the transfer holds the meter's draws until it is past
    /// connect (<see cref="HoldsTheLoop" />).
    /// </param>
    internal void MarkLive(bool bindsInterface = false)
    {
        lock (figuresLock)
        {
            IsLive = true;
            HoldsTheLoop = bindsInterface;
        }
    }

    /// <summary>Ends the transfer on the meter (<see cref="ParallelProgressMeter.EndTransfer" />).</summary>
    internal void End() => meter.EndTransfer(this);

    /// <inheritdoc />
    /// <remarks>
    /// A transfer that held the meter's draws while it bound <c>--interface</c> lets them go, and a line
    /// is drawn when due, as curl's loop draws once its blocking connect step returns.
    /// </remarks>
    public void ReportTransferStarted()
    {
        bool released;
        lock (figuresLock)
        {
            released = HoldsTheLoop;
            HoldsTheLoop = false;
        }

        if (released)
        {
            meter.DrawIfDue();
        }
    }

    /// <inheritdoc />
    public void ReportDownloaded(long bytesSoFar, long? expectedTotal)
    {
        lock (figuresLock)
        {
            Downloaded = bytesSoFar;
            DownloadTotal = expectedTotal ?? 0;
        }

        meter.DrawIfDue();
    }

    /// <inheritdoc />
    public void ReportUploaded(long bytesSoFar, long? expectedTotal)
    {
        lock (figuresLock)
        {
            Uploaded = bytesSoFar;
            UploadTotal = expectedTotal ?? 0;
        }

        meter.DrawIfDue();
    }

    /// <inheritdoc />
    public void ReportTransferDone()
    {
        // Nothing to record: the runner ends the transfer on the meter through End, once its reports are
        // written, as curl's tool ends a parallel transfer only when its message arrives.
    }
}
