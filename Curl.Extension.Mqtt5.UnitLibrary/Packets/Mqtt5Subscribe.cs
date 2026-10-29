using Curl.Extension.Mqtt5.Encoding;

namespace Curl.Extension.Mqtt5.Packets;

/// <summary>SUBSCRIBE (MQTT 5 section 3.8): the Packet Identifier, properties, then one or more subscriptions.</summary>
internal sealed class Mqtt5Subscribe : Mqtt5Packet
{
    /// <inheritdoc />
    public override Mqtt5PacketType PacketType => Mqtt5PacketType.Subscribe;

    /// <summary>The Packet Identifier.</summary>
    public ushort PacketIdentifier { get; init; }

    /// <summary>The SUBSCRIBE properties.</summary>
    public IReadOnlyList<Mqtt5Property> Properties { get; init; } = [];

    /// <summary>The subscriptions, at least one.</summary>
    public IReadOnlyList<Mqtt5Subscription> Subscriptions { get; init; } = [];

    /// <summary>Reads a SUBSCRIBE's variable header and payload.</summary>
    internal static Mqtt5Subscribe ReadBody(Mqtt5Reader reader)
    {
        ushort packetIdentifier = Mqtt5PacketFields.ReadPacketIdentifier(reader);
        IReadOnlyList<Mqtt5Property> properties = Mqtt5PropertyCodec.Read(reader, Mqtt5PropertyScope.Subscribe);
        Mqtt5PacketFields.CheckPayloadNotEmpty(reader.IsAtEnd, Mqtt5PacketType.Subscribe);
        var subscriptions = new List<Mqtt5Subscription>();
        while (!reader.IsAtEnd)
        {
            subscriptions.Add(Mqtt5Subscription.Read(reader));
        }

        return new Mqtt5Subscribe { PacketIdentifier = packetIdentifier, Properties = properties, Subscriptions = subscriptions };
    }

    /// <inheritdoc />
    protected override void WriteBody(Mqtt5Writer writer)
    {
        Mqtt5PacketFields.WritePacketIdentifier(writer, PacketIdentifier);
        Mqtt5PropertyCodec.Write(writer, Properties, Mqtt5PropertyScope.Subscribe);
        Mqtt5PacketFields.CheckPayloadNotEmpty(Subscriptions.Count == 0, PacketType);
        foreach (Mqtt5Subscription subscription in Subscriptions)
        {
            subscription.Write(writer);
        }
    }
}
