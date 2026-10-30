namespace Curl.Protobuf.Descriptors;

/// <summary>A field type as written.</summary>
/// <param name="Type">The scalar type, <see cref="ProtoFieldType.Named" /> or <see cref="ProtoFieldType.Map" />.</param>
/// <param name="Name">The type as written, a leading '.' kept: <c>int32</c>, <c>.pkg.Message</c>, <c>map&lt;string, int32&gt;</c>.</param>
public sealed record ProtoTypeReference(ProtoFieldType Type, string Name);
