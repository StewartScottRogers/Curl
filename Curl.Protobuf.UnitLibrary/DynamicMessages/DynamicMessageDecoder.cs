using System.Text;
using System.Text.Unicode;
using Curl.Protobuf.Wire;

namespace Curl.Protobuf.DynamicMessages;

/// <summary>
/// Reads Protocol Buffers binary into a <see cref="DynamicMessage" />. Packed and unpacked
/// repeated numeric fields are both accepted; a singular field read twice keeps the last value,
/// a message field read twice is merged, and the last oneof field read wins. A field the type
/// does not have, or with another wire type than the type's field, is kept as an unknown field.
/// Malformed input is a <see cref="ProtobufDecodeError" />, never an exception.
/// </summary>
public static class DynamicMessageDecoder
{
    /// <summary>Decodes a message of this type; the message is null unless the error is <see cref="ProtobufDecodeError.None" />.</summary>
    public static ProtobufDecodeError TryDecode(DynamicMessageType type, ReadOnlyMemory<byte> input, out DynamicMessage? message)
    {
        ArgumentNullException.ThrowIfNull(type);
        var decoded = new DynamicMessage(type);
        var error = TryDecodeInto(new ProtobufReader(input), decoded);
        message = error == ProtobufDecodeError.None ? decoded : null;
        return error;
    }

    private static ProtobufDecodeError TryDecodeInto(ProtobufReader reader, DynamicMessage message)
    {
        var error = ProtobufDecodeError.None;
        while (error == ProtobufDecodeError.None && !reader.IsAtEnd)
        {
            error = TryDecodeField(reader, message);
        }

        return error;
    }

    private static ProtobufDecodeError TryDecodeField(ProtobufReader reader, DynamicMessage message)
    {
        var error = reader.TryReadTag(out var fieldNumber, out var wireType);
        if (error != ProtobufDecodeError.None)
        {
            return error;
        }

        var field = message.Type.FindField(fieldNumber);
        return field is not null && IsWireTypeOf(field, wireType)
            ? TryDecodeKnownValue(reader, message, field, wireType)
            : TryDecodeUnknownValue(reader, message, fieldNumber, wireType);
    }

    private static bool IsWireTypeOf(DynamicField field, ProtobufWireType wireType) =>
        wireType == (field.Codec?.WireType ?? ProtobufWireType.LengthDelimited)
        || (wireType == ProtobufWireType.LengthDelimited && field.IsRepeated);

    private static ProtobufDecodeError TryDecodeUnknownValue(ProtobufReader reader, DynamicMessage message, int fieldNumber, ProtobufWireType wireType)
    {
        var error = reader.TryReadFieldValue(fieldNumber, wireType, out var unknownField);
        if (error == ProtobufDecodeError.None)
        {
            message.UnknownFields.Add(unknownField!);
        }

        return error;
    }

    private static ProtobufDecodeError TryDecodeKnownValue(ProtobufReader reader, DynamicMessage message, DynamicField field, ProtobufWireType wireType)
    {
        if (field.Codec is not { } codec)
        {
            return TryDecodeLengthDelimitedValue(reader, message, field);
        }

        if (wireType == ProtobufWireType.LengthDelimited)
        {
            return TryDecodePacked(reader, message.Repeated(field), codec);
        }

        var error = TryReadScalar(reader, codec, out var raw);
        if (error == ProtobufDecodeError.None)
        {
            Store(message, field, codec.FromWire(raw));
        }

        return error;
    }

    private static ProtobufDecodeError TryDecodePacked(ProtobufReader reader, List<object> values, DynamicScalarCodec codec)
    {
        var error = reader.TryReadLengthDelimited(out var payload);
        var packed = new ProtobufReader(payload);
        while (error == ProtobufDecodeError.None && !packed.IsAtEnd)
        {
            error = TryReadScalar(packed, codec, out var raw);
            values.Add(codec.FromWire(raw));
        }

        return error;
    }

    private static ProtobufDecodeError TryReadScalar(ProtobufReader reader, DynamicScalarCodec codec, out ulong raw)
    {
        if (codec.WireType == ProtobufWireType.Varint)
        {
            return reader.TryReadVarint(out raw);
        }

        if (codec.WireType == ProtobufWireType.Fixed64)
        {
            return reader.TryReadFixed64(out raw);
        }

        var error = reader.TryReadFixed32(out var value);
        raw = value;
        return error;
    }

    private static ProtobufDecodeError TryDecodeLengthDelimitedValue(ProtobufReader reader, DynamicMessage message, DynamicField field)
    {
        if (field.Kind == DynamicValueKind.Message)
        {
            return field.IsMap ? TryDecodeMapEntry(reader, message, field) : TryDecodeMessage(reader, message, field);
        }

        var error = reader.TryReadLengthDelimited(out var payload);
        if (error != ProtobufDecodeError.None)
        {
            return error;
        }

        if (field.Kind == DynamicValueKind.Bytes)
        {
            Store(message, field, payload.ToArray());
            return ProtobufDecodeError.None;
        }

        if (!Utf8.IsValid(payload.Span))
        {
            return ProtobufDecodeError.InvalidUtf8;
        }

        Store(message, field, Encoding.UTF8.GetString(payload.Span));
        return ProtobufDecodeError.None;
    }

    private static ProtobufDecodeError TryDecodeMessage(ProtobufReader reader, DynamicMessage message, DynamicField field)
    {
        var error = reader.TryReadEmbeddedMessage(out var embedded);
        if (error != ProtobufDecodeError.None)
        {
            return error;
        }

        var target = field.IsRepeated
            ? new DynamicMessage(field.MessageType!)
            : message.FindValue(field.Number) as DynamicMessage ?? new DynamicMessage(field.MessageType!);
        error = TryDecodeInto(embedded!, target);
        Store(message, field, target);
        return error;
    }

    private static ProtobufDecodeError TryDecodeMapEntry(ProtobufReader reader, DynamicMessage message, DynamicField field)
    {
        var error = reader.TryReadEmbeddedMessage(out var embedded);
        if (error != ProtobufDecodeError.None)
        {
            return error;
        }

        var entry = new DynamicMessage(field.MessageType!);
        error = TryDecodeInto(embedded!, entry);
        message.Map(field)[entry.GetValue("key")] = entry.GetValue("value");
        return error;
    }

    private static void Store(DynamicMessage message, DynamicField field, object value)
    {
        if (field.IsRepeated)
        {
            message.Repeated(field).Add(value);
        }
        else
        {
            message.SetSingular(field, value);
        }
    }
}
