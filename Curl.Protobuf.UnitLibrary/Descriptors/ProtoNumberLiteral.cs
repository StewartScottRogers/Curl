namespace Curl.Protobuf.Descriptors;

/// <summary>
/// Tells whether a run of number characters is a proto3 integer literal (decimal, octal
/// <c>0[0-7]*</c> or hexadecimal <c>0x...</c>) or float literal (<c>1.5</c>, <c>5.</c>,
/// <c>.5</c>, <c>1e5</c>, <c>1.5E-3</c>).
/// </summary>
internal static class ProtoNumberLiteral
{
    private const string DecimalDigits = "0123456789";

    private const string OctalDigits = "01234567";

    private const string HexadecimalDigits = "0123456789abcdefABCDEF";

    /// <summary>Whether <paramref name="literal" /> (never empty) is an integer literal.</summary>
    public static bool IsInteger(string literal) =>
        IsHexadecimal(literal) || IsOctal(literal) || IsDecimal(literal);

    /// <summary>Whether <paramref name="literal" /> is a float literal.</summary>
    public static bool IsFloat(string literal)
    {
        var exponentAt = literal.AsSpan().IndexOfAny('e', 'E');
        return exponentAt < 0
            ? IsMantissa(literal, hasExponent: false)
            : IsMantissa(literal[..exponentAt], hasExponent: true) && IsExponent(literal.AsSpan(exponentAt + 1));
    }

    private static bool IsHexadecimal(string literal) =>
        literal.Length > 2 && literal.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && OnlyDigits(literal.AsSpan(2), HexadecimalDigits);

    private static bool IsOctal(string literal) => literal[0] == '0' && OnlyDigits(literal.AsSpan(1), OctalDigits);

    private static bool IsDecimal(string literal) => literal[0] != '0' && OnlyDigits(literal, DecimalDigits);

    private static bool IsMantissa(string mantissa, bool hasExponent)
    {
        var dotAt = mantissa.IndexOf('.', StringComparison.Ordinal);
        if (dotAt < 0)
        {
            return hasExponent && IsDecimalDigits(mantissa);
        }

        var whole = mantissa.AsSpan(0, dotAt);
        var fraction = mantissa.AsSpan(dotAt + 1);
        return OnlyDigits(whole, DecimalDigits) && OnlyDigits(fraction, DecimalDigits) && whole.Length + fraction.Length > 0;
    }

    private static bool IsExponent(ReadOnlySpan<char> exponent) =>
        IsDecimalDigits(exponent.StartsWith("+") || exponent.StartsWith("-") ? exponent[1..] : exponent);

    private static bool IsDecimalDigits(ReadOnlySpan<char> digits) => !digits.IsEmpty && OnlyDigits(digits, DecimalDigits);

    private static bool OnlyDigits(ReadOnlySpan<char> digits, string allowed) => !digits.ContainsAnyExcept(allowed);
}
