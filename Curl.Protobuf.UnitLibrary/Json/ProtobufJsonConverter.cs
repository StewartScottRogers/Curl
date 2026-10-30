using Curl.Protobuf.DynamicMessages;
using Curl.Protobuf.Wire;

namespace Curl.Protobuf.Json;

/// <summary>Converts JSON to Protocol Buffers binary and back, for a message type read from <c>.proto</c> descriptors.</summary>
public static class ProtobufJsonConverter
{
    /// <summary>Converts UTF-8 JSON to the binary form of a message of this type; the binary is null unless the error is <see cref="ProtobufJsonError.None" />.</summary>
    public static ProtobufJsonError TryConvertJsonToBinary(DynamicMessageType type, ReadOnlyMemory<byte> json, out byte[]? binary)
    {
        var error = ProtobufJsonParser.TryParse(type, json, out var message);
        binary = message is null ? null : DynamicMessageEncoder.Encode(message);
        return error;
    }

    /// <summary>Converts the binary form of a message of this type to JSON; the JSON is null unless the error is <see cref="ProtobufJsonError.None" />.</summary>
    public static ProtobufJsonError TryConvertBinaryToJson(DynamicMessageType type, ReadOnlyMemory<byte> binary, bool indented, out string? json)
    {
        var error = DynamicMessageDecoder.TryDecode(type, binary, out var message);
        json = message is null ? null : ProtobufJsonFormatter.Format(message, indented);
        return error switch
        {
            ProtobufDecodeError.None => ProtobufJsonError.None,
            ProtobufDecodeError.NestingTooDeep => ProtobufJsonError.NestingTooDeep,
            _ => ProtobufJsonError.MalformedBinary,
        };
    }
}
