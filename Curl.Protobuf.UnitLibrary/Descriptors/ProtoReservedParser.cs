namespace Curl.Protobuf.Descriptors;

/// <summary>Reads a <c>reserved</c> statement: numbers and ranges, or names.</summary>
internal static class ProtoReservedParser
{
    /// <summary>Reads one <c>reserved</c> statement, numbers bounded by the minimum and maximum given (<c>max</c> is the maximum).</summary>
    public static void ParseReserved(ProtoTokenCursor cursor, ProtoReservedBody body, int minimum, int maximum)
    {
        cursor.Expect("reserved");
        if (cursor.Peek.Kind == ProtoTokenKind.String)
        {
            ParseNames(cursor, body);
        }
        else
        {
            ParseRanges(cursor, body, minimum, maximum);
        }

        cursor.Expect(";");
    }

    private static void ParseNames(ProtoTokenCursor cursor, ProtoReservedBody body)
    {
        do
        {
            body.ReservedNames.Add(cursor.Expect(ProtoTokenKind.String, "a reserved name").Text);
        }
        while (cursor.TryConsume(","));
    }

    private static void ParseRanges(ProtoTokenCursor cursor, ProtoReservedBody body, int minimum, int maximum)
    {
        do
        {
            body.ReservedRanges.Add(ParseRange(cursor, minimum, maximum));
        }
        while (cursor.TryConsume(","));
    }

    private static ProtoReservedRange ParseRange(ProtoTokenCursor cursor, int minimum, int maximum)
    {
        var first = cursor.Peek;
        var start = cursor.ExpectInteger(minimum, maximum, "a reserved number");
        if (!cursor.TryConsume("to"))
        {
            return new ProtoReservedRange(start, start);
        }

        var end = cursor.TryConsume("max") ? maximum : cursor.ExpectInteger(minimum, maximum, "a reserved number or \"max\"");
        return end >= start
            ? new ProtoReservedRange(start, end)
            : throw ProtoTokenCursor.At(first, $"Reserved range {start} to {end} ends before it starts.");
    }
}
