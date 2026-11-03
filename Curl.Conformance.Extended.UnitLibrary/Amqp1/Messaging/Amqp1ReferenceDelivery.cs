namespace Curl.Conformance.Extended.Amqp1.Messaging;

/// <summary>One transfer frame the reference container sends: its performative and the message bytes it carries.</summary>
/// <param name="Transfer">The transfer performative, such as one <see cref="Amqp1ReferenceMessage.Transfer"/> writes.</param>
/// <param name="Payload">The message bytes, or the part of them this frame carries.</param>
public sealed record Amqp1ReferenceDelivery(Amqp1ReferenceDescribed Transfer, byte[] Payload);
