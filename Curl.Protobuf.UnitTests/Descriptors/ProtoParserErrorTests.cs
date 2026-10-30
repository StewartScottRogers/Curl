namespace Curl.Protobuf.Descriptors;

/// <summary>
/// Pins how <see cref="ProtoParser" /> reports malformed <c>.proto</c> text: a typed
/// <see cref="ProtoSyntaxError" /> naming line and column, never an exception.
/// </summary>
[TestClass]
public sealed class ProtoParserErrorTests
{
    private const string SyntaxLine = "syntax = \"proto3\";\n";

    [TestMethod]
    [DataRow("", 1, "Expected \"syntax\", found end of input.")]
    [DataRow("package a;", 1, "Expected \"syntax\", found \"package\".")]
    [DataRow("syntax = proto3;", 10, "Expected a syntax string, found \"proto3\".")]
    [DataRow("syntax = \"proto2\";", 10, "Unsupported syntax \"proto2\"; only \"proto3\" is read.")]
    public void TryParse_BadSyntaxStatement_FailsOnTheFirstLine(string text, int column, string message)
    {
        Assert.AreEqual(new ProtoSyntaxError(1, column, message), ParseError(text));
    }

    [TestMethod]
    [DataRow("package a; package b;", 12, "A file may have only one package statement.")]
    [DataRow("foo", 1, "Expected a top-level statement, found \"foo\".")]
    [DataRow("5", 1, "Expected a top-level statement, found \"5\".")]
    [DataRow("message A {", 12, "Expected a field type, found end of input.")]
    [DataRow("message A { int32 = 1; }", 19, "Expected a field name, found \"=\".")]
    [DataRow("message A { int32 x = 1 }", 25, "Expected \";\", found \"}\".")]
    [DataRow("message A { int32 x = 1 [a = 1; }", 31, "Expected \"]\", found \";\".")]
    [DataRow("message A { int32 x = 1; int32 y = 1; }", 36, "Field number 1 is already used by \"x\".")]
    [DataRow("message A { int32 x = 19000; }", 23, "Field numbers 19000 to 19999 are reserved for the Protocol Buffers implementation.")]
    [DataRow("message A { int32 x = 0; }", 23, "Number 0 is out of range (1 to 536870911).")]
    [DataRow("message A { int32 x = -1; }", 23, "Number -1 is out of range (1 to 536870911).")]
    [DataRow("message A { int32 x = 536870912; }", 23, "Number 536870912 is out of range (1 to 536870911).")]
    [DataRow("message A { int32 x = 9223372036854775808; }", 23, "Number 9223372036854775808 is out of range (1 to 536870911).")]
    [DataRow("message A { int32 x = 99999999999999999999; }", 23, "Number 99999999999999999999 is out of range (1 to 536870911).")]
    [DataRow("message A { oneof o { } }", 13, "Oneof \"o\" has no fields.")]
    [DataRow("message A { oneof o { repeated int32 x = 1; } }", 23, "A field in a oneof may not have a label.")]
    [DataRow("message A { oneof o { map<int32, int32> m = 1; } }", 23, "A map field may not have a label or be in a oneof.")]
    [DataRow("message A { repeated map<int32, int32> m = 1; }", 13, "A map field may not have a label or be in a oneof.")]
    [DataRow("message A { map<float, int32> m = 1; }", 17, "Invalid map key type \"float\".")]
    [DataRow("message A { map<B, int32> m = 1; }", 17, "Invalid map key type \"B\".")]
    [DataRow("message A { reserved 5 to 2; }", 22, "Reserved range 5 to 2 ends before it starts.")]
    [DataRow("message A { reserved 1 to x; }", 27, "Expected a reserved number or \"max\", found \"x\".")]
    [DataRow("enum E { }", 1, "Enum \"E\" has no values.")]
    [DataRow("enum E { A = 1; }", 14, "The first value of a proto3 enum must be 0.")]
    [DataRow("enum E { A = 0; B = -2147483649; }", 21, "Number -2147483649 is out of range (-2147483648 to 2147483647).")]
    [DataRow("enum E { A = 0; B = 0; }", 21, "Enum \"E\" repeats the number 0 without option allow_alias = true.")]
    [DataRow("enum E { option allow_alias = false; A = 0; B = 0; C = 0; }", 49, "Enum \"E\" repeats the number 0 without option allow_alias = true.")]
    [DataRow("option x = ;", 12, "Expected an option value, found \";\".")]
    [DataRow("option x = -;", 13, "Expected an option value, found \";\".")]
    [DataRow("option x = { a: { b: 1 }", 25, "Unterminated option aggregate value.")]
    [DataRow("option (a.b = 1;", 13, "Expected \")\", found \"=\".")]
    public void TryParse_MalformedStatement_FailsAtTheOffendingToken(string body, int column, string message)
    {
        Assert.AreEqual(new ProtoSyntaxError(2, column, message), ParseError(SyntaxLine + body));
    }

    [TestMethod]
    [DataRow("@", 1, "Unexpected character '@'.")]
    [DataRow("option x = 08;", 12, "Invalid number \"08\".")]
    [DataRow("option x = 1_0;", 12, "Invalid number \"1_0\".")]
    [DataRow("option x = \"abc", 12, "Unterminated string literal.")]
    [DataRow("option x = \"a\nb\";", 12, "Unterminated string literal.")]
    [DataRow("option x = \"\\q\";", 14, "Invalid escape sequence in string literal.")]
    [DataRow("option x = \"\\!\";", 14, "Invalid escape sequence in string literal.")]
    [DataRow("option x = \"\\xg\";", 15, "Expected a hexadecimal digit after \\x.")]
    [DataRow("/* abc", 1, "Unterminated block comment.")]
    public void TryParse_MalformedToken_FailsAtTheToken(string body, int column, string message)
    {
        Assert.AreEqual(new ProtoSyntaxError(2, column, message), ParseError(SyntaxLine + body));
    }

    [TestMethod]
    public void TryParse_ErrorAfterMultiLineComment_CountsTheCommentsLines()
    {
        var error = ParseError(SyntaxLine + "/* one\ntwo */ // three\n\n  bad");

        Assert.AreEqual(new ProtoSyntaxError(5, 3, "Expected a top-level statement, found \"bad\"."), error);
    }

    private static ProtoSyntaxError ParseError(string text)
    {
        var parsed = ProtoParser.TryParse(text, out var file, out var error);

        Assert.IsFalse(parsed);
        Assert.IsNull(file);
        Assert.IsNotNull(error);
        return error;
    }
}
