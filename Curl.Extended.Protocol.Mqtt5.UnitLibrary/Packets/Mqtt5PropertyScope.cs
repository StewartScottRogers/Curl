namespace Curl.Extended.Protocol.Mqtt5.Packets;

/// <summary>
/// Where a property may appear (MQTT 5 table 2-4): one bit per packet type, at 1 shifted by the type's value, and
/// one for the Will Properties of CONNECT's payload.
/// </summary>
[Flags]
internal enum Mqtt5PropertyScope
{
    /// <summary>Nowhere.</summary>
    None = 0,

    /// <summary>CONNECT's variable header.</summary>
    Connect = 1 << (int)Mqtt5PacketType.Connect,

    /// <summary>CONNACK.</summary>
    ConnAck = 1 << (int)Mqtt5PacketType.ConnAck,

    /// <summary>PUBLISH.</summary>
    Publish = 1 << (int)Mqtt5PacketType.Publish,

    /// <summary>PUBACK.</summary>
    PubAck = 1 << (int)Mqtt5PacketType.PubAck,

    /// <summary>PUBREC.</summary>
    PubRec = 1 << (int)Mqtt5PacketType.PubRec,

    /// <summary>PUBREL.</summary>
    PubRel = 1 << (int)Mqtt5PacketType.PubRel,

    /// <summary>PUBCOMP.</summary>
    PubComp = 1 << (int)Mqtt5PacketType.PubComp,

    /// <summary>SUBSCRIBE.</summary>
    Subscribe = 1 << (int)Mqtt5PacketType.Subscribe,

    /// <summary>SUBACK.</summary>
    SubAck = 1 << (int)Mqtt5PacketType.SubAck,

    /// <summary>UNSUBSCRIBE.</summary>
    Unsubscribe = 1 << (int)Mqtt5PacketType.Unsubscribe,

    /// <summary>UNSUBACK.</summary>
    UnsubAck = 1 << (int)Mqtt5PacketType.UnsubAck,

    /// <summary>DISCONNECT.</summary>
    Disconnect = 1 << (int)Mqtt5PacketType.Disconnect,

    /// <summary>AUTH.</summary>
    Auth = 1 << (int)Mqtt5PacketType.Auth,

    /// <summary>The Will Properties in CONNECT's payload.</summary>
    Will = 1 << 16,
}
