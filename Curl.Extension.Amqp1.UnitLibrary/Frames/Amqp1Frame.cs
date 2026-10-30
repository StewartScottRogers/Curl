using Curl.Extension.Amqp1.Types;

namespace Curl.Extension.Amqp1.Frames;

/// <summary>
/// One AMQP 1.0 frame (Part 2 section 2.3): its type (<see cref="AmqpType"/> or <see cref="SaslType"/>), its
/// channel and its body, the bytes after the extended header. An AMQP frame with an empty body is a heartbeat.
/// </summary>
internal sealed record Amqp1Frame(byte Type, ushort Channel, ReadOnlyMemory<byte> Body)
{
    /// <summary>The frame type of an AMQP frame (section 2.3.2).</summary>
    public const byte AmqpType = 0x00;

    /// <summary>The frame type of a SASL frame (Part 5 section 5.3.3).</summary>
    public const byte SaslType = 0x01;

    /// <summary>Whether this is an empty AMQP frame, sent only to keep the connection alive (section 2.4.5).</summary>
    public bool IsHeartbeat => Type == AmqpType && Body.IsEmpty;

    /// <summary>An empty AMQP frame on channel 0.</summary>
    public static Amqp1Frame Heartbeat() => new(AmqpType, 0, ReadOnlyMemory<byte>.Empty);

    /// <summary>
    /// An AMQP frame on <paramref name="channel"/> carrying <paramref name="performative"/> and, for a transfer only,
    /// the message bytes <paramref name="payload"/>.
    /// </summary>
    public static Amqp1Frame ForPerformative(ushort channel, Amqp1Composite performative, ReadOnlySpan<byte> payload = default) =>
        new(AmqpType, channel, EncodeBody(AmqpType, performative, payload));

    /// <summary>A SASL frame carrying <paramref name="saslFrameBody"/>; its channel is 0, as section 5.3.3 asks.</summary>
    public static Amqp1Frame ForSasl(Amqp1Composite saslFrameBody) =>
        new(SaslType, 0, EncodeBody(SaslType, saslFrameBody, default));

    /// <summary>
    /// Reads the body: the performative, or SASL frame body, and the payload after it, empty unless it is a transfer.
    /// Raises <see cref="Amqp1MalformedEncodingException"/> for a body that is not one of these.
    /// </summary>
    public (Amqp1Composite Performative, ReadOnlyMemory<byte> Payload) ReadBody()
    {
        var reader = new Amqp1Reader(Body);
        var performative = Amqp1Composite.Read(reader.ReadValue());
        var payload = Body[reader.Position..];
        RequireFrameCarries(Type, performative, payload.Length);
        return (performative, payload);
    }

    private static byte[] EncodeBody(byte type, Amqp1Composite performative, ReadOnlySpan<byte> payload)
    {
        RequireFrameCarries(type, performative, payload.Length);
        return [.. Amqp1Writer.Encode(performative.ToDescribed()), .. payload];
    }

    private static void RequireFrameCarries(byte type, Amqp1Composite performative, int payloadLength)
    {
        var kind = type == AmqpType ? Amqp1CompositeKind.Performative : Amqp1CompositeKind.Sasl;
        if (performative.Type.Kind != kind)
        {
            throw new Amqp1MalformedEncodingException($"A frame of type 0x{type:x2} cannot carry {performative.Type.Name}.");
        }

        if (payloadLength > 0 && performative.Type != Amqp1CompositeType.Transfer)
        {
            throw new Amqp1MalformedEncodingException($"{payloadLength} payload bytes follow {performative.Type.Name}, which carries none.");
        }
    }
}
