namespace Curl.Extended.Protocol.Mqtt5.Packets;

/// <summary>The MQTT 5 control packet types of section 2.1.2, table 2-1; 0 is reserved.</summary>
internal enum Mqtt5PacketType
{
    /// <summary>Client asks to connect.</summary>
    Connect = 1,

    /// <summary>Server acknowledges the connection.</summary>
    ConnAck = 2,

    /// <summary>An application message.</summary>
    Publish = 3,

    /// <summary>QoS 1 publish acknowledgement.</summary>
    PubAck = 4,

    /// <summary>QoS 2 publish received.</summary>
    PubRec = 5,

    /// <summary>QoS 2 publish release.</summary>
    PubRel = 6,

    /// <summary>QoS 2 publish complete.</summary>
    PubComp = 7,

    /// <summary>Client subscribes.</summary>
    Subscribe = 8,

    /// <summary>Server acknowledges a subscribe.</summary>
    SubAck = 9,

    /// <summary>Client unsubscribes.</summary>
    Unsubscribe = 10,

    /// <summary>Server acknowledges an unsubscribe.</summary>
    UnsubAck = 11,

    /// <summary>Ping request.</summary>
    PingReq = 12,

    /// <summary>Ping response.</summary>
    PingResp = 13,

    /// <summary>Disconnect notification.</summary>
    Disconnect = 14,

    /// <summary>Authentication exchange.</summary>
    Auth = 15,
}
