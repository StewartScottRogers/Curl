namespace Curl.Protobuf.Descriptors;

/// <summary>One <c>.proto</c> file as <see cref="ProtoParser" /> read it, names as written.</summary>
/// <param name="Syntax">The file's <c>syntax</c>: always <c>proto3</c>.</param>
/// <param name="Package">The file's <c>package</c>, or empty when it has none.</param>
/// <param name="Options">The file's <c>option</c> statements, in order.</param>
/// <param name="Messages">The file's top-level messages, in order.</param>
/// <param name="Enums">The file's top-level enums, in order.</param>
public sealed record ProtoFileDescriptor(
    string Syntax,
    string Package,
    IReadOnlyList<ProtoOption> Options,
    IReadOnlyList<ProtoMessageDescriptor> Messages,
    IReadOnlyList<ProtoEnumDescriptor> Enums);
