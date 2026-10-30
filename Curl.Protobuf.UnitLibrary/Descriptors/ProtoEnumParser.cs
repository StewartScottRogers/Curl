namespace Curl.Protobuf.Descriptors;

/// <summary>Reads an <c>enum</c>: its values, options and reservations.</summary>
internal static class ProtoEnumParser
{
    private static readonly Dictionary<string, Action<ProtoTokenCursor, ProtoEnumBody>> Statements = new()
    {
        ["option"] = (cursor, body) => ProtoOptionParser.ParseOptionStatement(cursor, body.Options),
        ["reserved"] = (cursor, body) => ProtoReservedParser.ParseReserved(cursor, body, int.MinValue, int.MaxValue),
        [";"] = ProtoStatements.ParseEmpty,
    };

    /// <summary>Reads one <c>enum</c> statement.</summary>
    public static ProtoEnumDescriptor ParseEnum(ProtoTokenCursor cursor)
    {
        var keyword = cursor.Expect("enum");
        var name = cursor.Expect(ProtoTokenKind.Identifier, "an enum name").Text;
        var body = new ProtoEnumBody();
        ProtoStatements.ParseBlock(cursor, body, Statements, ParseValue);
        if (body.Values.Count == 0)
        {
            throw ProtoTokenCursor.At(keyword, $"Enum \"{name}\" has no values.");
        }

        if (body.FirstAlias is { } alias && !body.Options.Contains(new ProtoOption("allow_alias", "true")))
        {
            throw ProtoTokenCursor.At(alias, $"Enum \"{name}\" repeats the number {alias.Text} without option allow_alias = true.");
        }

        return new ProtoEnumDescriptor(name, body.Values, body.Options, body.ReservedRanges, body.ReservedNames);
    }

    private static void ParseValue(ProtoTokenCursor cursor, ProtoEnumBody body)
    {
        var name = cursor.Expect(ProtoTokenKind.Identifier, "an enum value name").Text;
        cursor.Expect("=");
        var token = cursor.Peek;
        var number = cursor.ExpectInteger(int.MinValue, int.MaxValue, "an enum value number");
        if (body.Values.Count == 0 && number != 0)
        {
            throw ProtoTokenCursor.At(token, "The first value of a proto3 enum must be 0.");
        }

        if (body.Values.Exists(value => value.Number == number))
        {
            body.FirstAlias ??= token;
        }

        var options = ProtoOptionParser.ParseFieldOptions(cursor);
        cursor.Expect(";");
        body.Values.Add(new ProtoEnumValueDescriptor(name, number, options));
    }
}
