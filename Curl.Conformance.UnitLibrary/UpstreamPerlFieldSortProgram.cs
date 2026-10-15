using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// The strip program test1286 runs on each protocol line to put an <c>Authorization: Digest</c>
/// line's fields in one order (BL-2238):
/// <c>if(s/TEST//) { $_ = $1 . join('SEP', map { EDIT; EDIT; $_ } sort split(/SPLIT/, $2)) . $3; }</c>.
/// When the substitution TEST matches the line, the line becomes capture group 1, then the fields
/// of group 2 - split at SPLIT with Perl's trailing empty fields dropped, sorted in Perl's
/// ordinal (byte) order, each run through the EDIT substitutions in turn - joined by SEP, then
/// group 3; the group numbers are read from the program. A line TEST does not match is left as it
/// is. Every piece is carried out as written, with no <c>eval</c>.
/// </summary>
internal sealed class UpstreamPerlFieldSortProgram : IUpstreamPerlLineEdit
{
    private static readonly Regex ProgramForm = new(
        @"^if\((?<test>s.+?)\) \{ \$_ = \$(?<prefix>\d) \. join\('(?<separator>[^']*)', map \{ (?<edits>.+?); \$_ \} sort split\(/(?<split>[^/]+)/, \$(?<fields>\d)\)\) \. \$(?<suffix>\d); \}$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        UpstreamRegex.MatchTimeout);

    private readonly UpstreamPerlSubstitution test;
    private readonly int[] groups;
    private readonly string separator;
    private readonly UpstreamPerlSubstitution[] edits;
    private readonly Regex split;

    private UpstreamPerlFieldSortProgram(UpstreamPerlSubstitution test, int[] groups, string separator, UpstreamPerlSubstitution[] edits, Regex split)
    {
        this.test = test;
        this.groups = groups;
        this.separator = separator;
        this.edits = edits;
        this.split = split;
    }

    /// <summary>Reads one line of Perl as the field sort program.</summary>
    /// <param name="line">The line, without its line feed.</param>
    /// <returns>The program, or <see langword="null"/> when the line has another form or one of its pieces is not a substitution or pattern the harness runs.</returns>
    public static UpstreamPerlFieldSortProgram? Parse(string line)
    {
        Match form = ProgramForm.Match(line.Trim());
        if (!form.Success)
        {
            return null;
        }

        UpstreamPerlSubstitution?[] edits = form.Groups["edits"].Value.Split("; ").Select(UpstreamPerlSubstitution.Parse).ToArray();
        return UpstreamPerlSubstitution.Parse(form.Groups["test"].Value) is { } test
            && !edits.Contains(null)
            && UpstreamRegex.TryCreate(form.Groups["split"].Value, RegexOptions.CultureInvariant) is { } split
            ? new UpstreamPerlFieldSortProgram(test, [Group(form, "prefix"), Group(form, "fields"), Group(form, "suffix")], form.Groups["separator"].Value, edits!, split)
            : null;
    }

    /// <inheritdoc/>
    public string Apply(string line)
    {
        Match match = test.Match(line);
        return match.Success
            ? match.Groups[groups[0]].Value + string.Join(separator, SortedFields(match.Groups[groups[1]].Value).Select(Edited)) + match.Groups[groups[2]].Value
            : line;
    }

    private static int Group(Match form, string name) => form.Groups[name].Value[0] - '0';

    // Perl's split drops the empty fields at the end; sort compares strings byte by byte.
    private IEnumerable<string> SortedFields(string text)
    {
        string[] fields = split.Split(text);
        int count = fields.Length;
        while (count > 0 && fields[count - 1].Length == 0)
        {
            count--;
        }

        return fields.Take(count).Order(StringComparer.Ordinal);
    }

    private string Edited(string field) =>
        edits.Aggregate(field, (text, edit) => edit.Apply(text));
}
