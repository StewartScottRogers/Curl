namespace Curl.Protobuf.Descriptors;

/// <summary>
/// Pins how <see cref="ProtoParser" /> reads services, <c>rpc</c> methods and <c>import</c> statements.
/// </summary>
[TestClass]
public sealed class ProtoServiceParserTests
{
    private const string SyntaxLine = "syntax = \"proto3\";\n";

    [TestMethod]
    public void TryParse_ServiceWithEachStreamingShape_ReadsEveryMethod()
    {
        var file = Parse(
            "service Chat {\n" +
            "  option deprecated = true;\n" +
            "  ;\n" +
            "  rpc Unary (Req) returns (.pkg.Res);\n" +
            "  rpc Client (stream Req) returns (Res);\n" +
            "  rpc Server (Req) returns (stream Res) {}\n" +
            "  rpc Both (stream a.Req) returns (stream Res) { option idempotency_level = NO_SIDE_EFFECTS; ; }\n" +
            "}\n");

        var service = file.Services.Single();
        Assert.AreEqual("Chat", service.Name);
        CollectionAssert.AreEqual(new[] { new ProtoOption("deprecated", "true") }, service.Options.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                ("Unary", "Req", false, ".pkg.Res", false),
                ("Client", "Req", true, "Res", false),
                ("Server", "Req", false, "Res", true),
                ("Both", "a.Req", true, "Res", true),
            },
            service.Methods.Select(method => (method.Name, method.InputType.Name, method.ClientStreaming, method.OutputType.Name, method.ServerStreaming)).ToArray());
        Assert.IsTrue(service.Methods.All(method => method.InputType.Type == ProtoFieldType.Named && method.OutputType.Type == ProtoFieldType.Named));
        CollectionAssert.AreEqual(
            new[] { new ProtoOption("idempotency_level", "NO_SIDE_EFFECTS") },
            service.Methods[3].Options.ToArray());
        Assert.IsEmpty(service.Methods[0].Options);
    }

    [TestMethod]
    public void TryParse_Imports_ReadsEachPathKindAndPosition()
    {
        var file = Parse("import \"a.proto\";\nimport public \"b.proto\";\n  import weak \"c.proto\";\n");

        CollectionAssert.AreEqual(
            new[]
            {
                new ProtoImportDescriptor("a.proto", ProtoImportKind.Default, 2, 1),
                new ProtoImportDescriptor("b.proto", ProtoImportKind.Public, 3, 1),
                new ProtoImportDescriptor("c.proto", ProtoImportKind.Weak, 4, 3),
            },
            file.Imports.ToArray());
    }

    [TestMethod]
    [DataRow("service S { rpc M (int32) returns (R); }", 20, "A method's request and response must be message types, not \"int32\".")]
    [DataRow("service S { rpc M (R) returns (stream string); }", 39, "A method's request and response must be message types, not \"string\".")]
    [DataRow("service S { message M {} }", 13, "Expected a service statement, found \"message\".")]
    [DataRow("service S { rpc M (R) returns (R) { rpc } }", 37, "Expected a method option, found \"rpc\".")]
    [DataRow("service S { rpc M (R) (R); }", 23, "Expected \"returns\", found \"(\".")]
    [DataRow("service S { rpc M (R) returns (R) }", 35, "Expected \"{\", found \"}\".")]
    [DataRow("import foo;", 8, "Expected an import path, found \"foo\".")]
    [DataRow("import public;", 14, "Expected an import path, found \";\".")]
    public void TryParse_MalformedServiceOrImport_FailsAtTheOffendingToken(string body, int column, string message)
    {
        var parsed = ProtoParser.TryParse(SyntaxLine + body, out _, out var error);

        Assert.IsFalse(parsed);
        Assert.AreEqual(new ProtoSyntaxError(2, column, message), error);
    }

    private static ProtoFileDescriptor Parse(string body)
    {
        var parsed = ProtoParser.TryParse(SyntaxLine + body, out var file, out var error);
        Assert.IsTrue(parsed, error?.Message);
        return file!;
    }
}
