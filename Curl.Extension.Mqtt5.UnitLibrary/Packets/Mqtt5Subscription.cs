using Curl.Extension.Mqtt5.Encoding;

namespace Curl.Extension.Mqtt5.Packets;

/// <summary>
/// One topic filter of a SUBSCRIBE with its subscription options (MQTT 5 section 3.8.3.1): Maximum QoS, No Local,
/// Retain As Published and Retain Handling.
/// </summary>
internal sealed class Mqtt5Subscription
{
    /// <summary>The topic filter, perhaps a shared subscription.</summary>
    public required string TopicFilter { get; init; }

    /// <summary>The Maximum QoS.</summary>
    public Mqtt5QualityOfService MaximumQualityOfService { get; init; }

    /// <summary>The No Local option; a Protocol Error on a shared subscription.</summary>
    public bool NoLocal { get; init; }

    /// <summary>The Retain As Published option.</summary>
    public bool RetainAsPublished { get; init; }

    /// <summary>The Retain Handling option.</summary>
    public Mqtt5RetainHandling RetainHandling { get; init; }

    /// <summary>Reads a topic filter and its options byte; reserved bits set are a Malformed Packet.</summary>
    internal static Mqtt5Subscription Read(Mqtt5Reader reader)
    {
        string topicFilter = reader.ReadUtf8String();
        byte options = reader.ReadByte();
        if ((options & 0xC0) != 0)
        {
            throw Mqtt5MalformedPacketException.Malformed("A subscription's reserved option bits are set.");
        }

        var subscription = new Mqtt5Subscription
        {
            TopicFilter = topicFilter,
            MaximumQualityOfService = (Mqtt5QualityOfService)(options & 0b11),
            NoLocal = (options & 0x04) != 0,
            RetainAsPublished = (options & 0x08) != 0,
            RetainHandling = (Mqtt5RetainHandling)((options >> 4) & 0b11),
        };
        subscription.Validate();
        return subscription;
    }

    /// <summary>Writes the topic filter and its options byte.</summary>
    internal void Write(Mqtt5Writer writer)
    {
        Validate();
        writer.WriteUtf8String(TopicFilter);
        writer.WriteByte((byte)((int)MaximumQualityOfService | (NoLocal ? 0x04 : 0) | (RetainAsPublished ? 0x08 : 0) | ((int)RetainHandling << 4)));
    }

    private void Validate()
    {
        Mqtt5Topics.ValidateTopicFilter(TopicFilter);
        if (MaximumQualityOfService > Mqtt5QualityOfService.ExactlyOnce || RetainHandling > Mqtt5RetainHandling.DoNotSend)
        {
            throw Mqtt5MalformedPacketException.ProtocolError("A subscription has Maximum QoS 3 or Retain Handling 3.");
        }

        if (NoLocal && Mqtt5Topics.IsSharedSubscription(TopicFilter))
        {
            throw Mqtt5MalformedPacketException.ProtocolError("A shared subscription sets No Local.");
        }
    }
}
