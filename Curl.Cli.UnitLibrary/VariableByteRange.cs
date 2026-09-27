namespace Curl.Cli;

/// <summary>
/// The <c>[start-end]</c> or <c>[start-]</c> byte range a <c>--variable</c> name may carry (curl 8.12.0
/// and later): the bytes from offset <see cref="Start"/> to offset <see cref="End"/>, both included.
/// </summary>
/// <param name="Start">The first offset kept.</param>
/// <param name="End">The last offset kept; <see cref="long.MaxValue"/> for the end of the content.</param>
internal readonly record struct VariableByteRange(long Start, long End)
{
    /// <summary>The range of a name that carries none: every byte.</summary>
    internal static VariableByteRange Whole { get; } = new(0, long.MaxValue);

    /// <summary>
    /// Reads a range from the start of <paramref name="rest"/> when it opens with <c>[</c> and a digit, as
    /// curl 8.21.0's <c>setvariable</c> does, and moves <paramref name="rest"/> past it; anything else is no
    /// range. Offsets are decimal and at most <see cref="long.MaxValue"/>.
    /// </summary>
    /// <param name="rest">The value after the variable name; on success, what follows the range.</param>
    /// <param name="range">The range read, or <see cref="Whole"/> when there is none.</param>
    /// <returns><see langword="false"/> when the range is malformed or its start is past its end.</returns>
    internal static bool TryRead(ref string rest, out VariableByteRange range)
    {
        range = Whole;
        if (rest.Length < 2 || rest[0] != '[' || !char.IsAsciiDigit(rest[1]))
        {
            return true;
        }

        int position = 1;
        if (!TryReadOffsets(rest, ref position, out long start, out long end))
        {
            return false;
        }

        range = new VariableByteRange(start, end);
        rest = rest[position..];
        return start <= end;
    }

    /// <summary>
    /// Cuts <paramref name="content"/> to the range: empty when <see cref="Start"/> is at or past its end,
    /// otherwise up to <see cref="End"/> or the last byte, whichever comes first.
    /// </summary>
    /// <param name="content">The variable's bytes.</param>
    /// <returns>The bytes in the range.</returns>
    internal byte[] Cut(byte[] content)
    {
        if (Start >= content.Length)
        {
            return [];
        }

        long last = Math.Min(End, content.Length - 1);
        return content[(int)Start..(int)(last + 1)];
    }

    /// <summary>Reads <c>start-]</c> or <c>start-end]</c> from <paramref name="position"/>, just after the <c>[</c>.</summary>
    private static bool TryReadOffsets(string text, ref int position, out long start, out long end)
    {
        end = long.MaxValue;
        if (!TryReadNumber(text, ref position, out start) || !TryReadCharacter(text, ref position, '-'))
        {
            return false;
        }

        return TryReadCharacter(text, ref position, ']')
            || (TryReadNumber(text, ref position, out end) && TryReadCharacter(text, ref position, ']'));
    }

    private static bool TryReadCharacter(string text, ref int position, char expected)
    {
        if (position < text.Length && text[position] == expected)
        {
            position++;
            return true;
        }

        return false;
    }

    /// <summary>Reads one or more decimal digits as curl's <c>curlx_str_number</c> does, refusing a value past <see cref="long.MaxValue"/>.</summary>
    private static bool TryReadNumber(string text, ref int position, out long number)
    {
        number = 0;
        int start = position;
        while (position < text.Length && char.IsAsciiDigit(text[position]))
        {
            int digit = text[position] - '0';
            if (number > (long.MaxValue - digit) / 10)
            {
                return false;
            }

            number = (number * 10) + digit;
            position++;
        }

        return position > start;
    }
}
