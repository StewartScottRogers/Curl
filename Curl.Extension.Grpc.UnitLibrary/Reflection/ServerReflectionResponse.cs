using Curl.Extension.Grpc.Wire;

namespace Curl.Extension.Grpc.Reflection;

/// <summary>
/// A <c>grpc.reflection.v1.ServerReflectionResponse</c> (reflection.proto, ADR-1149): the serialized
/// <c>FileDescriptorProto</c>s of its <c>file_descriptor_response</c>, or its <c>error_response</c>.
/// </summary>
/// <param name="FileDescriptorProtos">The files the server sent; empty for an error.</param>
/// <param name="Error">The <c>error_response</c>'s code and message, or <see langword="null" /> when the server sent none.</param>
internal sealed record ServerReflectionResponse(IReadOnlyList<ReadOnlyMemory<byte>> FileDescriptorProtos, (int Code, string Message)? Error)
{
    private const int FileDescriptorResponseField = 4;

    private const int ErrorResponseField = 7;

    /// <summary>Decodes a response.</summary>
    /// <param name="message">The response's binary form.</param>
    /// <returns>The response.</returns>
    /// <exception cref="GrpcWireException">The bytes are not a Protocol Buffers message.</exception>
    public static ServerReflectionResponse Decode(ReadOnlyMemory<byte> message)
    {
        ReflectionMessageFields response = ReflectionMessageFields.Read(message);
        ReflectionMessageFields? error = response.Message(ErrorResponseField);
        return new ServerReflectionResponse(
            [.. response.Message(FileDescriptorResponseField)?.Bytes(1) ?? []],
            error is null ? null : ((int)error.Integer(1), error.String(2)));
    }
}
