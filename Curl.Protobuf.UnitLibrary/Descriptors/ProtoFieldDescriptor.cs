namespace Curl.Protobuf.Descriptors;

/// <summary>One field of a <c>message</c>.</summary>
/// <param name="Name">The field's name as written.</param>
/// <param name="Number">The field's number, 1 to <see cref="Wire.ProtobufReader.MaximumFieldNumber" />.</param>
/// <param name="Label">Whether the field is singular, <c>optional</c> or <c>repeated</c>; a map field is repeated.</param>
/// <param name="Type">The field's type; <see cref="ProtoFieldType.Map" /> for a <c>map&lt;,&gt;</c> field.</param>
/// <param name="MapKey">A map field's key type, otherwise null.</param>
/// <param name="MapValue">A map field's value type, otherwise null.</param>
/// <param name="OneofName">The name of the oneof the field is in, otherwise null.</param>
/// <param name="Options">The field's <c>[...]</c> options, in order.</param>
public sealed record ProtoFieldDescriptor(
    string Name,
    int Number,
    ProtoFieldLabel Label,
    ProtoTypeReference Type,
    ProtoTypeReference? MapKey,
    ProtoTypeReference? MapValue,
    string? OneofName,
    IReadOnlyList<ProtoOption> Options);
