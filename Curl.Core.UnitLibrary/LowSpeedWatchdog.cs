using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Watches one transfer attempt's speed and cancels it when it stays below
/// <c>-Y</c>/<c>--speed-limit</c> for <c>-y</c>/<c>--speed-time</c>, as libcurl 8.21.0's
/// <c>Curl_speedcheck</c> does. Once a second, on a timer from the injected
/// <see cref="TimeProvider" />, it takes a sample of the bytes written to the watched output
/// and the bytes the handler reported uploaded, and measures the current speed as curl's
/// progress meter does: the faster of the two directions over the last five seconds of
/// samples (fewer at the start). The first check that finds the speed below the limit starts
/// a slow spell, a check at or above it ends the spell, and a check that finds the spell has
/// lasted the whole speed time sets <see cref="IsTooSlow" /> and cancels <see cref="Token" />.
/// The timer starts when the watchdog is created.
/// </summary>
public sealed class LowSpeedWatchdog : IDisposable
{
    /// <summary>
    /// The speed time curl 8.21.0 uses when <c>-Y</c> is given without <c>-y</c>; measured
    /// 2026-09-27 (BL-400 Notes).
    /// </summary>
    public static readonly TimeSpan DefaultSpeedTime = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The speed limit curl 8.21.0 uses when <c>-y</c> is given without <c>-Y</c>, in bytes
    /// per second; measured 2026-09-27 (BL-400 Notes).
    /// </summary>
    public const long DefaultBytesPerSecond = 1;

    /// <summary>How often the speed is checked, as curl's <c>EXPIRE_SPEEDCHECK</c> of one second.</summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How many samples the speed is measured across: the one five seconds ago and every one
    /// since, as curl's <c>CURR_TIME</c> of six.
    /// </summary>
    private const int SamplesMeasuredAcross = 6;

    private readonly Lock gate = new();

    private readonly long bytesPerSecond;

    private readonly TimeSpan speedTime;

    private readonly TimeProvider timeProvider;

    private readonly Queue<Sample> samples = new();

    private readonly CancellationTokenSource tooSlow = new();

    private readonly ITimer timer;

    private readonly IDiagnosticLog log;

    private long bytesDownloaded;

    private long bytesUploaded;

    private long? slowSince;

    private bool stopped;

    /// <summary>
    /// Initializes a new instance of the <see cref="LowSpeedWatchdog" /> class and starts its
    /// once-a-second check.
    /// </summary>
    /// <param name="bytesPerSecond">The <c>-Y</c> limit, in bytes per second; at least 1.</param>
    /// <param name="speedTime">The <c>-y</c> time the speed may stay below the limit; more than zero.</param>
    /// <param name="timeProvider">The clock the checks are taken and timed on.</param>
    /// <param name="diagnosticLog">
    /// Where the watchdog writes, component <see cref="DiagnosticLogComponents.Runner" /> (ADR-0222,
    /// BL-921): the limit armed as <c>verbose</c> and the limit hit, ending the attempt with
    /// <see cref="CurlExitCode.OperationTimedOut" />, as <c>error</c>; <see langword="null" /> for none.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="bytesPerSecond" /> is below 1, or <paramref name="speedTime" /> is not positive.
    /// </exception>
    public LowSpeedWatchdog(long bytesPerSecond, TimeSpan speedTime, TimeProvider timeProvider, IDiagnosticLog? diagnosticLog = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bytesPerSecond, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(speedTime, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.bytesPerSecond = bytesPerSecond;
        this.speedTime = speedTime;
        this.timeProvider = timeProvider;
        log = diagnosticLog ?? NoDiagnosticLog.Instance;
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            log.Write(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Runner, string.Create(
                CultureInfo.InvariantCulture,
                $"--speed-limit {bytesPerSecond} bytes/s over --speed-time {(long)speedTime.TotalSeconds} s armed"));
        }

        samples.Enqueue(new(timeProvider.GetTimestamp(), 0, 0));
        timer = timeProvider.CreateTimer(_ => CheckSpeed(), null, CheckInterval, CheckInterval);
    }

    /// <summary>Gets the token cancelled when the transfer has stayed too slow for the speed time.</summary>
    public CancellationToken Token => tooSlow.Token;

    /// <summary>Gets a value indicating whether the transfer stayed too slow for the speed time.</summary>
    public bool IsTooSlow { get; private set; }

    /// <summary>
    /// Gets the failure curl 8.21.0 ends a too-slow transfer with: exit 28 and
    /// <c>Operation too slow. Less than N bytes/sec transferred the last T seconds</c>
    /// (measured 2026-09-27, BL-400 Notes), carrying the bytes written to the watched output.
    /// </summary>
    public TransferResult Failure => TransferResult.Failure(
        CurlExitCode.OperationTimedOut,
        $"Operation too slow. Less than {bytesPerSecond} bytes/sec transferred the last {(long)speedTime.TotalSeconds} seconds",
        Interlocked.Read(ref bytesDownloaded));

