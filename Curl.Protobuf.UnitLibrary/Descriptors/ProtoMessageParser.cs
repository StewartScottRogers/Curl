using Curl.Protobuf.Wire;

namespace Curl.Protobuf.Descriptors;

/// <summary>Reads a <c>message</c>: its fields, oneofs, maps, nested types, options and reservations.</summary>
internal static class ProtoMessageParser
{
    private const int FirstImplementationReservedNumber = 19000;

    private const int LastImplementationReservedNumber = 19999;

    private static readonly Dictionary<string, Action<ProtoTokenCursor, ProtoMessageBody>> Statements = new()
    {
        ["message"] = (cursor, message) => message.Messages.Add(ParseMessage(cursor)),
        ["enum"] = (cursor, message) => message.Enums.Add(ProtoEnumParser.ParseEnum(cursor)),
        ["option"] = (cursor, message) => ProtoOptionParser.ParseOptionStatement(cursor, message.Options),
        ["oneof"] = ParseOneof,
        ["reserved"] = (cursor, message) =>
            ProtoReservedParser.ParseReserved(cursor, message, 1, ProtobufReader.MaximumFieldNumber),
        [";"] = ProtoStatements.ParseEmpty,
    };

    private static readonly Dictionary<string, Action<ProtoTokenCursor, ProtoOneofBody>> OneofStatements = new()
    {
        ["option"] = (cursor, oneof) => ProtoOptionParser.ParseOptionStatement(cursor, oneof.Options),
        [";"] = ProtoStatements.ParseEmpty,
    };

    private static readonly Dictionary<string, ProtoFieldLabel> Labels = new()
    {
        ["optional"] = ProtoFieldLabel.Optional,
        ["repeated"] = ProtoFieldLabel.Repeated,
    };

    private static readonly Dictionary<string, ProtoFieldType> ScalarTypes = new()
    {
        ["double"] = ProtoFieldType.Double,
        ["float"] = ProtoFieldType.Float,
        ["int32"] = ProtoFieldType.Int32,
        ["int64"] = ProtoFieldType.Int64,
        ["uint32"] = ProtoFieldType.UInt32,
        ["uint64"] = ProtoFieldType.UInt64,
        ["sint32"] = ProtoFieldType.SInt32,
        ["sint64"] = ProtoFieldType.SInt64,
        ["fixed32"] = ProtoFieldType.Fixed32,
        ["fixed64"] = ProtoFieldType.Fixed64,
        ["sfixed32"] = ProtoFieldType.SFixed32,
        ["sfixed64"] = ProtoFieldType.SFixed64,
        ["bool"] = ProtoFieldType.Bool,
        ["string"] = ProtoFieldType.String,
        ["bytes"] = ProtoFieldType.Bytes,
    };

    private static readonly HashSet<ProtoFieldType> MapKeyTypes =
    [
        ProtoFieldType.Int32, ProtoFieldType.Int64, ProtoFieldType.UInt32, ProtoFieldType.UInt64,
        ProtoFieldType.SInt32, ProtoFieldType.SInt64, ProtoFieldType.Fixed32, ProtoFieldType.Fixed64,
        ProtoFieldType.SFixed32, ProtoFieldType.SFixed64, ProtoFieldType.Bool, ProtoFieldType.String,
    ];

    /// <summary>Reads one <c>message</c> statement.</summary>
    public static ProtoMessageDescriptor ParseMessage(ProtoTokenCursor cursor)
    {
        cursor.Expect("message");
        var name = cursor.Expect(ProtoTokenKind.Identifier, "a message name").Text;
        var body = new ProtoMessageBody();
        ProtoStatements.ParseBlock(cursor, body, Statements, (fieldCursor, message) => ParseField(fieldCursor, message, null));
        return new ProtoMessageDescriptor(
            name, body.Fields, body.Oneofs, body.Messages, body.Enums, body.Options, body.ReservedRanges, body.ReservedNames);
    }

