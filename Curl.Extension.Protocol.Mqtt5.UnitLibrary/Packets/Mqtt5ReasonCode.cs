namespace Curl.Extension.Protocol.Mqtt5.Packets;

/// <summary>
/// The reason codes of MQTT 5 section 2.4, table 2-6. A value below 0x80 is success, 0x80 and above failure
/// (<see cref="Mqtt5ReasonCodes"/>). 0x00 has three names, by the packet that carries it.
/// </summary>
internal enum Mqtt5ReasonCode : byte
{
    /// <summary>0x00 in CONNACK, PUBACK, PUBREC, PUBREL, PUBCOMP, UNSUBACK and AUTH.</summary>
    Success = 0x00,

    /// <summary>0x00 in DISCONNECT.</summary>
    NormalDisconnection = 0x00,

    /// <summary>0x00 in SUBACK.</summary>
    GrantedQos0 = 0x00,

    /// <summary>0x01 in SUBACK.</summary>
    GrantedQos1 = 0x01,

    /// <summary>0x02 in SUBACK.</summary>
    GrantedQos2 = 0x02,

    /// <summary>0x04 in DISCONNECT.</summary>
    DisconnectWithWillMessage = 0x04,

    /// <summary>0x10 in PUBACK and PUBREC.</summary>
    NoMatchingSubscribers = 0x10,

    /// <summary>0x11 in UNSUBACK.</summary>
    NoSubscriptionExisted = 0x11,

    /// <summary>0x18 in AUTH.</summary>
    ContinueAuthentication = 0x18,

    /// <summary>0x19 in AUTH.</summary>
    ReAuthenticate = 0x19,

    /// <summary>0x80.</summary>
    UnspecifiedError = 0x80,

    /// <summary>0x81.</summary>
    MalformedPacket = 0x81,

    /// <summary>0x82.</summary>
    ProtocolError = 0x82,

    /// <summary>0x83.</summary>
    ImplementationSpecificError = 0x83,

    /// <summary>0x84.</summary>
    UnsupportedProtocolVersion = 0x84,

    /// <summary>0x85.</summary>
    ClientIdentifierNotValid = 0x85,

    /// <summary>0x86.</summary>
    BadUserNameOrPassword = 0x86,

    /// <summary>0x87.</summary>
    NotAuthorized = 0x87,

    /// <summary>0x88.</summary>
    ServerUnavailable = 0x88,

    /// <summary>0x89.</summary>
    ServerBusy = 0x89,

    /// <summary>0x8A.</summary>
    Banned = 0x8A,

    /// <summary>0x8B.</summary>
    ServerShuttingDown = 0x8B,

    /// <summary>0x8C.</summary>
    BadAuthenticationMethod = 0x8C,

    /// <summary>0x8D.</summary>
    KeepAliveTimeout = 0x8D,

    /// <summary>0x8E.</summary>
    SessionTakenOver = 0x8E,

    /// <summary>0x8F.</summary>
    TopicFilterInvalid = 0x8F,

    /// <summary>0x90.</summary>
    TopicNameInvalid = 0x90,

    /// <summary>0x91.</summary>
    PacketIdentifierInUse = 0x91,

    /// <summary>0x92.</summary>
    PacketIdentifierNotFound = 0x92,

    /// <summary>0x93.</summary>
    ReceiveMaximumExceeded = 0x93,

    /// <summary>0x94.</summary>
    TopicAliasInvalid = 0x94,

    /// <summary>0x95.</summary>
    PacketTooLarge = 0x95,

    /// <summary>0x96.</summary>
    MessageRateTooHigh = 0x96,

    /// <summary>0x97.</summary>
    QuotaExceeded = 0x97,

    /// <summary>0x98.</summary>
    AdministrativeAction = 0x98,

    /// <summary>0x99.</summary>
    PayloadFormatInvalid = 0x99,

    /// <summary>0x9A.</summary>
    RetainNotSupported = 0x9A,

    /// <summary>0x9B.</summary>
    QosNotSupported = 0x9B,

    /// <summary>0x9C.</summary>
    UseAnotherServer = 0x9C,

    /// <summary>0x9D.</summary>
    ServerMoved = 0x9D,

    /// <summary>0x9E.</summary>
    SharedSubscriptionsNotSupported = 0x9E,

    /// <summary>0x9F.</summary>
    ConnectionRateExceeded = 0x9F,

    /// <summary>0xA0.</summary>
    MaximumConnectTime = 0xA0,

    /// <summary>0xA1.</summary>
    SubscriptionIdentifiersNotSupported = 0xA1,

    /// <summary>0xA2.</summary>
    WildcardSubscriptionsNotSupported = 0xA2,
}
