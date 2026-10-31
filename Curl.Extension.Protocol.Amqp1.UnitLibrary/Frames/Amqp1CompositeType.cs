using Curl.Extension.Protocol.Amqp1.Types;

namespace Curl.Extension.Protocol.Amqp1.Frames;

/// <summary>
/// One AMQP 1.0 composite type a client writes or reads: its name, its numeric descriptor code (domain 0), where it
/// may appear and its fields in wire order, with which of them are mandatory. The catalogue holds every performative
/// (Part 2 section 2.7), error (2.8.14), the delivery states (Part 3 section 3.4), source and target (3.5.3, 3.5.4),
/// the header and properties sections (3.2.1, 3.2.4) and the SASL frame bodies (Part 5 section 5.3.3).
/// </summary>
internal sealed class Amqp1CompositeType
{
    private const Amqp1CompositeKind Performative = Amqp1CompositeKind.Performative;
    private const Amqp1CompositeKind DeliveryState = Amqp1CompositeKind.DeliveryState;
    private const Amqp1CompositeKind Sasl = Amqp1CompositeKind.Sasl;
    private const Amqp1CompositeKind Section = Amqp1CompositeKind.Section;
    private const Amqp1CompositeKind Field = Amqp1CompositeKind.Field;

    private readonly bool[] mandatory;
    private readonly string[] fieldNames;

    private Amqp1CompositeType(string name, ulong code, Amqp1CompositeKind kind, params string[] fields)
    {
        Name = name;
        Code = code;
        Kind = kind;
        fieldNames = [.. fields.Select(field => field.TrimEnd('!'))];
        mandatory = [.. fields.Select(field => field.EndsWith('!'))];
    }

    /// <summary>The open performative (section 2.7.1).</summary>
    public static Amqp1CompositeType Open { get; } = new("open", 0x10, Performative,
        "container-id!", "hostname", "max-frame-size", "channel-max", "idle-time-out", "outgoing-locales",
        "incoming-locales", "offered-capabilities", "desired-capabilities", "properties");

    /// <summary>The begin performative (section 2.7.2).</summary>
    public static Amqp1CompositeType Begin { get; } = new("begin", 0x11, Performative,
        "remote-channel", "next-outgoing-id!", "incoming-window!", "outgoing-window!", "handle-max",
        "offered-capabilities", "desired-capabilities", "properties");

    /// <summary>The attach performative (section 2.7.3).</summary>
    public static Amqp1CompositeType Attach { get; } = new("attach", 0x12, Performative,
        "name!", "handle!", "role!", "snd-settle-mode", "rcv-settle-mode", "source", "target", "unsettled",
        "incomplete-unsettled", "initial-delivery-count", "max-message-size", "offered-capabilities",
        "desired-capabilities", "properties");

    /// <summary>The flow performative (section 2.7.4).</summary>
    public static Amqp1CompositeType Flow { get; } = new("flow", 0x13, Performative,
        "next-incoming-id", "incoming-window!", "next-outgoing-id!", "outgoing-window!", "handle", "delivery-count",
        "link-credit", "available", "drain", "echo", "properties");

    /// <summary>The transfer performative (section 2.7.5), the only one followed by a payload.</summary>
    public static Amqp1CompositeType Transfer { get; } = new("transfer", 0x14, Performative,
        "handle!", "delivery-id", "delivery-tag", "message-format", "settled", "more", "rcv-settle-mode", "state",
        "resume", "aborted", "batchable");

    /// <summary>The disposition performative (section 2.7.6).</summary>
    public static Amqp1CompositeType Disposition { get; } = new("disposition", 0x15, Performative,
        "role!", "first!", "last", "settled", "state", "batchable");

    /// <summary>The detach performative (section 2.7.7).</summary>
    public static Amqp1CompositeType Detach { get; } = new("detach", 0x16, Performative, "handle!", "closed", "error");

    /// <summary>The end performative (section 2.7.8).</summary>
    public static Amqp1CompositeType End { get; } = new("end", 0x17, Performative, "error");

    /// <summary>The close performative (section 2.7.9).</summary>
    public static Amqp1CompositeType Close { get; } = new("close", 0x18, Performative, "error");

    /// <summary>The error type (section 2.8.14).</summary>
    public static Amqp1CompositeType Error { get; } = new("error", 0x1d, Field, "condition!", "description", "info");

    /// <summary>The received delivery state (Part 3 section 3.4.1).</summary>
    public static Amqp1CompositeType Received { get; } = new("received", 0x23, DeliveryState, "section-number!", "section-offset!");

    /// <summary>The accepted outcome (Part 3 section 3.4.2).</summary>
    public static Amqp1CompositeType Accepted { get; } = new("accepted", 0x24, DeliveryState);

    /// <summary>The rejected outcome (Part 3 section 3.4.3).</summary>
    public static Amqp1CompositeType Rejected { get; } = new("rejected", 0x25, DeliveryState, "error");

    /// <summary>The released outcome (Part 3 section 3.4.4).</summary>
    public static Amqp1CompositeType Released { get; } = new("released", 0x26, DeliveryState);

