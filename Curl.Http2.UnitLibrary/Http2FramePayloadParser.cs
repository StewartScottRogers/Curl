using System.Buffers.Binary;

namespace Curl.Http2;

/// <summary>
/// Reads each frame type's payload (RFC 9113 section 6) and checks the frame against the
/// rules that section sets: which stream it may be sent on, its length, and its padding.
/// Every violation is thrown as a connection error, which RFC 9113 section 5.4.1 allows
/// even where a stream error would do.
/// </summary>
public static class Http2FramePayloadParser
{
    /// <summary>
    /// Reads a DATA frame's body bytes, without padding.
    /// </summary>
    /// <param name="frame">A DATA frame.</param>
    /// <returns>The body bytes.</returns>
    /// <exception cref="Http2ProtocolException">The frame is on stream 0, or its padding is too long.</exception>
    public static ReadOnlyMemory<byte> ParseData(Http2Frame frame)
    {
        RequireStream(frame);
        return RemovePadding(frame);
    }

    /// <summary>
    /// Reads a HEADERS frame's fragment and priority fields, without padding.
    /// </summary>
    /// <param name="frame">A HEADERS frame.</param>
    /// <returns>The fragment and priority.</returns>
    /// <exception cref="Http2ProtocolException">The frame is on stream 0, too short, its padding is too long, or the stream depends on itself.</exception>
    public static Http2HeadersPayload ParseHeaders(Http2Frame frame)
    {
        RequireStream(frame);
        var content = RemovePadding(frame);
        if (!frame.HasFlag(Http2FrameFlags.Priority))
        {
            return new Http2HeadersPayload(content, null);
        }

        RequireLength(frame, content.Length >= 5);
        return new Http2HeadersPayload(content[5..], ReadPriority(frame, content.Span));
    }

    /// <summary>
    /// Reads a PRIORITY frame.
    /// </summary>
    /// <param name="frame">A PRIORITY frame.</param>
    /// <returns>The priority fields.</returns>
    /// <exception cref="Http2ProtocolException">The frame is on stream 0, not five bytes long, or the stream depends on itself.</exception>
    public static Http2Priority ParsePriority(Http2Frame frame)
    {
        RequireStream(frame);
        RequireLength(frame, frame.Payload.Length == 5);
        return ReadPriority(frame, frame.Payload.Span);
    }

    /// <summary>
    /// Reads a RST_STREAM frame's error code.
    /// </summary>
    /// <param name="frame">A RST_STREAM frame.</param>
    /// <returns>The error code.</returns>
    /// <exception cref="Http2ProtocolException">The frame is on stream 0 or not four bytes long.</exception>
    public static Http2ErrorCode ParseRstStream(Http2Frame frame)
    {
        RequireStream(frame);
        RequireLength(frame, frame.Payload.Length == 4);
        return (Http2ErrorCode)BinaryPrimitives.ReadUInt32BigEndian(frame.Payload.Span);
    }

    /// <summary>
    /// Reads a SETTINGS frame's parameters, in the order sent. An acknowledgement has none.
    /// </summary>
    /// <param name="frame">A SETTINGS frame.</param>
    /// <returns>The parameters.</returns>
    /// <exception cref="Http2ProtocolException">The frame is not on stream 0, is an acknowledgement with a payload, or its length is not a multiple of six.</exception>
    public static IReadOnlyList<Http2Setting> ParseSettings(Http2Frame frame)
    {
        RequireConnection(frame);
        var isLengthValid = frame.HasFlag(Http2FrameFlags.Acknowledgement) ? frame.Payload.IsEmpty : frame.Payload.Length % 6 == 0;
        RequireLength(frame, isLengthValid);
        var settings = new Http2Setting[frame.Payload.Length / 6];
        for (var index = 0; index < settings.Length; index++)
        {
            var entry = frame.Payload.Span.Slice(index * 6, 6);
            settings[index] = new Http2Setting((Http2SettingIdentifier)BinaryPrimitives.ReadUInt16BigEndian(entry), BinaryPrimitives.ReadUInt32BigEndian(entry[2..]));
        }

        return settings;
    }

