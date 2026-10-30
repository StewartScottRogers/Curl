using static Curl.Extension.Mqtt5.Packets.Mqtt5PropertyScope;

namespace Curl.Extension.Mqtt5.Packets;

/// <summary>MQTT 5 table 2-4: every property identifier with its data type and the packets it may appear in.</summary>
internal static class Mqtt5PropertyDefinitions
{
    private const Mqtt5PropertyScope PublishAndWill = Publish | Will;
    private const Mqtt5PropertyScope Acknowledgements = PubAck | PubRec | PubRel | PubComp | SubAck | UnsubAck;
    private const Mqtt5PropertyScope Everywhere = Connect | ConnAck | PublishAndWill | Acknowledgements | Subscribe | Unsubscribe | Disconnect | Auth;

    private static readonly Dictionary<Mqtt5PropertyIdentifier, Mqtt5PropertyDefinition> Definitions = new()
    {
        [Mqtt5PropertyIdentifier.PayloadFormatIndicator] = new(Mqtt5PropertyType.Byte, PublishAndWill, None),
        [Mqtt5PropertyIdentifier.MessageExpiryInterval] = new(Mqtt5PropertyType.FourByteInteger, PublishAndWill, None),
        [Mqtt5PropertyIdentifier.ContentType] = new(Mqtt5PropertyType.Utf8String, PublishAndWill, None),
        [Mqtt5PropertyIdentifier.ResponseTopic] = new(Mqtt5PropertyType.Utf8String, PublishAndWill, None),
        [Mqtt5PropertyIdentifier.CorrelationData] = new(Mqtt5PropertyType.BinaryData, PublishAndWill, None),
        [Mqtt5PropertyIdentifier.SubscriptionIdentifier] = new(Mqtt5PropertyType.VariableByteInteger, Publish | Subscribe, Publish),
        [Mqtt5PropertyIdentifier.SessionExpiryInterval] = new(Mqtt5PropertyType.FourByteInteger, Connect | ConnAck | Disconnect, None),
        [Mqtt5PropertyIdentifier.AssignedClientIdentifier] = new(Mqtt5PropertyType.Utf8String, ConnAck, None),
        [Mqtt5PropertyIdentifier.ServerKeepAlive] = new(Mqtt5PropertyType.TwoByteInteger, ConnAck, None),
        [Mqtt5PropertyIdentifier.AuthenticationMethod] = new(Mqtt5PropertyType.Utf8String, Connect | ConnAck | Auth, None),
        [Mqtt5PropertyIdentifier.AuthenticationData] = new(Mqtt5PropertyType.BinaryData, Connect | ConnAck | Auth, None),
        [Mqtt5PropertyIdentifier.RequestProblemInformation] = new(Mqtt5PropertyType.Byte, Connect, None),
        [Mqtt5PropertyIdentifier.WillDelayInterval] = new(Mqtt5PropertyType.FourByteInteger, Will, None),
        [Mqtt5PropertyIdentifier.RequestResponseInformation] = new(Mqtt5PropertyType.Byte, Connect, None),
        [Mqtt5PropertyIdentifier.ResponseInformation] = new(Mqtt5PropertyType.Utf8String, ConnAck, None),
        [Mqtt5PropertyIdentifier.ServerReference] = new(Mqtt5PropertyType.Utf8String, ConnAck | Disconnect, None),
        [Mqtt5PropertyIdentifier.ReasonString] = new(Mqtt5PropertyType.Utf8String, ConnAck | Acknowledgements | Disconnect | Auth, None),
        [Mqtt5PropertyIdentifier.ReceiveMaximum] = new(Mqtt5PropertyType.TwoByteInteger, Connect | ConnAck, None),
        [Mqtt5PropertyIdentifier.TopicAliasMaximum] = new(Mqtt5PropertyType.TwoByteInteger, Connect | ConnAck, None),
        [Mqtt5PropertyIdentifier.TopicAlias] = new(Mqtt5PropertyType.TwoByteInteger, Publish, None),
        [Mqtt5PropertyIdentifier.MaximumQos] = new(Mqtt5PropertyType.Byte, ConnAck, None),
        [Mqtt5PropertyIdentifier.RetainAvailable] = new(Mqtt5PropertyType.Byte, ConnAck, None),
        [Mqtt5PropertyIdentifier.UserProperty] = new(Mqtt5PropertyType.Utf8StringPair, Everywhere, Everywhere),
        [Mqtt5PropertyIdentifier.MaximumPacketSize] = new(Mqtt5PropertyType.FourByteInteger, Connect | ConnAck, None),
        [Mqtt5PropertyIdentifier.WildcardSubscriptionAvailable] = new(Mqtt5PropertyType.Byte, ConnAck, None),
        [Mqtt5PropertyIdentifier.SubscriptionIdentifierAvailable] = new(Mqtt5PropertyType.Byte, ConnAck, None),
        [Mqtt5PropertyIdentifier.SharedSubscriptionAvailable] = new(Mqtt5PropertyType.Byte, ConnAck, None),
    };

    /// <summary>The scope bit of <paramref name="packetType"/>.</summary>
    public static Mqtt5PropertyScope ScopeOf(Mqtt5PacketType packetType) => (Mqtt5PropertyScope)(1 << (int)packetType);

    /// <summary>The data type of <paramref name="identifier"/>; an identifier table 2-4 does not list is a Malformed Packet.</summary>
    public static Mqtt5PropertyType TypeOf(Mqtt5PropertyIdentifier identifier) => Find(identifier).Type;

    /// <summary>
    /// The definition of <paramref name="identifier"/> where <paramref name="scope"/> allows it. An unknown identifier,
    /// or one not valid in that scope, is a Malformed Packet (section 2.2.2.2).
    /// </summary>
    public static Mqtt5PropertyDefinition Get(Mqtt5PropertyIdentifier identifier, Mqtt5PropertyScope scope)
    {
        Mqtt5PropertyDefinition definition = Find(identifier);
        return (definition.Scope & scope) != 0
            ? definition
            : throw Mqtt5MalformedPacketException.Malformed($"Property {identifier} may not appear in {scope}.");
    }

    private static Mqtt5PropertyDefinition Find(Mqtt5PropertyIdentifier identifier) =>
        Definitions.TryGetValue(identifier, out Mqtt5PropertyDefinition? definition)
            ? definition
            : throw Mqtt5MalformedPacketException.Malformed($"0x{(int)identifier:X2} is not an MQTT 5 property identifier.");
}
