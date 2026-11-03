namespace Curl.Conformance.Extended.Amqp1;

/// <summary>An AMQP 1.0 symbol (Part 1 section 1.6.21): ASCII text the reference container reads and writes apart from a string.</summary>
/// <param name="Value">The symbol's text.</param>
public readonly record struct Amqp1ReferenceSymbol(string Value);
