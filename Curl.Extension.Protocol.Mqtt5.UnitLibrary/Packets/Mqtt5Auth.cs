using Curl.Extension.Protocol.Mqtt5.Encoding;

namespace Curl.Extension.Protocol.Mqtt5.Packets;

/// <summary>AUTH (MQTT 5 section 3.15): an Authenticate Reason Code and properties, both left out for Success.</summary>
internal sealed class Mqtt5Auth : Mqtt5Packet
{
    /// <inheritdoc />
    public override Mqtt5PacketType PacketType => Mqtt5PacketType.Auth;

    /// <summary>The Authenticate Reason Code.</summary>
    public Mqtt5ReasonCode ReasonCode { get; init; }

    /// <summary>The AUTH properties: Authentication Method and Data, Reason String, User Properties.</summary>
    public IReadOnlyList<Mqtt5Property> Properties { get; init; } = [];

    /// <summary>Reads an AUTH's variable header.</summary>
    internal static Mqtt5Auth ReadBody(Mqtt5Reader reader)
    {
        (Mqtt5ReasonCode reasonCode, IReadOnlyList<Mqtt5Property> properties) = Mqtt5PacketFields.ReadReasonAndProperties(reader, Mqtt5PropertyScope.Auth);
        return new Mqtt5Auth { ReasonCode = reasonCode, Properties = properties };
    }

    /// <inheritdoc />
    protected override void WriteBody(Mqtt5Writer writer) =>
        Mqtt5PacketFields.WriteReasonAndProperties(writer, ReasonCode, Properties, Mqtt5PropertyScope.Auth);
}
