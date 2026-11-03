namespace Curl.Conformance.Extended.Amqp0.Messaging;

/// <summary>The fixed fields of a content header frame (4.2.6.1) the client sent.</summary>
/// <param name="ClassId">The class of the content, 60 for basic.</param>
/// <param name="Weight">The weight field, which the specification says is unused and zero.</param>
/// <param name="BodySize">The total size of the body the body frames carry.</param>
/// <param name="PropertyFlags">The property flags; zero when the content carries no properties.</param>
public sealed record Amqp0BrokerContentHeader(ushort ClassId, ushort Weight, ulong BodySize, ushort PropertyFlags);
