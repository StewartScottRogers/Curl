using Curl.Protobuf.Descriptors;

namespace Curl.Protobuf.DynamicMessages;

/// <summary>One field of a <see cref="DynamicMessageType" />, its type resolved.</summary>
public sealed class DynamicField
{
    internal DynamicField(
        ProtoFieldDescriptor descriptor,
        DynamicValueKind kind,
        DynamicMessageType? messageType,
        ProtoEnumDescriptor? enumType)
    {
        Descriptor = descriptor;
        Kind = kind;
        MessageType = messageType;
        EnumType = enumType;
        Codec = DynamicScalarCodec.Find(kind);
    }

    /// <summary>The field as the <c>.proto</c> file declares it.</summary>
    public ProtoFieldDescriptor Descriptor { get; }

    /// <summary>The field's name.</summary>
    public string Name => Descriptor.Name;

    /// <summary>The field's number.</summary>
    public int Number => Descriptor.Number;

    /// <summary>What the field holds; <see cref="DynamicValueKind.Message" /> for a map field, whose entries are messages.</summary>
    public DynamicValueKind Kind { get; }

    /// <summary>The type of a message field, or a map field's entry type (fields <c>key</c> = 1 and <c>value</c> = 2); otherwise null.</summary>
    public DynamicMessageType? MessageType { get; }

    /// <summary>The enum of an enum field; otherwise null.</summary>
    public ProtoEnumDescriptor? EnumType { get; }

    /// <summary>Whether the field is a <c>map&lt;,&gt;</c>.</summary>
    public bool IsMap => Descriptor.Type.Type == ProtoFieldType.Map;

    /// <summary>Whether the field is <c>repeated</c> or a map.</summary>
    public bool IsRepeated => Descriptor.Label == ProtoFieldLabel.Repeated;

    /// <summary>
    /// Whether a singular field's presence is kept, so a default value is still written:
    /// an <c>optional</c> field, a oneof's field or a message field. Proto3's other singular
    /// fields have implicit presence and their default value is not written.
    /// </summary>
    public bool HasPresence =>
        Descriptor.Label == ProtoFieldLabel.Optional || Descriptor.OneofName is not null || Kind == DynamicValueKind.Message;

    /// <summary>Whether a repeated numeric field is written packed: always, unless it has <c>[packed = false]</c>.</summary>
    public bool IsPacked =>
        IsRepeated && Codec is not null && !Descriptor.Options.Any(option => option is { Name: "packed", Value: "false" });

    internal DynamicScalarCodec? Codec { get; }

    /// <summary>The value an unset singular field reads as: 0, false, empty, or an empty message.</summary>
    public object CreateDefaultValue() => Kind switch
    {
        DynamicValueKind.String => string.Empty,
        DynamicValueKind.Bytes => Array.Empty<byte>(),
        DynamicValueKind.Message => new DynamicMessage(MessageType!),
        _ => Codec!.FromWire(0),
    };

    /// <summary>Whether a value is of the .NET type this field holds (a message of its own type for a message field).</summary>
    public bool Accepts(object value) => Kind switch
    {
        DynamicValueKind.String => value is string,
        DynamicValueKind.Bytes => value is byte[],
        DynamicValueKind.Message => value is DynamicMessage message && message.Type == MessageType,
        _ => value.GetType() == Codec!.ValueType,
    };
}
