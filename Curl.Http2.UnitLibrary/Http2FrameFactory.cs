using System.Buffers.Binary;

namespace Curl.Http2;

/// <summary>
/// Creates each frame type of RFC 9113 section 6 with its payload laid out on the wire.
/// A pad length of zero leaves the frame unpadded; any other length sets PADDED and
/// appends that many zero bytes.
/// </summary>
public static class Http2FrameFactory
{
    /// <summary>
    /// Creates a DATA frame (RFC 9113 section 6.1).
    /// </summary>
    /// <param name="streamId">The stream.</param>
    /// <param name="data">The body bytes.</param>
    /// <param name="isEndStream">Whether to set END_STREAM.</param>
    /// <param name="padLength">How many padding bytes to add, 0 to 255.</param>
    /// <returns>The frame.</returns>
    public static Http2Frame CreateData(int streamId, ReadOnlyMemory<byte> data, bool isEndStream, int padLength = 0)
    {
        var flags = isEndStream ? Http2FrameFlags.EndStream : Http2FrameFlags.None;
        return Padded(Http2FrameType.Data, flags, streamId, data.Span, padLength);
    }

    /// <summary>
    /// Creates a HEADERS frame (RFC 9113 section 6.2).
    /// </summary>
    /// <param name="streamId">The stream.</param>
    /// <param name="fragment">The header block fragment.</param>
    /// <param name="isEndStream">Whether to set END_STREAM.</param>
    /// <param name="isEndHeaders">Whether to set END_HEADERS.</param>
    /// <param name="priority">The priority fields to include, which sets PRIORITY; <see langword="null" /> for none.</param>
    /// <param name="padLength">How many padding bytes to add, 0 to 255.</param>
    /// <returns>The frame.</returns>
    public static Http2Frame CreateHeaders(int streamId, ReadOnlyMemory<byte> fragment, bool isEndStream, bool isEndHeaders, Http2Priority? priority = null, int padLength = 0)
    {
        var flags = (byte)((isEndStream ? Http2FrameFlags.EndStream : 0) | (isEndHeaders ? Http2FrameFlags.EndHeaders : 0));
        var content = fragment.ToArray();
        if (priority is { } fields)
        {
            flags |= Http2FrameFlags.Priority;
            content = [.. PriorityBytes(fields), .. content];
        }

        return Padded(Http2FrameType.Headers, flags, streamId, content, padLength);
    }

    /// <summary>
    /// Creates a PRIORITY frame (RFC 9113 section 6.3).
    /// </summary>
    /// <param name="streamId">The stream.</param>
    /// <param name="priority">The priority fields.</param>
    /// <returns>The frame.</returns>
    public static Http2Frame CreatePriority(int streamId, Http2Priority priority) =>
        new(Http2FrameType.Priority, Http2FrameFlags.None, streamId, PriorityBytes(priority));

    /// <summary>
    /// Creates a RST_STREAM frame (RFC 9113 section 6.4).
    /// </summary>
    /// <param name="streamId">The stream to end.</param>
    /// <param name="errorCode">Why.</param>
    /// <returns>The frame.</returns>
    public static Http2Frame CreateRstStream(int streamId, Http2ErrorCode errorCode) =>
        new(Http2FrameType.RstStream, Http2FrameFlags.None, streamId, UInt32Bytes((uint)errorCode));

