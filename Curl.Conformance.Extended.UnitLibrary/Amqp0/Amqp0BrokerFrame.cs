using System.Buffers.Binary;

namespace Curl.Conformance.Extended.Amqp0;

/// <summary>A frame the reference broker read from the client (section 4.2.3), its frame-end octet kept so a case can check it.</summary>
/// <param name="Type">The frame type octet: 1 method, 2 content header, 3 content body, 8 heartbeat.</param>
/// <param name="Channel">The channel number.</param>
/// <param name="Payload">The payload.</param>
/// <param name="FrameEnd">The octet after the payload, %xCE in a well-formed frame.</param>
public sealed record Amqp0BrokerFrame(byte Type, ushort Channel, byte[] Payload, byte FrameEnd)
{
    /// <summary>The method frame type.</summary>
    public const byte MethodType = 1;

    /// <summary>The heartbeat frame type.</summary>
    public const byte HeartbeatType = 8;

    /// <summary>The frame-end octet.</summary>
    public const byte FrameEndOctet = 0xCE;

    /// <summary>A method frame's class id.</summary>
    public ushort ClassId => BinaryPrimitives.ReadUInt16BigEndian(Payload);

    /// <summary>A method frame's method id.</summary>
    public ushort MethodId => BinaryPrimitives.ReadUInt16BigEndian(Payload.AsSpan(2));

    /// <summary>A reader over a method frame's arguments.</summary>
    public Amqp0ArgumentReader Arguments() => new(Payload, 4);

    /// <summary>Encodes a frame: type, channel, 32-bit size, payload and the given frame-end octet.</summary>
    public static byte[] Encode(byte type, ushort channel, byte[] payload, byte frameEnd = FrameEndOctet)
    {
        ArgumentNullException.ThrowIfNull(payload);
        byte[] frame = new byte[payload.Length + 8];
        frame[0] = type;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(1), channel);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(3), (uint)payload.Length);
        payload.CopyTo(frame, 7);
        frame[^1] = frameEnd;
        return frame;
    }

    /// <summary>Encodes a method frame of the given class, method and arguments.</summary>
    public static byte[] EncodeMethod(ushort channel, ushort classId, ushort methodId, Amqp0ArgumentWriter arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return Encode(MethodType, channel, [.. new Amqp0ArgumentWriter().Short(classId).Short(methodId).ToArray(), .. arguments.ToArray()]);
    }
}