    private static void ParseOneof(ProtoTokenCursor cursor, ProtoMessageBody message)
    {
        var keyword = cursor.Expect("oneof");
        var oneof = new ProtoOneofBody(message, cursor.Expect(ProtoTokenKind.Identifier, "a oneof name").Text);
        var fieldCount = message.Fields.Count;
        ProtoStatements.ParseBlock(cursor, oneof, OneofStatements, (fieldCursor, body) => ParseField(fieldCursor, body.Message, body.Name));
        if (message.Fields.Count == fieldCount)
        {
            throw ProtoTokenCursor.At(keyword, $"Oneof \"{oneof.Name}\" has no fields.");
        }

        message.Oneofs.Add(new ProtoOneofDescriptor(oneof.Name, oneof.Options));
    }

    private static void ParseField(ProtoTokenCursor cursor, ProtoMessageBody message, string? oneofName)
    {
        var first = cursor.Peek;
        var label = ParseLabel(cursor);
        if (oneofName is not null && label != ProtoFieldLabel.Singular)
        {
            throw ProtoTokenCursor.At(first, "A field in a oneof may not have a label.");
        }

        var isMap = ProtoTokenCursor.Keyword(cursor.Peek) == "map";
        var (type, key, value) = isMap ? ParseMapType(cursor, first, label, oneofName) : (ParseTypeReference(cursor), null, null);
        var name = cursor.Expect(ProtoTokenKind.Identifier, "a field name").Text;
        cursor.Expect("=");
        var number = ParseFieldNumber(cursor, message);
        var options = ProtoOptionParser.ParseFieldOptions(cursor);
        cursor.Expect(";");
        message.Fields.Add(new ProtoFieldDescriptor(
            name, number, isMap ? ProtoFieldLabel.Repeated : label, type, key, value, oneofName, options));
    }

    private static ProtoFieldLabel ParseLabel(ProtoTokenCursor cursor)
    {
        if (!Labels.TryGetValue(ProtoTokenCursor.Keyword(cursor.Peek), out var label))
        {
            return ProtoFieldLabel.Singular;
        }

        cursor.Next();
        return label;
    }

    private static (ProtoTypeReference Type, ProtoTypeReference? Key, ProtoTypeReference? Value) ParseMapType(
        ProtoTokenCursor cursor, ProtoToken first, ProtoFieldLabel label, string? oneofName)
    {
        if (label != ProtoFieldLabel.Singular || oneofName is not null)
        {
            throw ProtoTokenCursor.At(first, "A map field may not have a label or be in a oneof.");
        }

        cursor.Expect("map");
        cursor.Expect("<");
        var keyToken = cursor.Peek;
        var key = ParseTypeReference(cursor);
        if (!MapKeyTypes.Contains(key.Type))
        {
            throw ProtoTokenCursor.At(keyToken, $"Invalid map key type \"{key.Name}\".");
        }

        cursor.Expect(",");
        var value = ParseTypeReference(cursor);
        cursor.Expect(">");
        return (new ProtoTypeReference(ProtoFieldType.Map, $"map<{key.Name}, {value.Name}>"), key, value);
    }

    private static ProtoTypeReference ParseTypeReference(ProtoTokenCursor cursor)
    {
        var name = (cursor.TryConsume(".") ? "." : string.Empty) + cursor.ExpectFullIdentifier("a field type");
        return new ProtoTypeReference(ScalarTypes.GetValueOrDefault(name, ProtoFieldType.Named), name);
    }

    private static int ParseFieldNumber(ProtoTokenCursor cursor, ProtoMessageBody message)
    {
        var token = cursor.Peek;
        var number = cursor.ExpectInteger(1, ProtobufReader.MaximumFieldNumber, "a field number");
        if (number is >= FirstImplementationReservedNumber and <= LastImplementationReservedNumber)
        {
            throw ProtoTokenCursor.At(token, $"Field numbers {FirstImplementationReservedNumber} to {LastImplementationReservedNumber} are reserved for the Protocol Buffers implementation.");
        }

        var used = message.Fields.Find(field => field.Number == number);
        return used is null
            ? number
            : throw ProtoTokenCursor.At(token, $"Field number {number} is already used by \"{used.Name}\".");
    }
}
