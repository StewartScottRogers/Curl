namespace Curl.Cli;

/// <summary>
/// The lines <c>--help &lt;option&gt;</c> prints on standard output (<c>--help -v</c>, <c>--help --verbose</c>,
/// <c>--help --no-verbose</c>): that option's section of <see cref="CurlManual"/>, ported from curl 8.21.0's
/// <c>tool_help</c> and <c>helpscan</c>. The subject is looked up in <see cref="CurlOptionAliasTable"/> by exact
/// name (after dropping a <c>--no-</c> prefix, which only a boolean may carry) or by its single letter; the
/// manual after <c>ALL OPTIONS</c> is searched for the option's heading and printed up to the next heading,
/// or up to <c>FILES</c> for <c>--xattr</c>, the last option. A name curl knows whose heading the manual does
/// not carry (<c>--help --no-compressed</c>, <c>--help --include</c>) prints nothing, as curl does. The lines
/// carry no line terminator; the console layer chooses the newline.
/// </summary>
public static class CurlOptionManualSection
{
    /// <summary>
    /// What curl writes to standard error, before exiting 0, for a subject that names no option; without its
    /// line terminator.
    /// </summary>
    public const string IncorrectOptionNameMessage = "Incorrect option name to show help for, see curl -h";

    private const string AllOptionsHeading = "\nALL OPTIONS\n";
    private const string NextOptionHeading = "\n    -";
    private const string FilesHeading = "\nFILES";
    private const string LastOptionName = "xattr";

    /// <summary>Finds the manual section <c>--help <paramref name="subject"/></c> prints.</summary>
    /// <param name="subject">The subject, starting with <c>-</c> (see <see cref="CurlHelpText.IsOptionSubject"/>).</param>
    /// <param name="lines">
    /// The section's lines, empty when the manual has no heading for the option; empty when the method
    /// returns <see langword="false"/>.
    /// </param>
    /// <returns>
    /// <see langword="false"/> when <paramref name="subject"/> names no option curl knows, and curl writes
    /// <see cref="IncorrectOptionNameMessage"/> to standard error instead.
    /// </returns>
    public static bool TryGetLines(string subject, out IReadOnlyList<string> lines)
    {
        CurlOptionAlias? alias = FindAlias(subject);
        lines = alias is null ? [] : SectionLines(Heading(alias, subject), alias.Name == LastOptionName ? FilesHeading : NextOptionHeading);
        return alias is not null;
    }

    /// <summary>curl's lookup: <c>--name</c> or <c>--no-name</c> by long name, <c>-x</c> by letter, else none.</summary>
    private static CurlOptionAlias? FindAlias(string subject)
    {
        if (subject.StartsWith("--", StringComparison.Ordinal))
        {
            return FindLongAlias(subject[2..]);
        }

        return subject.Length == 2 ? FindLetterAlias(subject[1]) : null;
    }

    private static CurlOptionAlias? FindLongAlias(string lookup)
    {
        bool noPrefixed = lookup.StartsWith("no-", StringComparison.Ordinal);
        string name = noPrefixed ? lookup[3..] : lookup;
        CurlOptionAlias? alias = CurlOptionAliasTable.Aliases.FirstOrDefault(candidate => candidate.Name == name);
        return noPrefixed && alias?.NoPrefix == CurlOptionNoPrefix.NotAccepted ? null : alias;
    }

    private static CurlOptionAlias? FindLetterAlias(char letter) =>
        letter == CurlOptionAliasTable.NoLetter ? null : CurlOptionAliasTable.Aliases.FirstOrDefault(candidate => candidate.Letter == letter);

    /// <summary>The heading curl searches for: by letter, by its <c>--no-</c> name, or the subject as given.</summary>
    private static string Heading(CurlOptionAlias alias, string subject)
    {
        if (alias.Letter != CurlOptionAliasTable.NoLetter)
        {
            return $"\n    -{alias.Letter}, --";
        }

        return alias.NoPrefix == CurlOptionNoPrefix.Documented ? $"\n    --no-{alias.Name}" : $"\n    {subject}";
    }

    /// <summary>
    /// curl's <c>helpscan</c>: from the heading's first line up to the line before <paramref name="endHeading"/>.
    /// Every option's section is followed by another heading, or by <c>FILES</c> for the last, so the end is
    /// always found once the heading is.
    /// </summary>
    private static IReadOnlyList<string> SectionLines(string heading, string endHeading)
    {
        string manual = string.Concat(CurlManual.Lines().Select(line => line + "\n"));
        int allOptionsEnd = manual.IndexOf(AllOptionsHeading, StringComparison.Ordinal) + AllOptionsHeading.Length;
        int headingStart = manual.IndexOf(heading, allOptionsEnd, StringComparison.Ordinal);
        if (headingStart < 0)
        {
            return [];
        }

        int sectionEnd = manual.IndexOf(endHeading, headingStart + heading.Length, StringComparison.Ordinal);
        return manual[(headingStart + 1)..sectionEnd].Split('\n');
    }
}
