using Curl.Extension.Protocol.Mqtt5.Encoding;

namespace Curl.Extension.Protocol.Mqtt5.Packets;

/// <summary>The will message of a CONNECT (MQTT 5 sections 3.1.2.5 to 3.1.2.7 and 3.1.3.2 to 3.1.3.4).</summary>
internal sealed class Mqtt5Will
{
    /// <summary>The Will Topic.</summary>
    public required string Topic { get; init; }

    /// <summary>The Will Payload.</summary>
    public byte[] Payload { get; init; } = [];

    /// <summary>The Will QoS.</summary>
    public Mqtt5QualityOfService QualityOfService { get; init; }

    /// <summary>The Will Retain flag.</summary>
    public bool Retain { get; init; }

    /// <summary>The Will Properties.</summary>
    public IReadOnlyList<Mqtt5Property> Properties { get; init; } = [];

    /// <summary>The Will Flag, Will QoS and Will Retain bits of the connect flags.</summary>
    internal int ConnectFlags => 0x04 | ((int)QualityOfService << 3) | (Retain ? 0x20 : 0);

    /// <summary>
    /// Reads the will from CONNECT's payload when the connect flags carry one. Will QoS 3, or Will QoS or Retain
    /// without the Will Flag, is a Malformed Packet.
    /// </summary>
    internal static Mqtt5Will? ReadPayloadFields(byte connectFlags, Mqtt5Reader reader)
    {
        var qualityOfService = (Mqtt5QualityOfService)((connectFlags >> 3) & 0b11);
        bool retain = (connectFlags & 0x20) != 0;
        if ((connectFlags & 0x04) == 0)
        {
            return qualityOfService == Mqtt5QualityOfService.AtMostOnce && !retain
                ? null
                : throw Mqtt5MalformedPacketException.Malformed("CONNECT sets Will QoS or Will Retain without the Will Flag.");
        }

        CheckQualityOfService(qualityOfService);
        IReadOnlyList<Mqtt5Property> properties = Mqtt5PropertyCodec.Read(reader, Mqtt5PropertyScope.Will);
        string topic = reader.ReadUtf8String();
        Mqtt5Topics.ValidateTopicName(topic);
        return new Mqtt5Will { Topic = topic, Payload = reader.ReadBinaryData(), QualityOfService = qualityOfService, Retain = retain, Properties = properties };
    }

    /// <summary>Writes the Will Properties, Will Topic and Will Payload.</summary>
    internal void WritePayloadFields(Mqtt5Writer writer)
    {
        CheckQualityOfService(QualityOfService);
        Mqtt5Topics.ValidateTopicName(Topic);
        Mqtt5PropertyCodec.Write(writer, Properties, Mqtt5PropertyScope.Will);
        writer.WriteUtf8String(Topic);
        writer.WriteBinaryData(Payload);
    }

    private static void CheckQualityOfService(Mqtt5QualityOfService qualityOfService)
    {
        if (qualityOfService > Mqtt5QualityOfService.ExactlyOnce)
        {
            throw Mqtt5MalformedPacketException.Malformed("Will QoS is 3.");
        }
    }
}
