namespace Curl.Protobuf.Descriptors;

/// <summary>One file of a <see cref="ProtoFileSet" />.</summary>
/// <param name="Path">The path the file was read by.</param>
/// <param name="Descriptor">The file as read.</param>
public sealed record ProtoFile(string Path, ProtoFileDescriptor Descriptor);
