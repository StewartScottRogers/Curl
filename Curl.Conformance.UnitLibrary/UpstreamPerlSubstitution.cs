using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// One line of a test case's <c>&lt;strippart&gt;</c> or <c>&lt;stripfile&gt;</c>: Perl code that
/// <c>runtests.pl</c> runs with <c>eval</c> on each line of an output. Only the form nearly every
/// case uses is carried out, a substitution <c>s/pattern/replacement/flags</c> with any
/// punctuation or symbol as the delimiter and flags from <c>gimsx</c>; the pattern is run as a
/// .NET regular expression.
/// </summary>
internal sealed class UpstreamPerlSubstitution
{
    private const string SupportedFlags = "gimsx";

    private readonly Regex pattern;
    private readonly string replacement;
    private readonly bool replacesEvery;

    private UpstreamPerlSubstitution(Regex pattern, string replacement, bool replacesEvery)
    {
        this.pattern = pattern;
        this.replacement = replacement;
        this.replacesEvery = replacesEvery;
    }

    /// <summary>Whether a line of a strip part is Perl that does nothing: blank, or a comment.</summary>
    /// <param name="line">The line, without its line feed.</param>
    /// <returns><see langword="true"/> when running it would change nothing.</returns>
    public static bool DoesNothing(string line) =>
        line.Trim() is { Length: 0 } or ['#', ..];

    /// <summary>Reads one line of Perl as a substitution.</summary>
    /// <param name="line">The line, without its line feed.</param>
    /// <returns>The substitution, or <see langword="null"/> when the line is some other Perl or its pattern does not compile.</returns>
    public static UpstreamPerlSubstitution? Parse(string line)
    {
        string code = line.Trim().TrimEnd(';');
        if (!StartsAsSubstitution(code))
        {
            return null;
        }

        char delimiter = code[1];
        List<string> pieces = SplitOnDelimiter(code[2..], delimiter);
        return pieces.Count == 3 && HasOnlySupportedFlags(pieces[2])
            ? Create(pieces[0], UnescapeDelimiter(pieces[1], delimiter), pieces[2])
            : null;
    }

    /// <summary>Runs the substitution on one line.</summary>
    /// <param name="line">The line, with its line feed.</param>
    /// <returns>The line after the substitution.</returns>
    public string Apply(string line) =>
        pattern.Replace(line, replacement, replacesEvery ? -1 : 1);

    private static bool StartsAsSubstitution(string code) =>
        code.Length >= 2 && code[0] == 's' && IsDelimiter(code[1]);

    private static bool HasOnlySupportedFlags(string flags) =>
        flags.All(flag => SupportedFlags.Contains(flag, StringComparison.Ordinal));

    private static string UnescapeDelimiter(string replacement, char delimiter) =>
        replacement.Replace(string.Concat("\\", delimiter.ToString()), delimiter.ToString(), StringComparison.Ordinal);

    private static bool IsDelimiter(char character) =>
        char.IsPunctuation(character) || char.IsSymbol(character);

    private static UpstreamPerlSubstitution? Create(string patternText, string replacement, string flags)
    {
        RegexOptions options = RegexOptions.CultureInvariant
            | (flags.Contains('i', StringComparison.Ordinal) ? RegexOptions.IgnoreCase : RegexOptions.None)
            | (flags.Contains('m', StringComparison.Ordinal) ? RegexOptions.Multiline : RegexOptions.None)
            | (flags.Contains('s', StringComparison.Ordinal) ? RegexOptions.Singleline : RegexOptions.None)
            | (flags.Contains('x', StringComparison.Ordinal) ? RegexOptions.IgnorePatternWhitespace : RegexOptions.None);
        return UpstreamRegex.TryCreate(patternText, options) is { } regex
            ? new UpstreamPerlSubstitution(regex, replacement, flags.Contains('g', StringComparison.Ordinal))
            : null;
    }

    // Splits at each delimiter that no backslash escapes; the escapes themselves are kept.
    private static List<string> SplitOnDelimiter(string text, char delimiter)
    {
        List<string> pieces = [];
        StringBuilder piece = new();
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] == delimiter)
            {
                pieces.Add(piece.ToString());
                piece.Clear();
                continue;
            }

            bool escapes = text[index] == '\\' && index + 1 < text.Length;
            piece.Append(text.AsSpan(index, escapes ? 2 : 1));
            index += escapes ? 1 : 0;
        }

        pieces.Add(piece.ToString());
        return pieces;
    }
}
