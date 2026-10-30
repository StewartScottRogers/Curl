namespace Curl.Protobuf.Descriptors;

/// <summary>Reads the value of an integer literal the tokenizer has already checked the form of.</summary>
internal static class ProtoIntegerLiteral
{
    /// <summary>Reads a decimal, octal (leading 0) or hexadecimal (leading 0x) literal; false when it does not fit 64 bits.</summary>
    public static bool TryParse(string literal, out ulong value)
    {
        var (digits, radix) = SplitRadix(literal);
        value = 0;
        foreach (var character in digits)
        {
            var digit = (ulong)Convert.ToInt32(character.ToString(), 16);
            if (value > (ulong.MaxValue - digit) / radix)
            {
                return false;
            }

            value = (value * radix) + digit;
        }

        return true;
    }

    private static (string Digits, ulong Radix) SplitRadix(string literal)
    {
        if (literal.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return (literal[2..], 16UL);
        }

        return literal.Length > 1 && literal[0] == '0' ? (literal[1..], 8UL) : (literal, 10UL);
    }
}
