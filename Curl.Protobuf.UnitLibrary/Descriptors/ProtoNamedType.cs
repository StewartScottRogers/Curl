namespace Curl.Protobuf.Descriptors;

/// <summary>A message or enum of a <see cref="ProtoFileSet" />, by its fully qualified name.</summary>
/// <param name="FullName">The fully qualified name with a leading '.': <c>.pkg.Outer.Inner</c>.</param>
/// <param name="Path">The path of the file that defines it.</param>
/// <param name="Message">The message, or null when it is an enum.</param>
/// <param name="Enum">The enum, or null when it is a message.</param>
public sealed record ProtoNamedType(string FullName, string Path, ProtoMessageDescriptor? Message, ProtoEnumDescriptor? Enum);
