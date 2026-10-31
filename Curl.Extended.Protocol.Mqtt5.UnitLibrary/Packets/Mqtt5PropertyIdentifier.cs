namespace Curl.Extended.Protocol.Mqtt5.Packets;

/// <summary>The property identifiers of MQTT 5 section 2.2.2.2, table 2-4.</summary>
internal enum Mqtt5PropertyIdentifier
{
    /// <summary>0x01, Byte.</summary>
    PayloadFormatIndicator = 0x01,

    /// <summary>0x02, Four Byte Integer.</summary>
    MessageExpiryInterval = 0x02,

    /// <summary>0x03, UTF-8 Encoded String.</summary>
    ContentType = 0x03,

    /// <summary>0x08, UTF-8 Encoded String.</summary>
    ResponseTopic = 0x08,

    /// <summary>0x09, Binary Data.</summary>
    CorrelationData = 0x09,

    /// <summary>0x0B, Variable Byte Integer.</summary>
    SubscriptionIdentifier = 0x0B,

    /// <summary>0x11, Four Byte Integer.</summary>
    SessionExpiryInterval = 0x11,

    /// <summary>0x12, UTF-8 Encoded String.</summary>
    AssignedClientIdentifier = 0x12,

    /// <summary>0x13, Two Byte Integer.</summary>
    ServerKeepAlive = 0x13,

    /// <summary>0x15, UTF-8 Encoded String.</summary>
    AuthenticationMethod = 0x15,

    /// <summary>0x16, Binary Data.</summary>
    AuthenticationData = 0x16,

    /// <summary>0x17, Byte.</summary>
    RequestProblemInformation = 0x17,

    /// <summary>0x18, Four Byte Integer.</summary>
    WillDelayInterval = 0x18,

    /// <summary>0x19, Byte.</summary>
    RequestResponseInformation = 0x19,

    /// <summary>0x1A, UTF-8 Encoded String.</summary>
    ResponseInformation = 0x1A,

    /// <summary>0x1C, UTF-8 Encoded String.</summary>
    ServerReference = 0x1C,

    /// <summary>0x1F, UTF-8 Encoded String.</summary>
    ReasonString = 0x1F,

    /// <summary>0x21, Two Byte Integer.</summary>
    ReceiveMaximum = 0x21,

    /// <summary>0x22, Two Byte Integer.</summary>
    TopicAliasMaximum = 0x22,

    /// <summary>0x23, Two Byte Integer.</summary>
    TopicAlias = 0x23,

    /// <summary>0x24, Byte.</summary>
    MaximumQos = 0x24,

    /// <summary>0x25, Byte.</summary>
    RetainAvailable = 0x25,

    /// <summary>0x26, UTF-8 String Pair.</summary>
    UserProperty = 0x26,

    /// <summary>0x27, Four Byte Integer.</summary>
    MaximumPacketSize = 0x27,

    /// <summary>0x28, Byte.</summary>
    WildcardSubscriptionAvailable = 0x28,

    /// <summary>0x29, Byte.</summary>
    SubscriptionIdentifierAvailable = 0x29,

    /// <summary>0x2A, Byte.</summary>
    SharedSubscriptionAvailable = 0x2A,
}
