using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Names where two byte sequences first differ, with the line of each around that point, so a
/// failing case says what to fix without rerunning it.
/// </summary>
internal static class UpstreamFirstDifference
{
    private const int MaximumShownCharacters = 200;

    private static readonly Dictionary<char, string> NamedEscapes = new()
    {
        ['\r'] = "\\r",
        ['\n'] = "\\n",
        ['\t'] = "\\t",
        ['"'] = "\\\"",
        ['\\'] = "\\\\",
    };

    /// <summary>Describes the first difference between what was expected and what was produced.</summary>
    /// <param name="label">What is being compared, such as <c>&lt;verify&gt;&lt;stdout&gt;</c>.</param>
    /// <param name="expected">The expected bytes.</param>
    /// <param name="actual">The bytes produced.</param>
    /// <returns>A sentence naming the byte offset, the line and both lines; <see langword="null"/> when they are equal.</returns>
    public static string? Describe(string label, byte[] expected, byte[] actual)
    {
        if (expected.AsSpan().SequenceEqual(actual))
        {
            return null;
        }

        int offset = expected.AsSpan().CommonPrefixLength(actual);
        int line = expected.AsSpan(0, offset).Count((byte)'\n') + 1;
        return $"{label} differs at byte {offset} (line {line}): expected {Show(LineAround(expected, offset))}, got {Show(LineAround(actual, offset))}";
    }

    // The line holding the byte at offset, from its start to its line feed; empty past the end.
    private static string LineAround(byte[] bytes, int offset)
    {
        if (offset >= bytes.Length)
        {
            return string.Empty;
        }

        int start = bytes.AsSpan(0, offset).LastIndexOf((byte)'\n') + 1;
        int lineFeed = bytes.AsSpan(offset).IndexOf((byte)'\n');
        int end = lineFeed < 0 ? bytes.Length : offset + lineFeed + 1;
        return Encoding.Latin1.GetString(bytes, start, end - start);
    }

    private static string Show(string line)
    {
        if (line.Length == 0)
        {
            return "the end";
        }

        StringBuilder shown = new("\"");
        foreach (char character in line.Length > MaximumShownCharacters ? line[..MaximumShownCharacters] : line)
        {
            shown.Append(Escape(character));
        }

        return shown.Append(line.Length > MaximumShownCharacters ? "\"..." : "\"").ToString();
    }

    private static string Escape(char character) =>
        NamedEscapes.TryGetValue(character, out string? named) ? named
        : character is < ' ' or > '~' ? $"\\x{(int)character:X2}"
        : character.ToString();
}
