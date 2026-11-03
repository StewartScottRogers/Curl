using Curl.Protobuf.DynamicMessages;
using Curl.Protobuf.Wire;

namespace Curl.Protobuf.Json;

/// <summary>Converts JSON to Protocol Buffers binary and back, for a message type read from <c>.proto</c> descriptors.</summary>
public static class ProtobufJsonConverter
{
    /// <summary>Converts UTF-8 JSON to the binary form of a message of this type; the binary is null unless the error is <see cref="ProtobufJsonError.None" />.</summary>
    public static ProtobufJsonError TryConvertJsonToBinary(DynamicMessageType type, ReadOnlyMemory<byte> json, out byte[]? binary)
    {
        var error = ProtobufJsonParser.Parse(type, json, out var message);
        binary = message is null ? null : DynamicMessageEncoder.Encode(message);
        return error;
    }

    /// <summary>
    /// Converts the binary form of a message of this type to JSON; the JSON is null unless the
    /// error is <see cref="ProtobufJsonError.None" />. A Timestamp or Duration outside its type's
    /// range, or an Any whose type URL names no type of the catalogue or whose value is not a message of that type, has no JSON form and is <see cref="ProtobufJsonError.InvalidValue" />.
    /// </summary>
    public static ProtobufJsonError TryConvertBinaryToJson(DynamicMessageType type, ReadOnlyMemory<byte> binary, bool indented, out string? json)
    {
        var error = DynamicMessageDecoder.TryDecode(type, binary, out var message);
        json = null;
        if (message is not null && !TryFormat(message, indented, out json))
        {
            return ProtobufJsonError.InvalidValue;
        }

        return error switch
        {
            ProtobufDecodeError.None => ProtobufJsonError.None,
            ProtobufDecodeError.NestingTooDeep => ProtobufJsonError.NestingTooDeep,
            _ => ProtobufJsonError.MalformedBinary,
        };
    }

    private static bool TryFormat(DynamicMessage message, bool indented, out string? json)
    {
        try
        {
            json = ProtobufJsonFormatter.Format(message, indented);
            return true;
        }
        catch (FormatException)
        {
            // A Timestamp or Duration outside its range, or an Any that does not resolve, has no JSON form.
            json = null;
            return false;
        }
    }
}
