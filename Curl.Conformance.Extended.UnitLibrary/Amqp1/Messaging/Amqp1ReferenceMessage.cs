namespace Curl.Conformance.Extended.Amqp1.Messaging;

/// <summary>
/// The message sections (Part 3 section 3.2) and delivery states (Part 3 section 3.4) the reference container writes
/// and reads, and the transfer performative (Part 2 section 2.7.5) that carries a message.
/// </summary>
public static class Amqp1ReferenceMessage
{
    /// <summary>header, 0x70.</summary>
    public const ulong Header = 0x70;

    /// <summary>properties, 0x73.</summary>
    public const ulong Properties = 0x73;

    /// <summary>application-properties, 0x74.</summary>
    public const ulong ApplicationProperties = 0x74;

    /// <summary>data, 0x75.</summary>
    public const ulong Data = 0x75;

    /// <summary>amqp-value, 0x77.</summary>
    public const ulong AmqpValue = 0x77;

    /// <summary>footer, 0x78.</summary>
    public const ulong Footer = 0x78;

    /// <summary>accepted, 0x24.</summary>
    public const ulong Accepted = 0x24;

    /// <summary>rejected, 0x25.</summary>
    public const ulong Rejected = 0x25;

    /// <summary>released, 0x26.</summary>
    public const ulong Released = 0x26;

    /// <summary>modified, 0x27.</summary>
    public const ulong Modified = 0x27;

    private static readonly Amqp1ReferenceEncoder Encoder = new(wide: false);

    /// <summary>Encodes <paramref name="sections"/> one after another, as a message's bytes in a transfer's payload.</summary>
    /// <param name="sections">The sections, each a described value such as data or amqp-value.</param>
    /// <returns>The encoded message.</returns>
    public static byte[] Encode(params Amqp1ReferenceDescribed[] sections) => [.. sections.SelectMany(Encoder.Encode)];

    /// <summary>Reads the sections of a message from <paramref name="message"/>, the payloads of its transfers joined.</summary>
    /// <param name="message">The encoded message.</param>
    /// <returns>Its sections in order.</returns>
    public static IReadOnlyList<Amqp1ReferenceDescribed> Decode(byte[] message)
    {
        ArgumentNullException.ThrowIfNull(message);
        List<Amqp1ReferenceDescribed> sections = [];
        for (int position = 0; position < message.Length;)
        {
            sections.Add((Amqp1ReferenceDescribed)Amqp1ReferenceDecoder.Read(message, ref position)!);
        }

        return sections;
    }

    /// <summary>A transfer performative: <paramref name="handle"/>, <paramref name="deliveryId"/>, delivery-tag 0, message-format 0, <paramref name="settled"/> and <paramref name="more"/>.</summary>
    /// <param name="handle">The link's handle.</param>
    /// <param name="deliveryId">The delivery-id.</param>
    /// <param name="settled">Whether the sender settles the delivery as it sends it.</param>
    /// <param name="more">Whether more transfers of the same delivery follow.</param>
    /// <returns>The transfer.</returns>
    public static Amqp1ReferenceDescribed Transfer(uint handle, uint deliveryId, bool settled, bool more) =>
        new(Amqp1ReferenceDescriptors.Transfer, new object?[] { handle, deliveryId, new byte[] { 0 }, 0u, settled, more });
}