    /// <summary>
    /// Creates a SETTINGS frame carrying <paramref name="settings" /> in order (RFC 9113 section 6.5).
    /// </summary>
    /// <param name="settings">The parameters.</param>
    /// <returns>The frame.</returns>
    public static Http2Frame CreateSettings(IReadOnlyList<Http2Setting> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var payload = new byte[settings.Count * 6];
        for (var index = 0; index < settings.Count; index++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(index * 6), (ushort)settings[index].Identifier);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan((index * 6) + 2), settings[index].Value);
        }

        return new Http2Frame(Http2FrameType.Settings, Http2FrameFlags.None, 0, payload);
    }

    /// <summary>
    /// Creates the empty SETTINGS frame with ACK that acknowledges the peer's SETTINGS.
    /// </summary>
    /// <returns>The frame.</returns>
    public static Http2Frame CreateSettingsAcknowledgement() =>
        new(Http2FrameType.Settings, Http2FrameFlags.Acknowledgement, 0, ReadOnlyMemory<byte>.Empty);

    /// <summary>
    /// Creates a PUSH_PROMISE frame (RFC 9113 section 6.6).
    /// </summary>
    /// <param name="streamId">The stream the promise is made on.</param>
    /// <param name="promisedStreamId">The stream reserved for the push.</param>
    /// <param name="fragment">The header block fragment.</param>
    /// <param name="isEndHeaders">Whether to set END_HEADERS.</param>
    /// <param name="padLength">How many padding bytes to add, 0 to 255.</param>
    /// <returns>The frame.</returns>
    public static Http2Frame CreatePushPromise(int streamId, int promisedStreamId, ReadOnlyMemory<byte> fragment, bool isEndHeaders, int padLength = 0)
    {
        var flags = isEndHeaders ? Http2FrameFlags.EndHeaders : Http2FrameFlags.None;
        byte[] content = [.. UInt32Bytes((uint)promisedStreamId & int.MaxValue), .. fragment.Span];
        return Padded(Http2FrameType.PushPromise, flags, streamId, content, padLength);
    }

    /// <summary>
    /// Creates a PING frame (RFC 9113 section 6.7).
    /// </summary>
    /// <param name="opaqueData">The eight opaque bytes, as a big-endian number.</param>
    /// <param name="isAcknowledgement">Whether to set ACK, answering the peer's PING.</param>
    /// <returns>The frame.</returns>
    public static Http2Frame CreatePing(ulong opaqueData, bool isAcknowledgement)
    {
        var payload = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(payload, opaqueData);
        var flags = isAcknowledgement ? Http2FrameFlags.Acknowledgement : Http2FrameFlags.None;
        return new Http2Frame(Http2FrameType.Ping, flags, 0, payload);
    }

    /// <summary>
    /// Creates a GOAWAY frame (RFC 9113 section 6.8).
    /// </summary>
    /// <param name="lastStreamId">The highest peer-initiated stream this endpoint processed.</param>
    /// <param name="errorCode">Why the connection is closing.</param>
    /// <param name="debugData">Opaque diagnostic bytes.</param>
    /// <returns>The frame.</returns>
    public static Http2Frame CreateGoAway(int lastStreamId, Http2ErrorCode errorCode, ReadOnlyMemory<byte> debugData)
    {
        byte[] payload = [.. UInt32Bytes((uint)lastStreamId & int.MaxValue), .. UInt32Bytes((uint)errorCode), .. debugData.Span];
        return new Http2Frame(Http2FrameType.GoAway, Http2FrameFlags.None, 0, payload);
    }

    /// <summary>
    /// Creates a WINDOW_UPDATE frame (RFC 9113 section 6.9).
    /// </summary>
    /// <param name="streamId">The stream, or 0 for the connection's window.</param>
    /// <param name="increment">How many bytes to grow the window by, 1 to 2^31 - 1.</param>
    /// <returns>The frame.</returns>
    public static Http2Frame CreateWindowUpdate(int streamId, int increment) =>
        new(Http2FrameType.WindowUpdate, Http2FrameFlags.None, streamId, UInt32Bytes((uint)increment));

    /// <summary>
    /// Creates a CONTINUATION frame (RFC 9113 section 6.10).
    /// </summary>
    /// <param name="streamId">The stream.</param>
    /// <param name="fragment">The next header block fragment.</param>
    /// <param name="isEndHeaders">Whether to set END_HEADERS.</param>
    /// <returns>The frame.</returns>
    public static Http2Frame CreateContinuation(int streamId, ReadOnlyMemory<byte> fragment, bool isEndHeaders)
    {
        var flags = isEndHeaders ? Http2FrameFlags.EndHeaders : Http2FrameFlags.None;
        return new Http2Frame(Http2FrameType.Continuation, flags, streamId, fragment.ToArray());
    }

    private static Http2Frame Padded(Http2FrameType type, byte flags, int streamId, ReadOnlySpan<byte> content, int padLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(padLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(padLength, byte.MaxValue);
        if (padLength == 0)
        {
            return new Http2Frame(type, flags, streamId, content.ToArray());
        }

        var payload = new byte[1 + content.Length + padLength];
        payload[0] = (byte)padLength;
        content.CopyTo(payload.AsSpan(1));
        return new Http2Frame(type, (byte)(flags | Http2FrameFlags.Padded), streamId, payload);
    }

    private static byte[] PriorityBytes(Http2Priority priority)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(priority.Weight, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(priority.Weight, 256);
        var exclusiveBit = priority.IsExclusive ? 0x80000000u : 0u;
        return [.. UInt32Bytes(((uint)priority.StreamDependency & int.MaxValue) | exclusiveBit), (byte)(priority.Weight - 1)];
    }

    private static byte[] UInt32Bytes(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }
}