    /// <summary>The modified outcome (Part 3 section 3.4.5).</summary>
    public static Amqp1CompositeType Modified { get; } = new("modified", 0x27, DeliveryState,
        "delivery-failed", "undeliverable-here", "message-annotations");

    /// <summary>The source of a link (Part 3 section 3.5.3).</summary>
    public static Amqp1CompositeType Source { get; } = new("source", 0x28, Field,
        "address", "durable", "expiry-policy", "timeout", "dynamic", "dynamic-node-properties", "distribution-mode",
        "filter", "default-outcome", "outcomes", "capabilities");

    /// <summary>The target of a link (Part 3 section 3.5.4).</summary>
    public static Amqp1CompositeType Target { get; } = new("target", 0x29, Field,
        "address", "durable", "expiry-policy", "timeout", "dynamic", "dynamic-node-properties", "capabilities");

    /// <summary>The header message section (Part 3 section 3.2.1).</summary>
    public static Amqp1CompositeType Header { get; } = new("header", 0x70, Section,
        "durable", "priority", "ttl", "first-acquirer", "delivery-count");

    /// <summary>The properties message section (Part 3 section 3.2.4).</summary>
    public static Amqp1CompositeType Properties { get; } = new("properties", 0x73, Section,
        "message-id", "user-id", "to", "subject", "reply-to", "correlation-id", "content-type", "content-encoding",
        "absolute-expiry-time", "creation-time", "group-id", "group-sequence", "reply-to-group-id");

    /// <summary>The sasl-mechanisms frame body (Part 5 section 5.3.3.1).</summary>
    public static Amqp1CompositeType SaslMechanisms { get; } = new("sasl-mechanisms", 0x40, Sasl, "sasl-server-mechanisms!");

    /// <summary>The sasl-init frame body (Part 5 section 5.3.3.2).</summary>
    public static Amqp1CompositeType SaslInit { get; } = new("sasl-init", 0x41, Sasl, "mechanism!", "initial-response", "hostname");

    /// <summary>The sasl-challenge frame body (Part 5 section 5.3.3.3).</summary>
    public static Amqp1CompositeType SaslChallenge { get; } = new("sasl-challenge", 0x42, Sasl, "challenge!");

    /// <summary>The sasl-response frame body (Part 5 section 5.3.3.4).</summary>
    public static Amqp1CompositeType SaslResponse { get; } = new("sasl-response", 0x43, Sasl, "response!");

    /// <summary>The sasl-outcome frame body (Part 5 section 5.3.3.5).</summary>
    public static Amqp1CompositeType SaslOutcome { get; } = new("sasl-outcome", 0x44, Sasl, "code!", "additional-data");

    /// <summary>Every type in the catalogue.</summary>
    public static IReadOnlyList<Amqp1CompositeType> All { get; } =
    [
        Open, Begin, Attach, Flow, Transfer, Disposition, Detach, End, Close, Error, Received, Accepted, Rejected,
        Released, Modified, Source, Target, Header, Properties, SaslMechanisms, SaslInit, SaslChallenge, SaslResponse,
        SaslOutcome,
    ];

    private static Dictionary<object, Amqp1CompositeType> ByDescriptor { get; } =
        All.SelectMany(type => new KeyValuePair<object, Amqp1CompositeType>[] { new(type.Code, type), new(type.Symbol, type) })
            .ToDictionary();

    /// <summary>The name, as the specification spells it.</summary>
    public string Name { get; }

    /// <summary>The numeric descriptor code, domain 0.</summary>
    public ulong Code { get; }

    /// <summary>The symbolic descriptor, <c>amqp:</c> name <c>:list</c>.</summary>
    public Amqp1Symbol Symbol => new($"amqp:{Name}:list");

    /// <summary>Where the type may appear.</summary>
    public Amqp1CompositeKind Kind { get; }

    /// <summary>The field names in wire order.</summary>
    public IReadOnlyList<string> FieldNames => fieldNames;

    /// <summary>
    /// The type a descriptor names, its numeric code or its symbol; raises
    /// <see cref="Amqp1MalformedEncodingException"/> for a descriptor outside the catalogue.
    /// </summary>
    public static Amqp1CompositeType FromDescriptor(object? descriptor) =>
        descriptor is not null && ByDescriptor.TryGetValue(descriptor, out var type)
            ? type
            : throw new Amqp1MalformedEncodingException($"Descriptor {descriptor ?? "null"} names no AMQP composite type a client knows.");

    /// <summary>Whether the field at <paramref name="index"/> is mandatory.</summary>
    public bool IsMandatory(int index) => mandatory[index];

    /// <summary>The index of the field named <paramref name="fieldName"/>.</summary>
    public int IndexOf(string fieldName)
    {
        var index = Array.IndexOf(fieldNames, fieldName);
        return index >= 0 ? index : throw new ArgumentOutOfRangeException(nameof(fieldName), fieldName, $"{Name} has no field {fieldName}.");
    }

    /// <summary>The name.</summary>
    public override string ToString() => Name;
}
