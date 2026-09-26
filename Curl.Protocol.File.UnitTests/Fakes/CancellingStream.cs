namespace Curl.Protocol.File.Fakes;

/// <summary>
/// A stream that cancels a token part way through a transfer, and then reports the
/// cancellation the way a real stream would — or does not report it at all, which is the
/// case the handler's own guards have to cover.
/// </summary>
/// <remarks>
/// <para>
/// This fake exists because a fake that calls
/// <see cref="CancellationToken.ThrowIfCancellationRequested" /> for itself makes every
/// cancellation test vacuous: the test passes on the fake's guard whether or not the
/// production code has one of its own. Here the token is cancelled by a chosen operation
/// — read three, write one — so cancellation arrives mid-body, exactly where a user's
/// Control-C arrives, and the operation that cancelled it then behaves as
/// <see cref="StreamCancellationStyle" /> says.
/// </para>
/// <para>
/// Nothing here touches a disk, and no instance is shared between tests: each test owns
/// its own stream and its own <see cref="CancellationTokenSource" />.
/// </para>
/// </remarks>
public sealed class CancellingStream : Stream, IRecordingStream
{
    private readonly MemoryStream inner;
    private readonly CancellationTokenSource? cancels;
    private readonly int triggerOperationNumber;
    private readonly StreamCancellationStyle style;
    private readonly bool readable;
    private readonly bool writable;
    private readonly bool seekable;
    private int reads;
    private int writes;

    private CancellingStream(
        MemoryStream inner,
        CancellationTokenSource? cancels,
        int triggerOperationNumber,
        StreamCancellationStyle style,
        bool readable,
        bool writable,
        bool seekable)
    {
        this.inner = inner;
        this.cancels = cancels;
        this.triggerOperationNumber = triggerOperationNumber;
        this.style = style;
        this.readable = readable;
        this.writable = writable;
        this.seekable = seekable;
    }

    /// <summary>
    /// Gets how many asynchronous reads were attempted, counting the one that cancelled.
    /// </summary>
    /// <remarks>
    /// This is the observable that tells the handler's loop guards apart from its stream
    /// helpers: a guard that ran stops the loop before the next read, so the count stays
    /// where the cancellation left it.
    /// </remarks>
    public int ReadCount => reads;

    /// <summary>
    /// Gets how many asynchronous writes were attempted, counting the one that cancelled.
    /// </summary>
    public int WriteCount => writes;

    /// <inheritdoc />
    public bool WasDisposed { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => readable;

    /// <inheritdoc />
    public override bool CanSeek => seekable;

    /// <inheritdoc />
    public override bool CanWrite => writable;

    /// <inheritdoc />
    public override long Length => seekable ? inner.Length : throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => seekable ? inner.Position : throw new NotSupportedException();
        set => inner.Position = seekable ? value : throw new NotSupportedException();
    }

    /// <summary>
    /// Creates a seekable readable stream over <paramref name="content" />.
    /// </summary>
    /// <param name="content">The content the reads return.</param>
    /// <param name="triggerOperationNumber">
    /// The one-based read that cancels <paramref name="cancels" />, or zero for a stream
    /// that never cancels anything.
    /// </param>
    /// <param name="style">How that read reports the cancellation.</param>
    /// <param name="cancels">
    /// The source to cancel, or <see langword="null" /> to leave the token alone — which
    /// is how a stream that cancelled itself rather than being cancelled is modelled.
    /// </param>
    /// <returns>The stream.</returns>
    public static CancellingStream Reading(
        byte[] content,
        int triggerOperationNumber,
        StreamCancellationStyle style,
        CancellationTokenSource? cancels) =>
        new(
            Buffered(content),
            cancels,
            triggerOperationNumber,
            style,
            readable: true,
            writable: false,
            seekable: true);

    /// <summary>
    /// Creates a forward-only readable stream over <paramref name="content" />, standing
    /// in for the standard input <c>-T -</c> uploads from. The handler cannot seek this
    /// one, so <c>-C</c> reaches it as a loop of reads.
    /// </summary>
    /// <param name="content">The content the reads return.</param>
    /// <param name="triggerOperationNumber">
    /// The one-based read that cancels <paramref name="cancels" />, or zero for none.
    /// </param>
    /// <param name="style">How that read reports the cancellation.</param>
    /// <param name="cancels">The source to cancel, or <see langword="null" /> for none.</param>
    /// <returns>The stream.</returns>
    public static CancellingStream ReadingNonSeekable(
        byte[] content,
        int triggerOperationNumber,
        StreamCancellationStyle style,
        CancellationTokenSource? cancels) =>
        new(
            Buffered(content),
            cancels,
            triggerOperationNumber,
            style,
            readable: true,
            writable: false,
            seekable: false);

    /// <summary>
    /// Creates a non-seekable writable stream, standing in for the standard output curl
    /// writes to by default.
    /// </summary>
    /// <param name="triggerOperationNumber">
    /// The one-based write that cancels <paramref name="cancels" />, or zero for none.
    /// </param>
    /// <param name="style">How that write reports the cancellation.</param>
    /// <param name="cancels">The source to cancel, or <see langword="null" /> for none.</param>
    /// <returns>The stream.</returns>
    public static CancellingStream Writing(
        int triggerOperationNumber,
        StreamCancellationStyle style,
        CancellationTokenSource? cancels) =>
        new(
            new MemoryStream(),
            cancels,
            triggerOperationNumber,
            style,
            readable: false,
            writable: true,
            seekable: false);

    /// <summary>
    /// Gets whatever was written before the cancellation.
    /// </summary>
    /// <returns>The written bytes.</returns>
    public byte[] ToArray() => inner.ToArray();

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        return inner.Read(buffer, offset, count);
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer) => inner.Read(buffer);

    /// <inheritdoc />
    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        reads++;

        if (reads == triggerOperationNumber)
        {
            cancels?.Cancel();

            if (style == StreamCancellationStyle.FaultedValueTask)
            {
                return ValueTask.FromCanceled<int>(cancellationToken);
            }

            if (style == StreamCancellationStyle.ThrownTaskCanceledException)
            {
                throw new TaskCanceledException();
            }
        }

        return ValueTask.FromResult(inner.Read(buffer.Span));
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) =>
        seekable ? inner.Seek(offset, origin) : throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        inner.Write(buffer, offset, count);
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer) => inner.Write(buffer);

    /// <inheritdoc />
    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        writes++;

        if (writes == triggerOperationNumber)
        {
            cancels?.Cancel();

            if (style == StreamCancellationStyle.FaultedValueTask)
            {
                return ValueTask.FromCanceled(cancellationToken);
            }

            if (style == StreamCancellationStyle.ThrownTaskCanceledException)
            {
                throw new TaskCanceledException();
            }
        }

        inner.Write(buffer.Span);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;

        base.Dispose(disposing);
    }

    private static MemoryStream Buffered(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var buffered = new MemoryStream();
        buffered.Write(content, 0, content.Length);
        buffered.Position = 0;

        return buffered;
    }
}
