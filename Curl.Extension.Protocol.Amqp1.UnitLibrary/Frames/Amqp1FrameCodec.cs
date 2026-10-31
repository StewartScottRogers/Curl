using System.Buffers.Binary;

namespace Curl.Extension.Protocol.Amqp1.Frames;

/// <summary>
/// Writes and reads the frame layout of Part 2 section 2.3: a four-byte SIZE, a one-byte DOFF counting four-byte
/// words to the body, a one-byte TYPE, two type-specific bytes (the channel of an AMQP frame), an extended header of
/// DOFF times four less eight bytes, which is skipped, and the body.
/// </summary>
internal static class Amqp1FrameCodec
{
    /// <summary>The size of the fixed frame header, and so the smallest frame.</summary>
    public const int HeaderLength = 8;

    /// <summary>The frame's bytes, with DOFF 2 and no extended header.</summary>
    public static byte[] Write(Amqp1Frame frame)
    {
        var bytes = new byte[HeaderLength + frame.Body.Length];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)bytes.Length);
        bytes[4] = 2;
        bytes[5] = frame.Type;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(6), frame.Channel);
        frame.Body.Span.CopyTo(bytes.AsSpan(HeaderLength));
        return bytes;
    }

    /// <summary>
    /// Reads the frame at the start of <paramref name="buffer"/>: false while the buffer holds less than the whole
    /// frame; otherwise the frame, its body a slice of <paramref name="buffer"/>, and how many bytes it took.
    /// Raises <see cref="Amqp1MalformedEncodingException"/> for a SIZE below 8, a DOFF below 2 or past SIZE, or a
    /// TYPE other than AMQP or SASL.
    /// </summary>
    public static bool TryRead(ReadOnlyMemory<byte> buffer, out Amqp1Frame? frame, out int consumed)
    {
        frame = null;
        consumed = 0;
        if (buffer.Length < 4)
        {
            return false;
        }

        var span = buffer.Span;
        var size = BinaryPrimitives.ReadUInt32BigEndian(span);
        if (size < HeaderLength)
        {
            throw new Amqp1MalformedEncodingException($"An AMQP frame is at least {HeaderLength} bytes, not {size}.");
        }

        if (size > (uint)buffer.Length)
        {
            return false;
        }

        var bodyOffset = RequireBodyOffset(span[4], (int)size);
        var type = span[5];
        if (type is not (Amqp1Frame.AmqpType or Amqp1Frame.SaslType))
        {
            throw new Amqp1MalformedEncodingException($"Frame type 0x{type:x2} is neither AMQP nor SASL.");
        }

        frame = new Amqp1Frame(type, BinaryPrimitives.ReadUInt16BigEndian(span[6..]), buffer[bodyOffset..(int)size]);
        consumed = (int)size;
        return true;
    }

    private static int RequireBodyOffset(byte dataOffset, int size) =>
        dataOffset >= 2 && dataOffset * 4 <= size
            ? dataOffset * 4
            : throw new Amqp1MalformedEncodingException($"An AMQP frame's DOFF of {dataOffset} is below 2 or past its {size} bytes.");
}
