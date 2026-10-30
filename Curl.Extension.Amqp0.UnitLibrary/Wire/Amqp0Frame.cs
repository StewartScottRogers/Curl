using System.Buffers.Binary;

namespace Curl.Extension.Amqp0.Wire;

/// <summary>
/// One frame (section 4.2.3): a type octet, a channel short, a payload size long, the payload, and the frame-end
/// octet 0xCE.
/// </summary>
internal sealed record Amqp0Frame(Amqp0FrameType Type, ushort Channel, byte[] Payload)
{
    /// <summary>The octet every frame ends with.</summary>
    public const byte FrameEnd = 0xCE;

    /// <summary>The bytes a frame adds around its payload: seven of header and the frame-end.</summary>
    public const int Overhead = 8;

    /// <summary>The smallest frame-max a peer may negotiate (section 4.2.1, frame-min-size).</summary>
    public const uint MinimumFrameMax = 4096;

    /// <summary>A heartbeat frame (section 4.2.7).</summary>
    public static Amqp0Frame Heartbeat { get; } = new(Amqp0FrameType.Heartbeat, 0, []);

    /// <summary>The frame's bytes.</summary>
    public byte[] ToBytes()
    {
        var writer = new Amqp0Writer();
        writer.WriteOctet((byte)Type);
        writer.WriteShort(Channel);
        writer.WriteLongString(Payload);
        writer.WriteOctet(FrameEnd);
        return writer.ToArray();
    }

    /// <summary>
    /// Reads one frame from the front of <paramref name="buffer"/>. Returns false, consuming nothing, while the buffer
    /// does not yet hold the whole frame. An unknown type, a frame larger than <paramref name="frameMax"/> (0 for no
    /// limit), a heartbeat off channel 0 or a bad frame-end is a 501 frame-error.
    /// </summary>
    public static bool TryRead(ReadOnlySpan<byte> buffer, uint frameMax, out Amqp0Frame? frame, out int consumed)
    {
        frame = null;
        consumed = 0;
        if (buffer.Length < 7)
        {
            return false;
        }

        var type = (Amqp0FrameType)buffer[0];
        ushort channel = BinaryPrimitives.ReadUInt16BigEndian(buffer[1..]);
        uint size = BinaryPrimitives.ReadUInt32BigEndian(buffer[3..]);
        CheckHeader(type, channel, size, frameMax);
        int total = (int)size + Overhead;
        if (buffer.Length < total)
        {
            return false;
        }

        if (buffer[total - 1] != FrameEnd)
        {
            throw Amqp0WireFormatException.FrameError($"A frame ends with 0x{buffer[total - 1]:X2}, not frame-end 0xCE.");
        }

        frame = new Amqp0Frame(type, channel, buffer.Slice(7, (int)size).ToArray());
        consumed = total;
        return true;
    }

    private static void CheckHeader(Amqp0FrameType type, ushort channel, uint size, uint frameMax)
    {
        if (!Enum.IsDefined(type))
        {
            throw Amqp0WireFormatException.FrameError($"Frame type {(byte)type} is not one AMQP 0-9-1 defines.");
        }

        long limit = frameMax == 0 ? int.MaxValue - Overhead : (long)frameMax - Overhead;
        if (size > limit)
        {
            throw Amqp0WireFormatException.FrameError($"A frame of {size} payload bytes exceeds frame-max {frameMax}.");
        }

        if (type == Amqp0FrameType.Heartbeat && channel != 0)
        {
            throw Amqp0WireFormatException.FrameError($"A heartbeat frame came on channel {channel}, not channel 0.");
        }
    }
}
