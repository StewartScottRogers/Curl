using System.Globalization;
using System.Text;

namespace Curl.Conformance;

/// <summary>
/// The character macros and inline instructions of a test file line, applied after its
/// variables, as <c>subchars</c> and <c>subbase64</c> in upstream's <c>testutil.pm</c> apply them
/// at <c>curl-8_21_0</c>.
/// </summary>
/// <remarks>
/// Instruction names match case-insensitively, the macros case-sensitively, as upstream's regular
/// expressions do. Each instruction is replaced from the leftmost match, repeatedly, until none
/// is left. Inside an instruction, <c>%XX</c> hexadecimal pairs become the byte they name.
/// </remarks>
internal static class UpstreamTestInstructions
{
    private static readonly (string Macro, string Replacement)[] CharacterMacros =
    [
        ("%SP", " "), ("%TAB", "\t"), ("%CR", "\r"), ("%LT", "<"), ("%GT", ">"), ("%AMP", "&"),
    ];

    private static readonly (string Marker, string Name, StringComparison Comparison)[] UnsupportedMarkers =
    [
        ("%days[", "%days", StringComparison.OrdinalIgnoreCase),
    ];

    /// <summary>Replaces <c>%SP</c>, <c>%TAB</c>, <c>%CR</c>, <c>%LT</c>, <c>%GT</c> and <c>%AMP</c>, in that order.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The line with every macro replaced.</returns>
    public static string ReplaceCharacterMacros(string line) =>
        CharacterMacros.Aggregate(line, (text, macro) => text.Replace(macro.Macro, macro.Replacement, StringComparison.Ordinal));

    /// <summary>Replaces every <c>%b64[…]b64%</c>, then every <c>%hex[…]hex%</c>, then every <c>%repeat[N x …]%</c>.</summary>
    /// <param name="line">The line, one character per byte.</param>
    /// <returns>The line with every instruction replaced.</returns>
    public static string Apply(string line)
    {
        string text = ReplaceEach(line, "%b64[", "]b64%", content => Convert.ToBase64String(Encoding.Latin1.GetBytes(DecodePercentPairs(content))));
        text = ReplaceEach(text, "%hex[", "]hex%", DecodePercentPairs);
        return ReplaceRepeats(text);
    }

    /// <summary>
    /// Adds the name of every instruction in the line that this harness does not carry out
    /// (<c>%days</c>) to the list, once each, matching case-insensitively as upstream's expressions
    /// do. <c>%include</c> and <c>%includetext</c> are <see cref="UpstreamTestFileInclusions"/>'s,
    /// <c>%sha256b64file</c> and <c>%strippemfile</c> <see cref="UpstreamTestFileContentInstructions"/>'s.
    /// </summary>
    /// <param name="line">The expanded line.</param>
    /// <param name="unsupported">The names found so far.</param>
    public static void AddUnsupported(string line, List<string> unsupported)
    {
        foreach ((string marker, string name, StringComparison comparison) in UnsupportedMarkers)
        {
            if (line.Contains(marker, comparison) && !unsupported.Contains(name))
            {
                unsupported.Add(name);
            }
        }
    }

    /// <summary>Replaces the leftmost <paramref name="opening"/>…<paramref name="closing"/>, matched case-insensitively, repeatedly, until none is left.</summary>
    /// <param name="text">The text.</param>
    /// <param name="opening">The instruction's opening, such as <c>%b64[</c>.</param>
    /// <param name="closing">The instruction's closing, such as <c>]b64%</c>.</param>
    /// <param name="replace">Gives the replacement for the content between them.</param>
    /// <returns>The text with every instruction replaced.</returns>
    public static string ReplaceEach(string text, string opening, string closing, Func<string, string> replace)
    {
        while (true)
        {
            int start = text.IndexOf(opening, StringComparison.OrdinalIgnoreCase);
            int end = start < 0 ? -1 : text.IndexOf(closing, start + opening.Length, StringComparison.OrdinalIgnoreCase);
            if (end < 0)
            {
                return text;
            }

            string content = text[(start + opening.Length)..end];
            text = string.Concat(text.AsSpan(0, start), replace(content), text.AsSpan(end + closing.Length));
        }
    }

    private static string ReplaceRepeats(string text)
    {
        int searchFrom = 0;
        while (true)
        {
            int start = text.IndexOf("%repeat[", searchFrom, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return text;
            }

            string? replaced = TryReplaceRepeatAt(text, start);
            searchFrom = replaced is null ? start + 1 : 0;
            text = replaced ?? text;
        }
    }

    private static string? TryReplaceRepeatAt(string text, int start)
    {
        int countStart = start + "%repeat[".Length;
        int countEnd = countStart;
        while (countEnd < text.Length && char.IsAsciiDigit(text[countEnd]))
        {
            countEnd++;
        }

        int contentStart = countEnd + " x ".Length;
        int end = FindRepeatEnd(text, countEnd, contentStart);
        if (end < 0 || !int.TryParse(text.AsSpan(countStart, countEnd - countStart), NumberStyles.None, CultureInfo.InvariantCulture, out int count))
        {
            return null;
        }

        string repeated = string.Concat(Enumerable.Repeat(DecodePercentPairs(text[contentStart..end]), count));
        return string.Concat(text.AsSpan(0, start), repeated, text.AsSpan(end + 2));
    }

    /// <summary>
    /// The index of the <c>]%</c> that closes a repeat whose count ends at <paramref name="countEnd"/>,
    /// or -1 when no <c> x </c> follows the count or the content would cross a line feed (which
    /// upstream's <c>.*?</c> does not match, and which an earlier <c>%hex[%0a]hex%</c> can put there).
    /// </summary>
    private static int FindRepeatEnd(string text, int countEnd, int contentStart)
    {
        if (string.Compare(text, countEnd, " x ", 0, 3, StringComparison.OrdinalIgnoreCase) != 0)
        {
            return -1;
        }

        int end = text.IndexOf("]%", contentStart, StringComparison.Ordinal);
        int lineFeed = text.IndexOf('\n', contentStart);
        return lineFeed >= 0 && lineFeed < end ? -1 : end;
    }

    /// <summary>Turns every <c>%XX</c> hexadecimal pair into the byte it names, one character per byte.</summary>
    /// <param name="content">The text.</param>
    /// <returns>The decoded text.</returns>
    public static string DecodePercentPairs(string content)
    {
        StringBuilder output = new(content.Length);
        for (int index = 0; index < content.Length; index++)
        {
            if (content[index] == '%' && index + 2 < content.Length && char.IsAsciiHexDigit(content[index + 1]) && char.IsAsciiHexDigit(content[index + 2]))
            {
                output.Append((char)int.Parse(content.AsSpan(index + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                index += 2;
            }
            else
            {
                output.Append(content[index]);
            }
        }

        return output.ToString();
    }
}
