namespace Curl.Protobuf.Descriptors;

/// <summary>One value of an <c>enum</c>.</summary>
/// <param name="Name">The value's name as written.</param>
/// <param name="Number">The value's number, which may be negative.</param>
/// <param name="Options">The value's <c>[...]</c> options, in order.</param>
public sealed record ProtoEnumValueDescriptor(string Name, int Number, IReadOnlyList<ProtoOption> Options);
