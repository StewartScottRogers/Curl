namespace Curl.Protobuf.Descriptors;

/// <summary>Reads statements by their first keyword from a table, so a new statement is one more entry.</summary>
internal static class ProtoStatements
{
    /// <summary>Reads one statement: the table's entry for the next keyword, or the default.</summary>
    public static void ParseStatement<TBody>(
        ProtoTokenCursor cursor,
        TBody body,
        IReadOnlyDictionary<string, Action<ProtoTokenCursor, TBody>> statements,
        Action<ProtoTokenCursor, TBody> otherwise)
    {
        var parse = statements.GetValueOrDefault(ProtoTokenCursor.Keyword(cursor.Peek), otherwise);
        parse(cursor, body);
    }

    /// <summary>Reads <c>{</c>, statements up to <c>}</c>, and the <c>}</c>.</summary>
    public static void ParseBlock<TBody>(
        ProtoTokenCursor cursor,
        TBody body,
        IReadOnlyDictionary<string, Action<ProtoTokenCursor, TBody>> statements,
        Action<ProtoTokenCursor, TBody> otherwise)
    {
        cursor.Expect("{");
        while (!cursor.TryConsume("}"))
        {
            ParseStatement(cursor, body, statements, otherwise);
        }
    }

    /// <summary>Reads the empty statement, a lone <c>;</c>.</summary>
    public static void ParseEmpty<TBody>(ProtoTokenCursor cursor, TBody body) => cursor.Expect(";");
}
