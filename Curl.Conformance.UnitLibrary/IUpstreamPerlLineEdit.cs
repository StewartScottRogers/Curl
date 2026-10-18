namespace Curl.Conformance;

/// <summary>
/// One line of a test case's <c>&lt;strippart&gt;</c> or <c>&lt;stripfile&gt;</c> that the harness
/// carries out: Perl code that <c>runtests.pl</c> runs with <c>eval</c> on each line of an output,
/// changing <c>$_</c>.
/// </summary>
internal interface IUpstreamPerlLineEdit
{
    /// <summary>Reads one line of Perl as an <see cref="UpstreamPerlSubstitution"/>, an <see cref="UpstreamPerlFieldSortProgram"/> or an <see cref="UpstreamPerlConditionalEmptying"/>, the first that reads it.</summary>
    /// <param name="line">The line, without its line feed.</param>
    /// <returns>The edit, or <see langword="null"/> when the line is Perl the harness does not carry out.</returns>
    static IUpstreamPerlLineEdit? Parse(string line) =>
        (IUpstreamPerlLineEdit?)UpstreamPerlSubstitution.Parse(line) ?? (IUpstreamPerlLineEdit?)UpstreamPerlFieldSortProgram.Parse(line) ?? UpstreamPerlConditionalEmptying.Parse(line);

    /// <summary>Runs the code on one line.</summary>
    /// <param name="line">The line, with its line feed.</param>
    /// <returns>The line as the code leaves <c>$_</c>.</returns>
    string Apply(string line);
}
