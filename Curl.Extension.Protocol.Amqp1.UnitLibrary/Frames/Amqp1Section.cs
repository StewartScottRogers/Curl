namespace Curl.Extension.Protocol.Amqp1.Frames;

/// <summary>One section of a message (Part 3 section 3.2): its type and the value <see cref="Amqp1SectionType"/> says it holds.</summary>
internal sealed record Amqp1Section(Amqp1SectionType Type, object? Value);
