namespace Curl.Protobuf.Descriptors;

/// <summary>What a <see cref="ProtoToken" /> is.</summary>
internal enum ProtoTokenKind
{
    /// <summary>A letter or underscore followed by letters, digits and underscores.</summary>
    Identifier,

    /// <summary>A decimal, octal (leading 0) or hexadecimal (leading 0x) integer literal.</summary>
    Integer,

    /// <summary>A floating-point literal.</summary>
    Float,

    /// <summary>A single- or double-quoted string literal.</summary>
    String,

    /// <summary>One punctuation character.</summary>
    Symbol,

    /// <summary>The end of the text.</summary>
    End,
}

/// <summary>One token of <c>.proto</c> text.</summary>
/// <param name="Kind">What the token is.</param>
/// <param name="Text">The token as written, except a string literal, which holds its decoded value.</param>
/// <param name="Line">The 1-based line the token starts on.</param>
/// <param name="Column">The 1-based column the token starts at.</param>
/// <param name="Start">The offset of the token's first character in the text.</param>
/// <param name="End">The offset just past the token's last character in the text.</param>
internal readonly record struct ProtoToken(ProtoTokenKind Kind, string Text, int Line, int Column, int Start, int End);
