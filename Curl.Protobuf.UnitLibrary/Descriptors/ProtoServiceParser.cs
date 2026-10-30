namespace Curl.Protobuf.Descriptors;

/// <summary>Reads a <c>service</c>: its options and its <c>rpc</c> methods, <c>stream</c> on either side.</summary>
internal static class ProtoServiceParser
{
    private static readonly Dictionary<string, Action<ProtoTokenCursor, ProtoServiceBody>> Statements = new()
    {
        ["option"] = (cursor, service) => ProtoOptionParser.ParseOptionStatement(cursor, service.Options),
        ["rpc"] = ParseMethod,
        [";"] = ProtoStatements.ParseEmpty,
    };

    private static readonly Dictionary<string, Action<ProtoTokenCursor, List<ProtoOption>>> MethodStatements = new()
    {
        ["option"] = ProtoOptionParser.ParseOptionStatement,
        [";"] = ProtoStatements.ParseEmpty,
    };

    /// <summary>Reads one <c>service</c> statement.</summary>
    public static ProtoServiceDescriptor ParseService(ProtoTokenCursor cursor)
    {
        cursor.Expect("service");
        var name = cursor.Expect(ProtoTokenKind.Identifier, "a service name").Text;
        var body = new ProtoServiceBody();
        ProtoStatements.ParseBlock(cursor, body, Statements, (statementCursor, _) => throw statementCursor.Unexpected("a service statement"));
        return new ProtoServiceDescriptor(name, body.Methods, body.Options);
    }

    private static void ParseMethod(ProtoTokenCursor cursor, ProtoServiceBody service)
    {
        cursor.Expect("rpc");
        var name = cursor.Expect(ProtoTokenKind.Identifier, "a method name").Text;
        var (clientStreaming, input) = ParseStreamType(cursor);
        cursor.Expect("returns");
        var (serverStreaming, output) = ParseStreamType(cursor);
        var options = new List<ProtoOption>();
        if (!cursor.TryConsume(";"))
        {
            ProtoStatements.ParseBlock(cursor, options, MethodStatements, (statementCursor, _) => throw statementCursor.Unexpected("a method option"));
        }

        service.Methods.Add(new ProtoMethodDescriptor(name, input, clientStreaming, output, serverStreaming, options));
    }

    /// <summary>Reads <c>( [stream] Type )</c>; the type must name a message, not a scalar.</summary>
    private static (bool Streaming, ProtoTypeReference Type) ParseStreamType(ProtoTokenCursor cursor)
    {
        cursor.Expect("(");
        var streaming = cursor.TryConsume("stream");
        var token = cursor.Peek;
        var type = ProtoMessageParser.ParseTypeReference(cursor);
        cursor.Expect(")");
        return type.Type == ProtoFieldType.Named
            ? (streaming, type)
            : throw ProtoTokenCursor.At(token, $"A method's request and response must be message types, not \"{type.Name}\".");
    }
}
