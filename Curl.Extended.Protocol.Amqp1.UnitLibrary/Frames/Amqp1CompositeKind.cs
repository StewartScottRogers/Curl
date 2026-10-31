namespace Curl.Extended.Protocol.Amqp1.Frames;

/// <summary>Where a composite type of <see cref="Amqp1CompositeType"/> may appear.</summary>
internal enum Amqp1CompositeKind
{
    /// <summary>A performative, the body of an AMQP frame (Part 2 section 2.7).</summary>
    Performative,

    /// <summary>A SASL frame body (Part 5 section 5.3.3).</summary>
    Sasl,

    /// <summary>A delivery state (Part 3 section 3.4).</summary>
    DeliveryState,

    /// <summary>A message section (Part 3 section 3.2).</summary>
    Section,

    /// <summary>A value carried in a field of another composite: an error, a source or a target.</summary>
    Field,
}
