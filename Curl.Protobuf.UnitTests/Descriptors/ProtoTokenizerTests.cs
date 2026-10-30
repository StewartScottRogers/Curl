namespace Curl.Protobuf.Descriptors;

/// <summary>
/// Pins how <see cref="ProtoTokenizer" /> splits <c>.proto</c> text into tokens.
/// </summary>
[TestClass]
public sealed class ProtoTokenizerTests
{
    [TestMethod]
    public void Tokenize_StringWithEveryEscape_DecodesTheEscapes()
    {
        var token = ProtoTokenizer.Tokenize("\"\\a\\b\\f\\n\\r\\t\\v\\\\\\'\\\"\\?\\x41\\X42\\101\\18\"")[0];

        Assert.AreEqual((ProtoTokenKind.String, "\a\b\f\n\r\t\v\\'\"?ABA\u00018"), (token.Kind, token.Text));
    }

    [TestMethod]
    public void Tokenize_SingleQuotedString_ReadsAString()
    {
        var token = ProtoTokenizer.Tokenize("'it\"s'")[0];

        Assert.AreEqual((ProtoTokenKind.String, "it\"s"), (token.Kind, token.Text));
    }

    [TestMethod]
    [DataRow("1.5")]
    [DataRow("5.")]
    [DataRow(".5")]
    [DataRow("1e5")]
    [DataRow("1E-5")]
    [DataRow("1e+5")]
    public void Tokenize_FloatLiteral_IsAFloat(string text)
    {
        Assert.AreEqual((ProtoTokenKind.Float, text), Kind(ProtoTokenizer.Tokenize(text)[0]));
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("017")]
    [DataRow("0x1F")]
    [DataRow("42")]
    public void Tokenize_IntegerLiteral_IsAnInteger(string text)
    {
        Assert.AreEqual((ProtoTokenKind.Integer, text), Kind(ProtoTokenizer.Tokenize(text)[0]));
    }

    [TestMethod]
    public void Tokenize_NumberThenMinusThenDotIdentifier_SplitsAtEachSymbol()
    {
        var tokens = ProtoTokenizer.Tokenize("5-.x");

        CollectionAssert.AreEqual(
            new[]
            {
                (ProtoTokenKind.Integer, "5"), (ProtoTokenKind.Symbol, "-"), (ProtoTokenKind.Symbol, "."),
                (ProtoTokenKind.Identifier, "x"), (ProtoTokenKind.End, string.Empty),
            },
            tokens.Select(Kind).ToArray());
    }

    [TestMethod]
    public void Tokenize_TokensOnSeveralLines_RecordsLineColumnAndOffsets()
    {
        var token = ProtoTokenizer.Tokenize("a\n  bc")[1];

        Assert.AreEqual(new ProtoToken(ProtoTokenKind.Identifier, "bc", 2, 3, 4, 6), token);
    }

    private static (ProtoTokenKind Kind, string Text) Kind(ProtoToken token) => (token.Kind, token.Text);
}
