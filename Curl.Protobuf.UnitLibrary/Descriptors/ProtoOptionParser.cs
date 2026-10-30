namespace Curl.Protobuf.Descriptors;

/// <summary>Reads options: <c>option name = value;</c> statements and <c>[name = value, ...]</c> lists.</summary>
internal static class ProtoOptionParser
{
    /// <summary>Reads one <c>option</c> statement into the options given.</summary>
    public static void ParseOptionStatement(ProtoTokenCursor cursor, List<ProtoOption> options)
    {
        cursor.Expect("option");
        options.Add(ParseOption(cursor));
        cursor.Expect(";");
    }

    /// <summary>Reads a field's or enum value's <c>[...]</c> options; none when there is no <c>[</c>.</summary>
    public static List<ProtoOption> ParseFieldOptions(ProtoTokenCursor cursor)
    {
        var options = new List<ProtoOption>();
        if (!cursor.TryConsume("["))
        {
            return options;
        }

        do
        {
            options.Add(ParseOption(cursor));
        }
        while (cursor.TryConsume(","));

        cursor.Expect("]");
        return options;
    }

    private static ProtoOption ParseOption(ProtoTokenCursor cursor)
    {
        var nameStart = cursor.Peek;
        do
        {
            ParseOptionNamePart(cursor);
        }
        while (cursor.TryConsume("."));

        var name = cursor.SourceFrom(nameStart);
        cursor.Expect("=");
        var valueStart = cursor.Peek;
        ParseConstant(cursor);
        return new ProtoOption(name, cursor.SourceFrom(valueStart));
    }

    private static void ParseOptionNamePart(ProtoTokenCursor cursor)
    {
        if (!cursor.TryConsume("("))
        {
            cursor.Expect(ProtoTokenKind.Identifier, "an option name");
            return;
        }

        cursor.TryConsume(".");
        cursor.ExpectFullIdentifier("an option name");
        cursor.Expect(")");
    }

    private static void ParseConstant(ProtoTokenCursor cursor)
    {
        if (cursor.Peek.Kind == ProtoTokenKind.String)
        {
            ParseStrings(cursor);
        }
        else if (cursor.TryConsume("{"))
        {
            SkipAggregate(cursor);
        }
        else if (cursor.TryConsume("-") || cursor.TryConsume("+"))
        {
            ParseSignedNumber(cursor);
        }
        else if (cursor.Peek.Kind == ProtoTokenKind.Identifier)
        {
            cursor.ExpectFullIdentifier("an option value");
        }
        else
        {
            ParseNumber(cursor);
        }
    }

    private static void ParseStrings(ProtoTokenCursor cursor)
    {
        while (cursor.Peek.Kind == ProtoTokenKind.String)
        {
            cursor.Next();
        }
    }

    private static void ParseSignedNumber(ProtoTokenCursor cursor)
    {
        if (ProtoTokenCursor.Keyword(cursor.Peek) is "inf" or "nan")
        {
            cursor.Next();
            return;
        }

        ParseNumber(cursor);
    }

    private static void ParseNumber(ProtoTokenCursor cursor)
    {
        if (cursor.Peek.Kind is not (ProtoTokenKind.Integer or ProtoTokenKind.Float))
        {
            throw cursor.Unexpected("an option value");
        }

        cursor.Next();
    }

    private static void SkipAggregate(ProtoTokenCursor cursor)
    {
        var depth = 1;
        while (depth > 0)
        {
            var token = cursor.Next();
            if (token.Kind == ProtoTokenKind.End)
            {
                throw ProtoTokenCursor.At(token, "Unterminated option aggregate value.");
            }

            var keyword = ProtoTokenCursor.Keyword(token);
            depth += keyword == "{" ? 1 : keyword == "}" ? -1 : 0;
        }
    }
}
