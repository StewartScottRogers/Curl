namespace Curl.Conformance.Extended.Grpc.Reflection;

/// <summary>One <c>.proto</c> file the reference reflection service can describe.</summary>
/// <param name="Name">The file's name, as a <c>file_by_filename</c> request and other files' <c>dependency</c> lists give it.</param>
/// <param name="Descriptor">The serialized <c>google.protobuf.FileDescriptorProto</c>.</param>
/// <param name="Symbols">The fully qualified symbols, without a leading dot, a <c>file_containing_symbol</c> request finds this file by.</param>
/// <param name="Dependencies">The names of the files it imports, in its <c>dependency</c> order.</param>
public sealed record GrpcReflectedFile(string Name, byte[] Descriptor, IReadOnlyList<string> Symbols, IReadOnlyList<string> Dependencies);
