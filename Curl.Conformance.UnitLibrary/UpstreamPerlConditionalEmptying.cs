using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// The strip line test1506, test1510 and test1512 run on each stderr line to keep only curl's
/// connection lines (BL-2641): <c>$_ = '' if(($_ !~ /A/) and ($_ !~ /B/))</c>. Each term is
/// <c>($_ !~ /PATTERN/)</c> or <c>($_ =~ /PATTERN/)</c>, the terms joined by <c>and</c>; a line
/// every term holds for is emptied, as Perl's assignment empties <c>$_</c>, and any other line is
/// left as it is. The patterns are run as .NET regular expressions, with no <c>eval</c>.
/// </summary>
internal sealed class UpstreamPerlConditionalEmptying : IUpstreamPerlLineEdit
{
    private const string Term = @"\(\$_ (?:!~|=~) /[^/]*/\)";

    private static readonly Regex ProgramForm = new(
        @"^\$_ = ''\s*if\s*\((?<terms>" + Term + "(?: and " + Term + @")*)\);?$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        UpstreamRegex.MatchTimeout);

    private static readonly Regex TermForm = new(
        @"\(\$_ (?<operator>!~|=~) /(?<pattern>[^/]*)/\)",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        UpstreamRegex.MatchTimeout);

    private readonly (Regex Pattern, bool MustMatch)[] terms;

    private UpstreamPerlConditionalEmptying((Regex Pattern, bool MustMatch)[] terms) => this.terms = terms;

    /// <summary>Reads one line of Perl as the conditional emptying.</summary>
    /// <param name="line">The line, without its line feed.</param>
    /// <returns>The edit, or <see langword="null"/> when the line has another form or one of its patterns does not compile.</returns>
    public static UpstreamPerlConditionalEmptying? Parse(string line)
    {
        Match form = ProgramForm.Match(line.Trim());
        if (!form.Success)
        {
            return null;
        }

        var parsed = TermForm.Matches(form.Groups["terms"].Value)
            .Select(term => (Pattern: UpstreamRegex.TryCreate(term.Groups["pattern"].Value, RegexOptions.CultureInvariant), MustMatch: term.Groups["operator"].Value == "=~"))
            .ToArray();
        return parsed.Any(term => term.Pattern is null)
            ? null
            : new UpstreamPerlConditionalEmptying(parsed.Select(term => (term.Pattern!, term.MustMatch)).ToArray());
    }

    /// <inheritdoc/>
    public string Apply(string line) =>
        terms.All(term => term.Pattern.IsMatch(line) == term.MustMatch) ? string.Empty : line;
}
