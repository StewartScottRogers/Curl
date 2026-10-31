using Curl.Extended.Protocol.Amqp1.Types;

namespace Curl.Extended.Protocol.Amqp1.Frames;

/// <summary>
/// Writes a message's sections (Part 3 section 3.2) as the payload of a transfer, and reads them back from the
/// payloads of the one or more transfer frames that carried the message, joined in order (Part 2 section 2.6.14).
/// </summary>
internal static class Amqp1MessageCodec
{
    /// <summary>
    /// The sections written one after another as described values; raises
    /// <see cref="Amqp1MalformedEncodingException"/> for a section whose value is not the kind its type holds.
    /// </summary>
    public static byte[] Write(IEnumerable<Amqp1Section> sections)
    {
        var writer = new Amqp1Writer();
        foreach (var section in sections)
        {
            RequireHolds(section);
            var value = section.Value is Amqp1Composite composite ? composite.Fields : section.Value;
            writer.WriteValue(new Amqp1Described(section.Type.Code, value));
        }

        return writer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// The sections in the transfer payloads <paramref name="transferPayloads"/>, joined in order; raises
    /// <see cref="Amqp1MalformedEncodingException"/> for bytes that are not described sections.
    /// </summary>
    public static IReadOnlyList<Amqp1Section> Read(IEnumerable<ReadOnlyMemory<byte>> transferPayloads)
    {
        var joined = transferPayloads.SelectMany(payload => payload.ToArray()).ToArray();
        var reader = new Amqp1Reader(joined);
        var sections = new List<Amqp1Section>();
        while (!reader.IsAtEnd)
        {
            sections.Add(ReadSection(reader.ReadValue()));
        }

        return sections;
    }

    private static Amqp1Section ReadSection(object? value)
    {
        var described = value as Amqp1Described
            ?? throw new Amqp1MalformedEncodingException($"A {value?.GetType().Name ?? "null"} is not a described message section.");
        var type = Amqp1SectionType.FromDescriptor(described.Descriptor);
        var section = new Amqp1Section(type, type.CompositeType is null ? described.Value : Amqp1Composite.Read(described));
        RequireHolds(section);
        return section;
    }

    private static void RequireHolds(Amqp1Section section)
    {
        if (!section.Type.Holds(section.Value))
        {
            throw new Amqp1MalformedEncodingException(
                $"A {section.Type.Name} section cannot hold a {section.Value?.GetType().Name ?? "null"}.");
        }
    }
}
