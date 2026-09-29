namespace Curl.Http3;

/// <summary>
/// An HTTP/3 frame (RFC 9114 section 7): its type, and a payload each subclass lays out.
/// <see cref="ToBytes" /> writes the frame; <see cref="Http3FrameReader" /> reads one.
/// </summary>
public abstract class Http3Frame
{
    private static readonly Dictionary<Http3FrameType, PayloadParser> PayloadParsers = new()
    {
        [Http3FrameType.Data] = payload => new Http3DataFrame(payload.ToArray()),
        [Http3FrameType.Headers] = payload => new Http3HeadersFrame(payload.ToArray()),
        [Http3FrameType.CancelPush] = payload => new Http3CancelPushFrame(ReadOnlyInteger(payload, Http3FrameType.CancelPush)),
        [Http3FrameType.Settings] = Http3SettingsFrame.ParsePayload,
        [Http3FrameType.PushPromise] = Http3PushPromiseFrame.ParsePayload,
        [Http3FrameType.Goaway] = payload => new Http3GoawayFrame(ReadOnlyInteger(payload, Http3FrameType.Goaway)),
        [Http3FrameType.MaxPushId] = payload => new Http3MaxPushIdFrame(ReadOnlyInteger(payload, Http3FrameType.MaxPushId)),
    };

    private protected Http3Frame()
    {
    }

    /// <summary>Gets the frame's type.</summary>
    public abstract Http3FrameType Type { get; }

    /// <summary>
    /// Writes the frame as it goes on a stream: type, payload length, payload.
    /// </summary>
    /// <returns>The frame's bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An ID or setting is outside 0 to 2^62 - 1.</exception>
    public byte[] ToBytes()
    {
        List<byte> payload = [];
        WritePayload(payload);
        List<byte> frame = new(payload.Count + 16);
        Http3VariableLengthInteger.Write(frame, (long)Type);
        Http3VariableLengthInteger.Write(frame, payload.Count);
        frame.AddRange(payload);
        return [.. frame];
    }

    /// <summary>
    /// Interprets the payload of a frame of one of the types RFC 9114 defines.
    /// </summary>
    /// <param name="type">A defined frame type other than the reserved HTTP/2 ones.</param>
    /// <param name="payload">The whole payload.</param>
    /// <returns>The frame.</returns>
    /// <exception cref="Http3Exception">
    /// The payload holds more or less than the type's fields (<see cref="Http3ErrorCode.FrameError" />),
    /// or a <c>SETTINGS</c> payload repeats or reserves an identifier (<see cref="Http3ErrorCode.SettingsError" />).
    /// </exception>
    internal static Http3Frame Parse(Http3FrameType type, ReadOnlySpan<byte> payload) => PayloadParsers[type](payload);

    /// <summary>
    /// Reads one integer from a payload, or fails with <see cref="Http3ErrorCode.FrameError" />.
    /// </summary>
    /// <param name="payload">The payload.</param>
    /// <param name="position">Where the integer starts; afterwards, where it ended.</param>
    /// <param name="type">The frame's type, for the message.</param>
    /// <returns>The integer.</returns>
    internal static long ReadInteger(ReadOnlySpan<byte> payload, ref int position, Http3FrameType type)
    {
        if (!Http3VariableLengthInteger.TryRead(payload, ref position, out var value))
        {
            throw new Http3Exception(Http3ErrorCode.FrameError, $"the {type} frame's payload ends inside an integer");
        }

        return value;
    }

    /// <summary>
    /// Writes the fields that follow the frame's length.
    /// </summary>
    /// <param name="payload">Where the payload's bytes go.</param>
    private protected abstract void WritePayload(List<byte> payload);

    private delegate Http3Frame PayloadParser(ReadOnlySpan<byte> payload);

    private static long ReadOnlyInteger(ReadOnlySpan<byte> payload, Http3FrameType type)
    {
        var position = 0;
        var value = ReadInteger(payload, ref position, type);
        if (position != payload.Length)
        {
            throw new Http3Exception(Http3ErrorCode.FrameError, $"the {type} frame's payload runs past its integer");
        }

        return value;
    }
}
