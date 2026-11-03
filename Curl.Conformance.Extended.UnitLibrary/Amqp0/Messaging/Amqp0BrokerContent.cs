using System.Buffers.Binary;

namespace Curl.Conformance.Extended.Amqp0.Messaging;

/// <summary>
/// Content frames as the reference broker writes and reads them, from the AMQP 0-9-1 specification alone: a content
/// header frame (4.2.6.1: class id, weight, 64-bit body size, property flags and the properties) and body frames
/// (4.2.6.2) of at most a given size each, none for an empty body.
/// </summary>
public static class Amqp0BrokerContent
{
    /// <summary>The content header frame type.</summary>
    public const byte HeaderType = 2;

    /// <summary>The content body frame type.</summary>
    public const byte BodyType = 3;

    /// <summary>The basic class id, the class of every content this plan sends.</summary>
    public const ushort BasicClassId = 60;

    /// <summary>The property flag bit of content-type, the first basic property (4.2.6.1).</summary>
    public const ushort ContentTypeFlag = 0x8000;

    /// <summary>
    /// Encodes a basic content header frame on <paramref name="channel"/> and the body in frames of at most
    /// <paramref name="bodyFrameSize"/> payload bytes on <paramref name="bodyChannel"/>; a <paramref name="contentType"/>
    /// sets the content-type property.
    /// </summary>
    public static byte[] Encode(ushort channel, byte[] body, int bodyFrameSize, ushort bodyChannel, string? contentType = null)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bodyFrameSize);
        Amqp0ArgumentWriter header = new Amqp0ArgumentWriter().Short(BasicClassId).Short(0)
            .Long((uint)((ulong)body.LongLength >> 32)).Long((uint)body.LongLength)
            .Short(contentType is null ? (ushort)0 : ContentTypeFlag);
        if (contentType is not null)
        {
            header.ShortString(contentType);
        }

        List<byte> frames = [.. Amqp0BrokerFrame.Encode(HeaderType, channel, header.ToArray())];
        for (int offset = 0; offset < body.Length; offset += bodyFrameSize)
        {
            frames.AddRange(Amqp0BrokerFrame.Encode(BodyType, bodyChannel, body[offset..Math.Min(body.Length, offset + bodyFrameSize)]));
        }

        return [.. frames];
    }

    /// <summary>Reads a content header frame the client sent.</summary>
    public static Amqp0BrokerContentHeader ReadHeader(Amqp0BrokerFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        byte[] payload = frame.Payload;
        return new Amqp0BrokerContentHeader(
            BinaryPrimitives.ReadUInt16BigEndian(payload),
            BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(2)),
            BinaryPrimitives.ReadUInt64BigEndian(payload.AsSpan(4)),
            BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(12)));
    }
}
