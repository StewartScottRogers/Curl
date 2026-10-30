namespace Curl.Protobuf.Descriptors;

/// <summary>One <c>message</c> of a <c>.proto</c> file.</summary>
/// <param name="Name">The message's name as written.</param>
/// <param name="Fields">The message's fields in order, a oneof's among them.</param>
/// <param name="Oneofs">The message's oneofs, in order.</param>
/// <param name="Messages">The messages nested in this one, in order.</param>
/// <param name="Enums">The enums nested in this message, in order.</param>
/// <param name="Options">The message's <c>option</c> statements, in order.</param>
/// <param name="ReservedRanges">The field numbers the message reserves.</param>
/// <param name="ReservedNames">The field names the message reserves.</param>
public sealed record ProtoMessageDescriptor(
    string Name,
    IReadOnlyList<ProtoFieldDescriptor> Fields,
    IReadOnlyList<ProtoOneofDescriptor> Oneofs,
    IReadOnlyList<ProtoMessageDescriptor> Messages,
    IReadOnlyList<ProtoEnumDescriptor> Enums,
    IReadOnlyList<ProtoOption> Options,
    IReadOnlyList<ProtoReservedRange> ReservedRanges,
    IReadOnlyList<string> ReservedNames);
