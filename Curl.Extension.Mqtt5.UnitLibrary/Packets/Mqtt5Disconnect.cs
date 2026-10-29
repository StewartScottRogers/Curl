using Curl.Extension.Mqtt5.Encoding;

namespace Curl.Extension.Mqtt5.Packets;

/// <summary>DISCONNECT (MQTT 5 section 3.14): a Disconnect Reason Code and properties, both left out for a normal disconnection.</summary>
internal sealed class Mqtt5Disconnect : Mqtt5Packet
{
    /// <inheritdoc />
    public override Mqtt5PacketType PacketType => Mqtt5PacketType.Disconnect;

    /// <summary>The Disconnect Reason Code.</summary>
    public Mqtt5ReasonCode ReasonCode { get; init; }

    /// <summary>The DISCONNECT properties.</summary>
    public IReadOnlyList<Mqtt5Property> Properties { get; init; } = [];

    /// <summary>Reads a DISCONNECT's variable header.</summary>
    internal static Mqtt5Disconnect ReadBody(Mqtt5Reader reader)
    {
        (Mqtt5ReasonCode reasonCode, IReadOnlyList<Mqtt5Property> properties) = Mqtt5PacketFields.ReadReasonAndProperties(reader, Mqtt5PropertyScope.Disconnect);
        return new Mqtt5Disconnect { ReasonCode = reasonCode, Properties = properties };
    }

    /// <inheritdoc />
    protected override void WriteBody(Mqtt5Writer writer) =>
        Mqtt5PacketFields.WriteReasonAndProperties(writer, ReasonCode, Properties, Mqtt5PropertyScope.Disconnect);
}
