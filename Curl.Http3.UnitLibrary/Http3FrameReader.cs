namespace Curl.Http3;

/// <summary>
/// Reads HTTP/3 frames (RFC 9114 section 7) from a stream one at a time, skipping frames of
/// unknown and grease types (section 9) and refusing the reserved HTTP/2 types (section 7.2.8).
/// </summary>
/// <remarks>
/// Each frame's whole payload is read before it is returned, up to the limit the reader is
/// given; which frames may appear on which stream is the caller's to check, as
/// <see cref="Http3ControlStreamReader" /> does for the control stream.
/// </remarks>
public sealed class Http3FrameReader
{
    private const int SkipChunkLength = 4096;

    private static readonly HashSet<long> DefinedTypes =
    [
        (long)Http3FrameType.Data,
        (long)Http3FrameType.Headers,
        (long)Http3FrameType.CancelPush,
        (long)Http3FrameType.Settings,
        (long)Http3FrameType.PushPromise,
        (long)Http3FrameType.Goaway,
        (long)Http3FrameType.MaxPushId,
    ];

    private static readonly HashSet<long> ReservedHttp2Types =
    [
        (long)Http3FrameType.ReservedHttp2Priority,
        (long)Http3FrameType.ReservedHttp2Ping,
        (long)Http3FrameType.ReservedHttp2WindowUpdate,
        (long)Http3FrameType.ReservedHttp2Continuation,
    ];

    private readonly Stream stream;

    private readonly long maximumPayloadLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="Http3FrameReader" /> class.
    /// </summary>
    /// <param name="stream">The stream, positioned at a frame; on a unidirectional stream, after its type.</param>
    /// <param name="maximumPayloadLength">The longest payload of a known frame type the reader accepts.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumPayloadLength" /> is negative or above <see cref="Array.MaxLength" />.</exception>
    public Http3FrameReader(Stream stream, long maximumPayloadLength)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumPayloadLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumPayloadLength, Array.MaxLength);
        this.stream = stream;
        this.maximumPayloadLength = maximumPayloadLength;
    }

    /// <summary>
    /// Gets a value indicating whether the stream has ended, cleanly or inside a frame.
    /// </summary>
    public bool HasReachedEndOfStream { get; private set; }

    /// <summary>
    /// Reads the next frame of a type RFC 9114 defines, skipping any of other types.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The frame, or <see langword="null" /> when the stream ended between frames.</returns>
    /// <exception cref="Http3Exception">
    /// The stream ended inside a frame, or a payload is not laid out as its type requires
    /// (<see cref="Http3ErrorCode.FrameError" />); the frame is a reserved HTTP/2 type
    /// (<see cref="Http3ErrorCode.FrameUnexpected" />); its payload is over the limit
    /// (<see cref="Http3ErrorCode.ExcessiveLoad" />); or a <c>SETTINGS</c> payload is invalid
    /// (<see cref="Http3ErrorCode.SettingsError" />).
    /// </exception>
    public async ValueTask<Http3Frame?> ReadFrameAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await ReadNextDefinedFrameAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (EndOfStreamException)
        {
            HasReachedEndOfStream = true;
            throw new Http3Exception(Http3ErrorCode.FrameError, "the stream ends inside a frame");
        }
    }

    private static bool IsDefined(long type)
    {
        if (ReservedHttp2Types.Contains(type))
        {
            throw new Http3Exception(Http3ErrorCode.FrameUnexpected, $"frame type 0x{type:x} is reserved for HTTP/2");
        }

        return DefinedTypes.Contains(type);
    }

    private async ValueTask<Http3Frame?> ReadNextDefinedFrameAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            if (await Http3StreamReading.ReadVariableLengthIntegerAsync(stream, cancellationToken).ConfigureAwait(false) is not { } type)
            {
                HasReachedEndOfStream = true;
                return null;
            }

            var length = await Http3StreamReading.ReadVariableLengthIntegerAsync(stream, cancellationToken).ConfigureAwait(false)
                ?? throw new EndOfStreamException();
            if (IsDefined(type))
            {
                return await ReadPayloadAsync((Http3FrameType)type, length, cancellationToken).ConfigureAwait(false);
            }

            await SkipAsync(length, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<Http3Frame> ReadPayloadAsync(Http3FrameType type, long length, CancellationToken cancellationToken)
    {
        if (length > maximumPayloadLength)
        {
            throw new Http3Exception(
                Http3ErrorCode.ExcessiveLoad,
                $"the {type} frame's {length}-byte payload is over the {maximumPayloadLength}-byte limit");
        }

        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        return Http3Frame.Parse(type, payload);
    }

    private async ValueTask SkipAsync(long length, CancellationToken cancellationToken)
    {
        var discarded = new byte[SkipChunkLength];
        while (length > 0)
        {
            var chunk = (int)Math.Min(length, SkipChunkLength);
            await stream.ReadExactlyAsync(discarded.AsMemory(0, chunk), cancellationToken).ConfigureAwait(false);
            length -= chunk;
        }
    }
}
