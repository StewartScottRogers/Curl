namespace Curl.Http3;

/// <summary>
/// What one <see cref="Http3FrameReader.ReadFrameOrDataAsync" /> call read: a whole frame of a
/// type other than <c>DATA</c>, a count of <c>DATA</c> payload bytes written into the caller's
/// buffer, or the end of the stream.
/// </summary>
public sealed class Http3FrameOrData
{
    private Http3FrameOrData(Http3Frame? frame, int dataLength, bool isEndOfStream)
    {
        Frame = frame;
        DataLength = dataLength;
        IsEndOfStream = isEndOfStream;
    }

    /// <summary>Gets the result for a stream that ended between frames.</summary>
    public static Http3FrameOrData EndOfStream { get; } = new(null, 0, isEndOfStream: true);

    /// <summary>
    /// Gets the whole frame read, never a <see cref="Http3DataFrame" />, or
    /// <see langword="null" /> when <c>DATA</c> bytes were read or the stream ended.
    /// </summary>
    public Http3Frame? Frame { get; }

    /// <summary>
    /// Gets how many <c>DATA</c> payload bytes were written into the caller's buffer: at least
    /// one when <see cref="Frame" /> is <see langword="null" /> and the stream has not ended,
    /// otherwise zero.
    /// </summary>
    public int DataLength { get; }

    /// <summary>Gets a value indicating whether the stream ended between frames.</summary>
    public bool IsEndOfStream { get; }

    /// <summary>Gives the result for a whole frame read.</summary>
    /// <param name="frame">The frame.</param>
    /// <returns>The result carrying <paramref name="frame" />.</returns>
    internal static Http3FrameOrData OfFrame(Http3Frame frame) => new(frame, 0, isEndOfStream: false);

    /// <summary>Gives the result for <c>DATA</c> payload bytes written into the caller's buffer.</summary>
    /// <param name="dataLength">How many bytes were written.</param>
    /// <returns>The result carrying <paramref name="dataLength" />.</returns>
    internal static Http3FrameOrData OfData(int dataLength) => new(null, dataLength, isEndOfStream: false);
}
