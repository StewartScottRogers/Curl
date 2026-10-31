namespace Curl.Extension.Protocol.Amqp0.Wire;

/// <summary>A field table decimal ('D'): <paramref name="Value"/> divided by ten to the power <paramref name="Scale"/>.</summary>
internal readonly record struct Amqp0Decimal(byte Scale, uint Value);
