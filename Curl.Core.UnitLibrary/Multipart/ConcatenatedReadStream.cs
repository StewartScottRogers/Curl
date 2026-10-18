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

    private readonly bool[]? readsOncePerFill;

    private readonly bool[] files;

    private int current;

    /// <summary>Initialises a stream over <paramref name="segments" />, which it now owns.</summary>
    /// <param name="segments">The streams to read, in order.</param>
    /// <param name="allowsSeeking">
    /// Whether it seeks when every segment seeks; a mail message of unknown size does not, so
    /// its sender knows neither its size nor how to measure it, as curl does not (BL-2025).
    /// </param>
    /// <param name="callbackSegments">
    /// The indexes of the segments that are a <c>curl_mime_data_cb</c> part's read callback, or
    /// <see langword="null" /> for none. With none, each read reads one segment. With any, each read
    /// fills the buffer across segments and reads a callback segment at most once, stopping before
    /// a second, as curl 8.22.0's <c>read_part_content</c> answers <c>STOP_FILLING</c> once a buffer
    /// fill has called a read callback (upstream test645, BL-2624).
    /// </param>
    /// <param name="fileSegments">
    /// The indexes of the segments that are a file part's opened file, or <see langword="null" /> for none. While
    /// the buffer fills across segments, a file is read at most once per fill too, as libcurl reads a file part
    /// through its read function, and one that seeks is passed by in the same fill once its position reaches its
    /// length, as <c>read_part_content</c> spares the read at the part's known size (upstream test650, BL-2625).
    /// </param>
    internal ConcatenatedReadStream(IReadOnlyList<Stream> segments, bool allowsSeeking = true, IReadOnlyCollection<int>? callbackSegments = null, IReadOnlyCollection<int>? fileSegments = null)
    {
        this.segments = segments;
        starts = allowsSeeking ? StartsWhenEverySegmentSeeks(segments) : null;
        files = MarkSegments(segments.Count, fileSegments ?? []);
        readsOncePerFill = callbackSegments is { Count: > 0 } ? MarkSegments(segments.Count, [.. callbackSegments, .. fileSegments ?? []]) : null;
    }

    /// <summary>Where each segment stands now, or <see langword="null" /> when one cannot seek.</summary>
    private static long[]? StartsWhenEverySegmentSeeks(IReadOnlyList<Stream> segments) =>
        segments.All(segment => segment.CanSeek) ? [.. segments.Select(segment => segment.Position)] : null;

    /// <summary>One flag per segment, set for each index in <paramref name="indexes" />.</summary>
    private static bool[] MarkSegments(int count, IReadOnlyCollection<int> indexes)
    {
        bool[] marks = new bool[count];
        foreach (int index in indexes)
        {
            marks[index] = true;
        }

        return marks;
    }

    /// <summary>Whether the segment at <paramref name="index" /> is a file that seeks and stands at its end.</summary>
    private bool IsFileAtItsEnd(int index) => files[index] && segments[index].CanSeek && segments[index].Position >= segments[index].Length;

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
    public override int Read(Span<byte> buffer) => readsOncePerFill is null ? ReadOneSegment(buffer) : Fill(buffer, readsOncePerFill);

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        readsOncePerFill is null ? ReadOneSegmentAsync(buffer, cancellationToken) : FillAsync(buffer, readsOncePerFill, cancellationToken);

    /// <summary>Reads from the first segment with bytes left.</summary>
    private int ReadOneSegment(Span<byte> buffer)
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

    /// <summary>Reads from the first segment with bytes left.</summary>
    private async ValueTask<int> ReadOneSegmentAsync(Memory<byte> buffer, CancellationToken cancellationToken)
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

    /// <summary>Fills <paramref name="buffer" /> across segments, reading a callback or file segment at most once.</summary>
    private int Fill(Span<byte> buffer, bool[] readsOnce)
    {
        int filled = 0;
        bool calledBack = false;
        while (FillsOn(filled, buffer.Length, calledBack, readsOnce))
        {
            calledBack |= readsOnce[current];
            int read = segments[current].Read(buffer[filled..]);
            filled += read;
            PassSegmentWhenDone(read);
        }

        return filled;
    }

    /// <summary>Whether a fill reads on: room is left, a segment is left, and it is not a second read-once segment.</summary>
    private bool FillsOn(int filled, int length, bool calledBack, bool[] readsOnce) =>
        filled < length && current < segments.Count && !(calledBack && readsOnce[current]);

    /// <summary>Moves to the next segment when the current one read nothing or is a file at its end.</summary>
    private void PassSegmentWhenDone(int read) => current += read == 0 || IsFileAtItsEnd(current) ? 1 : 0;

    /// <summary>Fills <paramref name="buffer" /> across segments, reading a callback or file segment at most once.</summary>
    private async ValueTask<int> FillAsync(Memory<byte> buffer, bool[] readsOnce, CancellationToken cancellationToken)
    {
        int filled = 0;
        bool calledBack = false;
        while (FillsOn(filled, buffer.Length, calledBack, readsOnce))
        {
            calledBack |= readsOnce[current];
            int read = await segments[current].ReadAsync(buffer[filled..], cancellationToken).ConfigureAwait(false);
            filled += read;
            PassSegmentWhenDone(read);
        }

        return filled;
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
