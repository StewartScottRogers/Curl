namespace Curl.Conformance.Extended.Amqp1;

/// <summary>One frame (Part 2 section 2.3) the reference container received.</summary>
/// <param name="Type">The frame type: <see cref="AmqpType"/> or <see cref="SaslType"/>.</param>
/// <param name="Channel">The channel; ignored in a SASL frame.</param>
/// <param name="Body">The performative or SASL frame body; null for an empty frame.</param>
/// <param name="Payload">The bytes after the body, such as a transfer's message.</param>
public sealed record Amqp1ReferenceFrame(byte Type, ushort Channel, Amqp1ReferenceDescribed? Body, byte[] Payload)
{
    /// <summary>The frame type of an AMQP frame.</summary>
    public const byte AmqpType = 0x00;

    /// <summary>The frame type of a SASL frame.</summary>
    public const byte SaslType = 0x01;
}