    /// <summary>
    /// Reads a PUSH_PROMISE frame, without padding.
    /// </summary>
    /// <param name="frame">A PUSH_PROMISE frame.</param>
    /// <returns>The promised stream and fragment.</returns>
    /// <exception cref="Http2ProtocolException">The frame is on stream 0, too short, or its padding is too long.</exception>
    public static Http2PushPromisePayload ParsePushPromise(Http2Frame frame)
    {
        RequireStream(frame);
        var content = RemovePadding(frame);
        RequireLength(frame, content.Length >= 4);
        return new Http2PushPromisePayload(ReadStreamId(content.Span), content[4..]);
    }

    /// <summary>
    /// Reads a PING frame's opaque data.
    /// </summary>
    /// <param name="frame">A PING frame.</param>
    /// <returns>The eight opaque bytes, as a big-endian number.</returns>
    /// <exception cref="Http2ProtocolException">The frame is not on stream 0 or not eight bytes long.</exception>
    public static ulong ParsePing(Http2Frame frame)
    {
        RequireConnection(frame);
        RequireLength(frame, frame.Payload.Length == 8);
        return BinaryPrimitives.ReadUInt64BigEndian(frame.Payload.Span);
    }

    /// <summary>
    /// Reads a GOAWAY frame.
    /// </summary>
    /// <param name="frame">A GOAWAY frame.</param>
    /// <returns>The last stream, error code and debug data.</returns>
    /// <exception cref="Http2ProtocolException">The frame is not on stream 0 or shorter than eight bytes.</exception>
    public static Http2GoAwayPayload ParseGoAway(Http2Frame frame)
    {
        RequireConnection(frame);
        RequireLength(frame, frame.Payload.Length >= 8);
        var errorCode = (Http2ErrorCode)BinaryPrimitives.ReadUInt32BigEndian(frame.Payload.Span[4..]);
        return new Http2GoAwayPayload(ReadStreamId(frame.Payload.Span), errorCode, frame.Payload[8..]);
    }

    /// <summary>
    /// Reads a WINDOW_UPDATE frame's increment.
    /// </summary>
    /// <param name="frame">A WINDOW_UPDATE frame.</param>
    /// <returns>The increment, 1 to 2^31 - 1.</returns>
    /// <exception cref="Http2ProtocolException">The frame is not four bytes long, or its increment is 0.</exception>
    public static int ParseWindowUpdate(Http2Frame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        RequireLength(frame, frame.Payload.Length == 4);
        var increment = ReadStreamId(frame.Payload.Span);
        if (increment == 0)
        {
            throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, $"WINDOW_UPDATE on stream {frame.StreamId} has an increment of 0");
        }

        return increment;
    }

    private static void RequireStream(Http2Frame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.StreamId == 0)
        {
            throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, $"{frame.Type} frame on stream 0");
        }
    }

    private static void RequireConnection(Http2Frame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.StreamId != 0)
        {
            throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, $"{frame.Type} frame on stream {frame.StreamId}");
        }
    }

    private static void RequireLength(Http2Frame frame, bool isLengthValid)
    {
        if (!isLengthValid)
        {
            throw new Http2ProtocolException(Http2ErrorCode.FrameSizeError, $"{frame.Type} frame with a {frame.Payload.Length}-byte payload");
        }
    }

    private static ReadOnlyMemory<byte> RemovePadding(Http2Frame frame)
    {
        if (!frame.HasFlag(Http2FrameFlags.Padded))
        {
            return frame.Payload;
        }

        if (frame.Payload.IsEmpty || frame.Payload.Span[0] >= frame.Payload.Length)
        {
            throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, $"{frame.Type} frame's padding is longer than its payload");
        }

        return frame.Payload[1..^frame.Payload.Span[0]];
    }

    private static Http2Priority ReadPriority(Http2Frame frame, ReadOnlySpan<byte> fields)
    {
        var dependency = ReadStreamId(fields);
        if (dependency == frame.StreamId)
        {
            throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, $"stream {frame.StreamId} depends on itself");
        }

        return new Http2Priority(dependency, (fields[0] & 0x80) != 0, fields[4] + 1);
    }

    private static int ReadStreamId(ReadOnlySpan<byte> bytes) => BinaryPrimitives.ReadInt32BigEndian(bytes) & int.MaxValue;
}
