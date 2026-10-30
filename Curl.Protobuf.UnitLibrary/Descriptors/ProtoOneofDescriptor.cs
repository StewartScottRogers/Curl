namespace Curl.Protobuf.Descriptors;

/// <summary>One <c>oneof</c> of a message; its fields are in the message's fields, naming it.</summary>
/// <param name="Name">The oneof's name as written.</param>
/// <param name="Options">The oneof's <c>option</c> statements, in order.</param>
public sealed record ProtoOneofDescriptor(string Name, IReadOnlyList<ProtoOption> Options);