    /// <summary>
    /// Starts a watchdog for curl's <c>-Y</c> and <c>-y</c> as given on the command line:
    /// <c>-Y</c> alone watches for <see cref="DefaultSpeedTime" />, <c>-y</c> alone for
    /// <see cref="DefaultBytesPerSecond" />, and a limit or time of zero, like neither option,
    /// watches nothing.
    /// </summary>
    /// <param name="bytesPerSecond">The <c>-Y</c> value, or <see langword="null" /> when not given.</param>
    /// <param name="speedTimeSeconds">The <c>-y</c> value in seconds, or <see langword="null" /> when not given.</param>
    /// <param name="timeProvider">The clock the checks are taken and timed on.</param>
    /// <param name="diagnosticLog">Where the watchdog writes; <see langword="null" /> for none.</param>
    /// <returns>The started watchdog, or <see langword="null" /> when there is nothing to watch.</returns>
    public static LowSpeedWatchdog? StartFromCommandLine(long? bytesPerSecond, long? speedTimeSeconds, TimeProvider timeProvider, IDiagnosticLog? diagnosticLog = null)
    {
        if (bytesPerSecond is null && speedTimeSeconds is null)
        {
            return null;
        }

        long limit = bytesPerSecond ?? DefaultBytesPerSecond;
        TimeSpan time = speedTimeSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : DefaultSpeedTime;
        return limit > 0 && time > TimeSpan.Zero ? new LowSpeedWatchdog(limit, time, timeProvider, diagnosticLog) : null;
    }

    /// <summary>
    /// Wraps a transfer's output so every byte written to it counts toward the download speed.
    /// Disposing the wrapper leaves <paramref name="output" /> open.
    /// </summary>
    /// <param name="output">The attempt's output.</param>
    /// <returns>The counting wrapper.</returns>
    public Stream WatchOutput(Stream output) => new CountingStream(output, this);

    /// <summary>
    /// Wraps a transfer's progress sink so the bytes the handler reports uploaded count
    /// toward the upload speed; every report still reaches <paramref name="progress" />.
    /// </summary>
    /// <param name="progress">The attempt's progress sink.</param>
    /// <returns>The watching sink.</returns>
    public ITransferProgress WatchProgress(ITransferProgress progress) => new UploadWatchingProgress(progress, this);

    /// <summary>
    /// Stops the checks. <see cref="Token" />'s source is not disposed: cancelling it may run the
    /// cancelled attempt on to the end on the timer's thread, which then disposes this
    /// watchdog, and disposing a source from inside its own cancellation is not safe.
    /// </summary>
    public void Dispose()
    {
        timer.Dispose();
        lock (gate)
        {
            stopped = true;
        }
    }

    private void CheckSpeed()
    {
        if (HasStayedTooSlow())
        {
            LogTooSlow();
            tooSlow.Cancel();
        }
    }

    private void LogTooSlow()
    {
        if (log.IsEnabled(DiagnosticLogLevel.Error))
        {
            log.Write(DiagnosticLogLevel.Error, DiagnosticLogComponents.Runner, string.Create(
                CultureInfo.InvariantCulture,
                $"--speed-limit {bytesPerSecond} bytes/s not reached for {(long)speedTime.TotalSeconds} s; the attempt ends with exit {(int)CurlExitCode.OperationTimedOut} ({CurlExitCode.OperationTimedOut})"));
        }
    }

    private bool HasStayedTooSlow()
    {
        lock (gate)
        {
            if (stopped || IsTooSlow)
            {
                return false;
            }

            long now = timeProvider.GetTimestamp();
            Sample oldest = TakeSample(now);
            if (SpeedSince(oldest, now) >= bytesPerSecond)
            {
                slowSince = null;
                return false;
            }

            slowSince ??= now;
            IsTooSlow = timeProvider.GetElapsedTime(slowSince.Value, now) >= speedTime;
            return IsTooSlow;
        }
    }

    private Sample TakeSample(long now)
    {
        samples.Enqueue(new(now, Interlocked.Read(ref bytesDownloaded), Interlocked.Read(ref bytesUploaded)));
        if (samples.Count > SamplesMeasuredAcross)
        {
            samples.Dequeue();
        }

        return samples.Peek();
    }

    private double SpeedSince(Sample oldest, long now)
    {
        long moved = Math.Max(Interlocked.Read(ref bytesDownloaded) - oldest.Downloaded, Interlocked.Read(ref bytesUploaded) - oldest.Uploaded);
        return moved / timeProvider.GetElapsedTime(oldest.Timestamp, now).TotalSeconds;
    }

    /// <summary>What had moved in each direction at one check.</summary>
    private readonly record struct Sample(long Timestamp, long Downloaded, long Uploaded);

    /// <summary>Passes every write through to the output and counts its bytes as downloaded.</summary>
    private sealed class CountingStream(Stream inner, LowSpeedWatchdog watchdog) : Stream
    {
        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => inner.CanWrite;

        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) => inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            inner.Write(buffer, offset, count);
            Interlocked.Add(ref watchdog.bytesDownloaded, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            Interlocked.Add(ref watchdog.bytesDownloaded, buffer.Length);
        }
    }

    /// <summary>Passes every report through and keeps the latest upload count.</summary>
    private sealed class UploadWatchingProgress(ITransferProgress inner, LowSpeedWatchdog watchdog) : ITransferProgress
    {
        public void ReportTransferStarted() => inner.ReportTransferStarted();

        public void ReportDownloaded(long bytesSoFar, long? expectedTotal) => inner.ReportDownloaded(bytesSoFar, expectedTotal);

        public void ReportTransferDone() => inner.ReportTransferDone();

        public void ReportUploaded(long bytesSoFar, long? expectedTotal)
        {
            Interlocked.Exchange(ref watchdog.bytesUploaded, bytesSoFar);
            inner.ReportUploaded(bytesSoFar, expectedTotal);
        }
    }
}
