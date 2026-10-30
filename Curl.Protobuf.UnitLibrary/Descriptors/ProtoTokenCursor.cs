namespace Curl.Protobuf.Descriptors;

/// <summary>Walks the tokens of one <c>.proto</c> text, throwing a <see cref="ProtoSyntaxException" /> where one is not what is expected.</summary>
internal sealed class ProtoTokenCursor(string source, List<ProtoToken> tokens)
{
    private int index;

    /// <summary>The first token of each type reference read through this cursor, by reference identity.</summary>
    public Dictionary<ProtoTypeReference, ProtoToken> ReferenceTokens { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>The next token, not consumed.</summary>
    public ProtoToken Peek => tokens[index];

    /// <summary>The keyword or symbol a token spells: its text for an identifier or symbol, empty otherwise.</summary>
    public static string Keyword(ProtoToken token) =>
        token.Kind is ProtoTokenKind.Identifier or ProtoTokenKind.Symbol ? token.Text : string.Empty;

    /// <summary>An error at a token.</summary>
    public static ProtoSyntaxException At(ProtoToken token, string message) =>
        new(new ProtoSyntaxError(token.Line, token.Column, message));

    /// <summary>Consumes the next token; at the end it stays at the end.</summary>
    public ProtoToken Next()
    {
        var token = tokens[index];
        index += token.Kind == ProtoTokenKind.End ? 0 : 1;
        return token;
    }

    /// <summary>Consumes the next token when it is the keyword or symbol given.</summary>
    public bool TryConsume(string keyword)
    {
        if (Keyword(Peek) != keyword)
        {
            return false;
        }

        index++;
        return true;
    }

    /// <summary>Consumes the keyword or symbol given, or throws.</summary>
    public ProtoToken Expect(string keyword) =>
        Keyword(Peek) == keyword ? Next() : throw Unexpected($"\"{keyword}\"");

    /// <summary>Consumes a token of the kind given, or throws naming what was expected.</summary>
    public ProtoToken Expect(ProtoTokenKind kind, string description) =>
        Peek.Kind == kind ? Next() : throw Unexpected(description);

    /// <summary>Consumes identifiers joined by '.', and returns them joined.</summary>
    public string ExpectFullIdentifier(string description)
    {
        var parts = new List<string> { Expect(ProtoTokenKind.Identifier, description).Text };
        while (TryConsume("."))
        {
            parts.Add(Expect(ProtoTokenKind.Identifier, "an identifier").Text);
        }

        return string.Join('.', parts);
    }

    /// <summary>Consumes an integer, '-' before it allowed, from the minimum to the maximum given, or throws.</summary>
    public int ExpectInteger(int minimum, int maximum, string description)
    {
        var first = Peek;
        var negative = TryConsume("-");
        var token = Expect(ProtoTokenKind.Integer, description);
        var written = (negative ? "-" : string.Empty) + token.Text;
        var value = SignedValue(token.Text, negative);
        return value >= minimum && value <= maximum ? (int)value : throw OutOfRange(first, written, minimum, maximum);
    }

    /// <summary>The literal's value, or <see cref="long.MaxValue" />, out of every int range, past 63 bits.</summary>
    private static long SignedValue(string literal, bool negative)
    {
        if (!ProtoIntegerLiteral.TryParse(literal, out var magnitude) || magnitude > long.MaxValue)
        {
            return long.MaxValue;
        }

        return negative ? -(long)magnitude : (long)magnitude;
    }

    /// <summary>The source text from the first token given to the last token consumed.</summary>
    public string SourceFrom(ProtoToken first) => source[first.Start..tokens[index - 1].End];

    /// <summary>An error at the next token: expected one thing, found it.</summary>
    public ProtoSyntaxException Unexpected(string expected)
    {
        var found = Peek.Kind == ProtoTokenKind.End ? "end of input" : $"\"{Peek.Text}\"";
        return At(Peek, $"Expected {expected}, found {found}.");
    }

    private static ProtoSyntaxException OutOfRange(ProtoToken token, string written, int minimum, int maximum) =>
        At(token, $"Number {written} is out of range ({minimum} to {maximum}).");
}
