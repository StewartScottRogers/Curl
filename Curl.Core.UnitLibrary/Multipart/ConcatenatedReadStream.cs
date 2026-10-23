using System.Runtime.ExceptionServices;

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

    private readonly bool[] readsOncePerFill;

    private readonly bool[] files;

    private readonly long?[] callbackLengths;

    private readonly long[] segmentReads;

    private int current;

    /// <summary>
    /// The failure of a segment read that came after bytes of the same fill: those bytes are returned first and the
    /// next read throws this, so the bytes before a refused one still go out, as with one segment per read.
    /// </summary>
    private Exception? deferredFailure;

    /// <summary>Initialises a stream over <paramref name="segments" />, which it now owns.</summary>
    /// <param name="segments">The streams to read, in order.</param>
    /// <param name="allowsSeeking">
    /// Whether it seeks when every segment seeks; a mail message of unknown size does not, so
    /// its sender knows neither its size nor how to measure it, as curl does not (BL-2025).
    /// </param>
    /// <param name="callbackSegments">
    /// The indexes of the segments that are a <c>curl_mime_data_cb</c> part's read callback, or
    /// <see langword="null" /> for none. Every read fills the buffer across segments, as libcurl's
    /// <c>mime_read</c> does (BL-3214), and with any it reads a callback segment at most once, stopping before
    /// a second, as curl 8.22.0's <c>read_part_content</c> answers <c>STOP_FILLING</c> once a buffer
    /// fill has called a read callback (upstream test645, BL-2624).
    /// </param>
    /// <param name="fileSegments">
    /// The indexes of the segments that are a file part's opened file, or <see langword="null" /> for none. While
    /// the buffer fills across segments, a file is read at most once per fill too, as libcurl reads a file part
    /// through its read function, and one that seeks is passed by in the same fill once its position reaches its
    /// length, as <c>read_part_content</c> spares the read at the part's known size (upstream test650, BL-2625).
    /// </param>
    /// <param name="callbackLengths">
    /// The known size of each callback segment that has one, by index, or <see langword="null" /> for none. Once a
    /// fill has read that many bytes from it the segment is passed in the same fill without reading it again, so it
    /// does not stop the fill, as <c>read_part_content</c> spares the read at <c>offset &gt;= datasize</c> (upstream
    /// test668, BL-2630). An encoded segment (<see cref="EncodedReadStream" />) whose data has ended is passed the
    /// same way, and one that stopped filling after its one source read ends the fill, which starts again when it
    /// had given nothing, as curl's <c>Curl_mime_read</c> loops on <c>STOP_FILLING</c> (upstream test667).
    /// </param>
    internal ConcatenatedReadStream(
        IReadOnlyList<Stream> segments,
        bool allowsSeeking = true,
        IReadOnlyCollection<int>? callbackSegments = null,
        IReadOnlyCollection<int>? fileSegments = null,
        IReadOnlyDictionary<int, long>? callbackLengths = null)
    {
        this.segments = segments;
        starts = allowsSeeking ? StartsWhenEverySegmentSeeks(segments) : null;
        files = MarkSegments(segments.Count, fileSegments ?? []);
        readsOncePerFill = ReadOnceMarks(segments.Count, callbackSegments, fileSegments);
        this.callbackLengths = LengthsByIndex(segments.Count, callbackLengths ?? new Dictionary<int, long>());
        segmentReads = new long[segments.Count];
    }

    /// <summary>One flag per segment read at most once per fill; none is set when no segment is a callback.</summary>
    private static bool[] ReadOnceMarks(int count, IReadOnlyCollection<int>? callbackSegments, IReadOnlyCollection<int>? fileSegments) =>
        MarkSegments(count, callbackSegments is { Count: > 0 } ? [.. callbackSegments, .. fileSegments ?? []] : []);

    /// <summary>One known length or <see langword="null" /> per segment, from <paramref name="lengths" />.</summary>
    private static long?[] LengthsByIndex(int count, IReadOnlyDictionary<int, long> lengths)
    {
        long?[] byIndex = new long?[count];
        foreach (KeyValuePair<int, long> length in lengths)
        {
            byIndex[length.Key] = length.Value;
        }

        return byIndex;
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

    /// <summary>
    /// Whether the segment at <paramref name="index" /> is known to have nothing left without being read: a file at
    /// its end, a callback segment that gave its known size, or an encoded segment whose data has ended.
    /// </summary>
    private bool IsAtItsEnd(int index) =>
        IsFileAtItsEnd(index) || segmentReads[index] >= (callbackLengths[index] ?? long.MaxValue) || segments[index] is EncodedReadStream { IsAtItsEnd: true };

    /// <summary>Whether reading the segment at <paramref name="index" /> counts as a fill's one callback or file read.</summary>
    private static bool CountsAsTheRead(bool[] readsOnce, int index, bool atItsEnd) => readsOnce[index] && !atItsEnd;

    /// <summary>Whether the read of the current segment just returned 0 because it stopped filling, not at its end.</summary>
    private bool StoppedFilling(int read) => read == 0 && segments[current] is EncodedReadStream { StoppedFilling: true };

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
    public override int Read(Span<byte> buffer) => Fill(buffer, readsOncePerFill);

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        FillAsync(buffer, readsOncePerFill, cancellationToken);

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <summary>Fills <paramref name="buffer" /> across segments, reading a callback or file segment at most once.</summary>
    private int Fill(Span<byte> buffer, bool[] readsOnce)
    {
        ThrowDeferredFailure();
        int filled;
        bool stopped;
        do
        {
            (filled, stopped) = FillOnce(buffer, readsOnce);
        }
        while (filled == 0 && stopped);

        return filled;
    }

    /// <summary>Throws the failure a fill deferred behind its bytes, if one did.</summary>
    private void ThrowDeferredFailure()
    {
        Exception? failure = deferredFailure;
        deferredFailure = null;
        if (failure is null)
        {
            return;
        }

        ExceptionDispatchInfo.Throw(failure);
    }

    /// <summary>One buffer fill: its byte count, and whether an encoded segment stopped it.</summary>
    private (int Filled, bool Stopped) FillOnce(Span<byte> buffer, bool[] readsOnce)
    {
        int filled = 0;
        bool calledBack = false;
        while (FillsOn(filled, buffer.Length, calledBack, readsOnce))
        {
            calledBack |= CountsAsTheRead(readsOnce, current, IsAtItsEnd(current));
            int read;
            try
            {
                read = segments[current].Read(buffer[filled..]);
            }
            catch (Exception failure) when (filled > 0)
            {
                deferredFailure = failure;
                return (filled, false);
            }

            filled += read;
            if (StoppedFilling(read))
            {
                return (filled, true);
            }

            PassSegmentWhenDone(read);
        }

        return (filled, false);
    }

    /// <summary>Whether a fill reads on: room is left, a segment is left, and it is not a second read-once segment.</summary>
    private bool FillsOn(int filled, int length, bool calledBack, bool[] readsOnce) =>
        filled < length && current < segments.Count && !(calledBack && CountsAsTheRead(readsOnce, current, IsAtItsEnd(current)));

    /// <summary>Counts the bytes read, and moves to the next segment when the current one read nothing or is at its end.</summary>
    private void PassSegmentWhenDone(int read)
    {
        segmentReads[current] += read;
        current += read == 0 || IsAtItsEnd(current) ? 1 : 0;
    }

    /// <summary>Fills <paramref name="buffer" /> across segments, reading a callback or file segment at most once.</summary>
    private async ValueTask<int> FillAsync(Memory<byte> buffer, bool[] readsOnce, CancellationToken cancellationToken)
    {
        ThrowDeferredFailure();
        int filled;
        bool stopped;
        do
        {
            (filled, stopped) = await FillOnceAsync(buffer, readsOnce, cancellationToken).ConfigureAwait(false);
        }
        while (filled == 0 && stopped);

        return filled;
    }

    /// <summary>One buffer fill: its byte count, and whether an encoded segment stopped it.</summary>
    private async ValueTask<(int Filled, bool Stopped)> FillOnceAsync(Memory<byte> buffer, bool[] readsOnce, CancellationToken cancellationToken)
    {
        int filled = 0;
        bool calledBack = false;
        while (FillsOn(filled, buffer.Length, calledBack, readsOnce))
        {
            calledBack |= CountsAsTheRead(readsOnce, current, IsAtItsEnd(current));
            int read;
            try
            {
                read = await segments[current].ReadAsync(buffer[filled..], cancellationToken).ConfigureAwait(false);
            }
            catch (Exception failure) when (filled > 0)
            {
                deferredFailure = failure;
                return (filled, false);
            }

            filled += read;
            if (StoppedFilling(read))
            {
                return (filled, true);
            }

            PassSegmentWhenDone(read);
        }

        return (filled, false);
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
            segmentReads[index] = offset;
            remaining -= offset;
        }

        deferredFailure = null;
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
