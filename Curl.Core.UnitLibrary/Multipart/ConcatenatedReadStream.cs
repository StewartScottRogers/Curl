namespace Curl.Core.Multipart;

/// <summary>
/// A read-only stream that reads each of its segments to the end in turn, and disposes
/// them all when it is disposed. It seeks when every segment seeks, measuring each segment
/// from where it stood when the stream was made.
/// </summary>
/// <remarks>
/// This is how a multipart body streams its file parts instead of reading them into
/// memory: the literal header and boundary bytes are memory segments, and each file is
/// the stream <see cref="Curl.Protocol.Abstractions.IFileSystem" /> opened for it. Seeking
/// is what lets <see cref="RedirectFollower" /> send the same body again after a 307 or 308.
/// </remarks>
internal sealed class ConcatenatedReadStream : Stream
{
    private readonly IReadOnlyList<Stream> segments;

    private readonly long[]? starts;

    private int current;

    /// <summary>Initialises a stream over <paramref name="segments" />, which it now owns.</summary>
    /// <param name="segments">The streams to read, in order.</param>
    internal ConcatenatedReadStream(IReadOnlyList<Stream> segments)
    {
        this.segments = segments;
        starts = segments.All(segment => segment.CanSeek) ? [.. segments.Select(segment => segment.Position)] : null;
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => starts is not null;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => SeekableStarts.Select((start, index) => segments[index].Length - start).Sum();

    /// <inheritdoc />
    public override long Position
    {
        get => SeekableStarts.Select((start, index) => segments[index].Position - start).Sum();
        set => MoveTo(value);
    }

    private long[] SeekableStarts => starts ?? throw new NotSupportedException();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        while (buffer.Length > 0 && current < segments.Count)
        {
            int read = segments[current].Read(buffer);
            if (read > 0)
            {
                return read;
            }

            current++;
        }

        return 0;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (buffer.Length > 0 && current < segments.Count)
        {
            int read = await segments[current].ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read > 0)
            {
                return read;
            }

            current++;
        }

        return 0;
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        return Position;
    }

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>
    /// Places every segment so the next read starts <paramref name="position" /> bytes into
    /// the stream: the segments before it read to their end, the rest at their start.
    /// </summary>
    private void MoveTo(long position)
    {
        long[] segmentStarts = SeekableStarts;
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        long remaining = position;
        for (int index = 0; index < segments.Count; index++)
        {
            // A segment placed at its start needs no length, which an encoded one may have to measure.
            long offset = remaining == 0 ? 0 : Math.Min(remaining, segments[index].Length - segmentStarts[index]);
            segments[index].Position = segmentStarts[index] + offset;
            remaining -= offset;
        }

        current = 0;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The segments are disposed whatever <paramref name="disposing" /> says: this sealed type
    /// declares no finalizer, so only <see cref="Stream.Dispose()" /> ever calls this.
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
        foreach (Stream segment in segments)
        {
            segment.Dispose();
        }

        base.Dispose(disposing);
    }
}
