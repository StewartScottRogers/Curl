using Curl.Protobuf.Descriptors;

namespace Curl.Extension.Grpc.Reflection;

/// <summary>One <c>FileDescriptorProto</c> a server sent by reflection (ADR-1149), decoded.</summary>
/// <param name="Name">The file's name, which <c>file_by_filename</c> asks for and other files list as a dependency.</param>
/// <param name="Dependencies">The names of the files it imports.</param>
/// <param name="Descriptor">Its types and services, as the <c>.proto</c> parser would describe them.</param>
internal sealed record ReflectedProtoFile(string Name, IReadOnlyList<string> Dependencies, ProtoFileDescriptor Descriptor);
