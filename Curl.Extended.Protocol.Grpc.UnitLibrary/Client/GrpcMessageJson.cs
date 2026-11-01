using System.Text;
using Curl.Protobuf.DynamicMessages;
using Curl.Protobuf.Json;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Grpc.Client;

/// <summary>Turns <c>-d</c> JSON into a request message and a response message into JSON, by the proto3 JSON mapping (ADR-1147).</summary>
internal static class GrpcMessageJson
{
    /// <summary>Encodes the <c>-d</c> data as a message of the request type; no <c>-d</c>, or an empty one, sends the empty message.</summary>
    /// <param name="type">The request message type.</param>
    /// <param name="postData">The <c>-d</c> data, or <see langword="null" /> when none was given.</param>
    /// <returns>The message's binary form.</returns>
    /// <exception cref="GrpcClientException">The data is not a message of the type as JSON (exit 2).</exception>
    public static byte[] Encode(DynamicMessageType type, ReadOnlyMemory<byte>? postData)
    {
        if (postData is not { Length: > 0 } json)
        {
            return [];
        }

        ProtobufJsonError error = ProtobufJsonConverter.TryConvertJsonToBinary(type, json, out byte[]? binary);
        return binary ?? throw new GrpcClientException(CurlExitCode.FailedInit, $"-d: the data is not a {type.FullName} message as JSON ({error})");
    }

    /// <summary>Decodes a response message as JSON indented by two spaces, with <c>\n</c> line ends on every platform, and a newline.</summary>
    /// <param name="type">The response message type.</param>
    /// <param name="payload">The message's binary form.</param>
    /// <returns>The UTF-8 JSON text and its newline.</returns>
    /// <exception cref="GrpcClientException">The bytes are not a message of the type (exit 8).</exception>
    public static byte[] Decode(DynamicMessageType type, ReadOnlyMemory<byte> payload)
    {
        ProtobufJsonError error = ProtobufJsonConverter.TryConvertBinaryToJson(type, payload, indented: true, out string? json);
        return json is null
            ? throw new GrpcClientException(CurlExitCode.WeirdServerReply, $"The server's message is not a {type.FullName} ({error})")
            : Encoding.UTF8.GetBytes(json.ReplaceLineEndings("\n") + "\n");
    }
}
