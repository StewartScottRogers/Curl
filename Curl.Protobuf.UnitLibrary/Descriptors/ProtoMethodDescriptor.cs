namespace Curl.Protobuf.Descriptors;

/// <summary>One <c>rpc</c> method of a <c>service</c>.</summary>
/// <param name="Name">The method's name as written.</param>
/// <param name="InputType">The request message type as written.</param>
/// <param name="ClientStreaming">True when the request is written <c>stream</c>: the client sends many.</param>
/// <param name="OutputType">The response message type as written.</param>
/// <param name="ServerStreaming">True when the response is written <c>stream</c>: the server sends many.</param>
/// <param name="Options">The <c>option</c> statements in the method's body, in order.</param>
public sealed record ProtoMethodDescriptor(
    string Name,
    ProtoTypeReference InputType,
    bool ClientStreaming,
    ProtoTypeReference OutputType,
    bool ServerStreaming,
    IReadOnlyList<ProtoOption> Options);
