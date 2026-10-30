namespace Curl.Protobuf.Descriptors;

/// <summary>One <c>service</c> of a <c>.proto</c> file.</summary>
/// <param name="Name">The service's name as written.</param>
/// <param name="Methods">The service's <c>rpc</c> methods, in order.</param>
/// <param name="Options">The service's <c>option</c> statements, in order.</param>
public sealed record ProtoServiceDescriptor(
    string Name,
    IReadOnlyList<ProtoMethodDescriptor> Methods,
    IReadOnlyList<ProtoOption> Options);
