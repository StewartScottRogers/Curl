namespace Curl.Protobuf.Descriptors;

/// <summary>Reads a file: <c>syntax</c> first, then top-level statements from a table.</summary>
internal static class ProtoFileParser
{
    private static readonly Dictionary<string, Action<ProtoTokenCursor, ProtoFileBody>> Statements = new()
    {
        ["package"] = ParsePackage,
        ["option"] = (cursor, file) => ProtoOptionParser.ParseOptionStatement(cursor, file.Options),
        ["message"] = (cursor, file) => file.Messages.Add(ProtoMessageParser.ParseMessage(cursor)),
        ["enum"] = (cursor, file) => file.Enums.Add(ProtoEnumParser.ParseEnum(cursor)),
        [";"] = ProtoStatements.ParseEmpty,
    };

    /// <summary>Reads the whole file.</summary>
    public static ProtoFileDescriptor ParseFile(ProtoTokenCursor cursor)
    {
        var syntax = ParseSyntax(cursor);
        var file = new ProtoFileBody();
        while (cursor.Peek.Kind != ProtoTokenKind.End)
        {
            ProtoStatements.ParseStatement(cursor, file, Statements, ParseUnknownStatement);
        }

        return new ProtoFileDescriptor(syntax, file.Package ?? string.Empty, file.Options, file.Messages, file.Enums);
    }

    private static string ParseSyntax(ProtoTokenCursor cursor)
    {
        cursor.Expect("syntax");
        cursor.Expect("=");
        var syntax = cursor.Expect(ProtoTokenKind.String, "a syntax string");
        cursor.Expect(";");
        return syntax.Text == "proto3"
            ? syntax.Text
            : throw ProtoTokenCursor.At(syntax, $"Unsupported syntax \"{syntax.Text}\"; only \"proto3\" is read.");
    }

    private static void ParsePackage(ProtoTokenCursor cursor, ProtoFileBody file)
    {
        var keyword = cursor.Expect("package");
        if (file.Package is not null)
        {
            throw ProtoTokenCursor.At(keyword, "A file may have only one package statement.");
        }

        file.Package = cursor.ExpectFullIdentifier("a package name");
        cursor.Expect(";");
    }

    private static void ParseUnknownStatement(ProtoTokenCursor cursor, ProtoFileBody file) =>
        throw cursor.Unexpected("a top-level statement");
}
