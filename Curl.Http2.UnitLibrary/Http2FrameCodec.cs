using System.Buffers.Binary;

namespace Curl.Http2;

/// <summary>
/// Writes frames to and reads frames from a byte stream (RFC 9113 section 4.1). A frame
/// may arrive split over any number of reads; the codec waits for all of it.
/// </summary>
public static class Http2FrameCodec
{
    /// <summary>The length of a frame header: 24-bit length, type, flags and 31-bit stream identifier.</summary>
    public const int HeaderLength = 9;

    /// <summary>SETTINGS_MAX_FRAME_SIZE's initial value, and the smallest it may be (RFC 9113 section 6.5.2).</summary>
    public const int DefaultMaximumFrameSize = 16384;

    /// <summary>The largest SETTINGS_MAX_FRAME_SIZE allowed: 2^24 - 1 (RFC 9113 section 6.5.2).</summary>
    public const int LargestMaximumFrameSize = 16777215;

    /// <summary>
    /// Serializes a frame: its nine-byte header followed by its payload.
    /// </summary>
    /// <param name="frame">The frame.</param>
    /// <returns>The frame's wire bytes.</returns>
    public static byte[] Serialize(Http2Frame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(frame.Payload.Length, LargestMaximumFrameSize);
        var bytes = new byte[HeaderLength + frame.Payload.Length];
        bytes[0] = (byte)(frame.Payload.Length >> 16);
        bytes[1] = (byte)(frame.Payload.Length >> 8);
        bytes[2] = (byte)frame.Payload.Length;
        bytes[3] = (byte)frame.Type;
        bytes[4] = frame.Flags;
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(5), frame.StreamId & int.MaxValue);
        frame.Payload.Span.CopyTo(bytes.AsSpan(HeaderLength));
        return bytes;
    }

    /// <summary>
    /// Writes one frame to <paramref name="stream" /> in a single write.
    /// </summary>
    /// <param name="stream">The connection's stream.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the frame is written.</returns>
    public static async Task WriteAsync(Stream stream, Http2Frame frame, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        await stream.WriteAsync(Serialize(frame), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads one frame from <paramref name="stream" />. The reserved bit of the stream
    /// identifier is ignored, as RFC 9113 section 4.1 requires.
    /// </summary>
    /// <param name="stream">The connection's stream.</param>
    /// <param name="maximumFrameSize">The largest payload this endpoint accepts: its SETTINGS_MAX_FRAME_SIZE.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The frame, or <see langword="null" /> when the stream ended before a frame began.</returns>
    /// <exception cref="Http2ProtocolException">The frame's length exceeds <paramref name="maximumFrameSize" /> (FRAME_SIZE_ERROR).</exception>
    /// <exception cref="EndOfStreamException">The stream ended part way through a frame.</exception>
    public static async Task<Http2Frame?> ReadAsync(Stream stream, int maximumFrameSize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var header = new byte[HeaderLength];
        var received = await stream.ReadAtLeastAsync(header, HeaderLength, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        if (received == 0)
        {
            return null;
        }

        if (received < HeaderLength)
        {
            throw new EndOfStreamException("The connection closed part way through an HTTP/2 frame header.");
        }

        var length = (header[0] << 16) | (header[1] << 8) | header[2];
        if (length > maximumFrameSize)
        {
            throw new Http2ProtocolException(Http2ErrorCode.FrameSizeError, $"a frame of {length} bytes exceeds the maximum frame size of {maximumFrameSize}");
        }

        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        var streamId = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(5)) & int.MaxValue;
        return new Http2Frame((Http2FrameType)header[3], header[4], streamId, payload);
    }
}
