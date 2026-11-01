using Curl.Extended.Protocol.Amqp1.Types;

namespace Curl.Extended.Protocol.Amqp1.Frames;

/// <summary>
/// One message section of Part 3 section 3.2: its name, its descriptor code and symbol, and the value it holds. The
/// header and properties sections hold an <see cref="Amqp1Composite"/> of <see cref="CompositeType"/>; the
/// annotations, application-properties and footer sections an <see cref="Amqp1Map"/>; data a <see cref="byte"/>[];
/// amqp-sequence a list; amqp-value any value.
/// </summary>
internal sealed class Amqp1SectionType
{
    private readonly Func<object?, bool> holdsValue;

    private Amqp1SectionType(string name, ulong code, string encoding, Func<object?, bool> holdsValue, Amqp1CompositeType? compositeType = null)
    {
        Name = name;
        Code = code;
        Symbol = new Amqp1Symbol($"amqp:{name}:{encoding}");
        this.holdsValue = holdsValue;
        CompositeType = compositeType;
    }

    /// <summary>The header section (section 3.2.1).</summary>
    public static Amqp1SectionType Header { get; } = OfComposite(Amqp1CompositeType.Header);

    /// <summary>The delivery-annotations section (section 3.2.2).</summary>
    public static Amqp1SectionType DeliveryAnnotations { get; } = new("delivery-annotations", 0x71, "map", IsMap);

    /// <summary>The message-annotations section (section 3.2.3).</summary>
    public static Amqp1SectionType MessageAnnotations { get; } = new("message-annotations", 0x72, "map", IsMap);

    /// <summary>The properties section (section 3.2.4).</summary>
    public static Amqp1SectionType Properties { get; } = OfComposite(Amqp1CompositeType.Properties);

    /// <summary>The application-properties section (section 3.2.5).</summary>
    public static Amqp1SectionType ApplicationProperties { get; } = new("application-properties", 0x74, "map", IsMap);

    /// <summary>The data section (section 3.2.6), one part of an opaque binary body.</summary>
    public static Amqp1SectionType Data { get; } = new("data", 0x75, "binary", value => value is byte[]);

    /// <summary>The amqp-sequence section (section 3.2.7), one part of a body of lists.</summary>
    public static Amqp1SectionType AmqpSequence { get; } = new("amqp-sequence", 0x76, "list", value => value is IReadOnlyList<object?>);

    /// <summary>The amqp-value section (section 3.2.8), a body of one AMQP value.</summary>
    public static Amqp1SectionType AmqpValue { get; } = new("amqp-value", 0x77, "*", _ => true);

    /// <summary>The footer section (section 3.2.9).</summary>
    public static Amqp1SectionType Footer { get; } = new("footer", 0x78, "map", IsMap);

    private static Dictionary<object, Amqp1SectionType> ByDescriptor { get; } =
        new[] { Header, DeliveryAnnotations, MessageAnnotations, Properties, ApplicationProperties, Data, AmqpSequence, AmqpValue, Footer }
            .SelectMany(type => new KeyValuePair<object, Amqp1SectionType>[] { new(type.Code, type), new(type.Symbol, type) })
            .ToDictionary();

    /// <summary>The name, as the specification spells it.</summary>
    public string Name { get; }

    /// <summary>The numeric descriptor code, domain 0.</summary>
    public ulong Code { get; }

    /// <summary>The symbolic descriptor.</summary>
    public Amqp1Symbol Symbol { get; }

    /// <summary>The composite type a header or properties section holds; null for every other section.</summary>
    public Amqp1CompositeType? CompositeType { get; }

    /// <summary>
    /// The section a descriptor names, its numeric code or its symbol; raises
    /// <see cref="Amqp1MalformedEncodingException"/> for a descriptor that names no section.
    /// </summary>
    public static Amqp1SectionType FromDescriptor(object? descriptor) =>
        descriptor is not null && ByDescriptor.TryGetValue(descriptor, out var type)
            ? type
            : throw new Amqp1MalformedEncodingException($"Descriptor {descriptor ?? "null"} names no AMQP message section.");

    /// <summary>Whether <paramref name="value"/> is the kind of value this section holds.</summary>
    public bool Holds(object? value) => holdsValue(value);

    /// <summary>The name.</summary>
    public override string ToString() => Name;

    private static bool IsMap(object? value) => value is Amqp1Map;

    private static Amqp1SectionType OfComposite(Amqp1CompositeType compositeType) =>
        new(compositeType.Name, compositeType.Code, "list", value => value is Amqp1Composite composite && composite.Type == compositeType, compositeType);
}
