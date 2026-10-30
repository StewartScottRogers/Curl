namespace Curl.Extension.Mqtt5.Packets;

/// <summary>
/// One row of MQTT 5 table 2-4: the property's data type, where it may appear, and where it may appear more than once.
/// </summary>
internal sealed record Mqtt5PropertyDefinition(Mqtt5PropertyType Type, Mqtt5PropertyScope Scope, Mqtt5PropertyScope RepeatScope);
