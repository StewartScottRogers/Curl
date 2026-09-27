namespace Curl.Core;

/// <summary>
/// Wraps a transfer's stream and holds its throughput at <c>--limit-rate</c>, as curl
/// 8.21.0 does: no single read or write moves more than one second's worth of bytes, and
/// before each one it waits, with <see cref="Task.Delay(TimeSpan, TimeProvider, CancellationToken)" />
/// on the injected <see cref="TimeProvider" />, until the bytes already moved would have
/// taken that long at the rate. Reads and writes share one count and one clock, which
/// starts when the stream is constructed. Only the asynchronous members move bytes; the
/// synchronous <see cref="Read(byte[], int, int)" /> and <see cref="Write(byte[], int, int)" />
/// throw, because waiting there would block a thread. Disposing it disposes the wrapped stream.
/// </summary>
public sealed class RateLimitedStream : Stream
{
    private readonly Stream inner;

    private readonly long bytesPerSecond;

    private readonly TimeProvider timeProvider;

    private readonly long startTimestamp;

    private long bytesMoved;

    /// <summary>Initializes a new instance of the <see cref="RateLimitedStream" /> class.</summary>
    /// <param name="inner">The stream whose reads and writes are limited.</param>
    /// <param name="bytesPerSecond">The <c>--limit-rate</c>, in bytes per second; at least 1.</param>
    /// <param name="timeProvider">The clock the waits are measured and taken on.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bytesPerSecond" /> is below 1.</exception>
    public RateLimitedStream(Stream inner, long bytesPerSecond, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfLessThan(bytesPerSecond, 1);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.inner = inner;
        this.bytesPerSecond = bytesPerSecond;
        this.timeProvider = timeProvider;
        startTimestamp = timeProvider.GetTimestamp();
    }

    /// <inheritdoc />
    public override bool CanRead => inner.CanRead;

    /// <inheritdoc />
    /// <remarks>Always <see langword="false" />: seeking would break the rate's byte count.</remarks>
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => inner.CanWrite;

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; the stream does not seek.</exception>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; the stream does not seek.</exception>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Flush() => inner.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; use <see cref="ReadAsync(Memory{byte}, CancellationToken)" />.</exception>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; use <see cref="WriteAsync(ReadOnlyMemory{byte}, CancellationToken)" />.</exception>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; the stream does not seek.</exception>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; the stream does not seek.</exception>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    /// <remarks>Reads at most one second's worth of bytes, after waiting for the rate.</remarks>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await WaitForRateAsync(cancellationToken).ConfigureAwait(false);
        int read = await inner.ReadAsync(buffer[..OneSecondOf(buffer.Length)], cancellationToken).ConfigureAwait(false);
        bytesMoved += read;
        return read;
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    /// <remarks>Writes in pieces of at most one second's worth of bytes, waiting for the rate before each.</remarks>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (!buffer.IsEmpty)
        {
            await WaitForRateAsync(cancellationToken).ConfigureAwait(false);
            ReadOnlyMemory<byte> piece = buffer[..OneSecondOf(buffer.Length)];
            await inner.WriteAsync(piece, cancellationToken).ConfigureAwait(false);
            bytesMoved += piece.Length;
            buffer = buffer[piece.Length..];
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        inner.Dispose();
        base.Dispose(disposing);
    }

    private int OneSecondOf(int length) => (int)Math.Min(length, bytesPerSecond);

    private Task WaitForRateAsync(CancellationToken cancellationToken)
    {
        TimeSpan due = TimeSpan.FromSeconds((double)bytesMoved / bytesPerSecond);
        TimeSpan wait = due - timeProvider.GetElapsedTime(startTimestamp);
        return wait > TimeSpan.Zero
            ? Task.Delay(wait, timeProvider, cancellationToken)
            : Task.CompletedTask;
    }
}
