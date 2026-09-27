using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Expands one upstream curl test file for one run, the way <c>runtests.pl</c>'s <c>prepro</c>
/// does at <c>curl-8_21_0</c> before <c>getpart.pm</c> reads it: <c>%if</c> / <c>%else</c> /
/// <c>%endif</c> lines are resolved against the run's features, and every line that is kept has
/// its variables (<c>%HOSTIP</c>, <c>%TESTNUMBER</c>, …), its character macros (<c>%SP</c>,
/// <c>%TAB</c>, <c>%CR</c>, <c>%LT</c>, <c>%GT</c>, <c>%AMP</c>) and its <c>%b64[…]b64%</c>,
/// <c>%hex[…]hex%</c> and <c>%repeat[…]%</c> instructions replaced, in that order.
/// </summary>
/// <remarks>
/// It works on the file's bytes rather than on a parsed <see cref="UpstreamTestCase"/> because a
/// <c>%if</c> block can wrap whole parts, which <see cref="UpstreamTestCaseParser"/> would read
/// from both branches; parse the expanded file with <see cref="UpstreamTestFileExpansion.Parse"/>.
/// Bytes are carried one character per byte (Latin-1), as Perl carries them. One step of
/// <c>prepro</c> is left out on purpose: forcing CRLF line endings inside <c>&lt;data&gt;</c> and
/// <c>&lt;connect&gt;</c> parts marked <c>crlf="yes"</c> or <c>crlf="headers"</c>, so parsed
/// bodies keep one meaning, "as written"; the stage that serves those parts applies it with
/// <see cref="UpstreamTestSectionLineEndings"/>. Upstream converts one line feed per source line
/// there, which differs from converting the whole part only when a line gains a line feed from
/// <c>%hex</c> or <c>%repeat</c>.
/// </remarks>
public static class UpstreamTestFileExpander
{
    /// <summary>Expands the bytes of one test file.</summary>
    /// <param name="file">The whole test file, as written.</param>
    /// <param name="variables">
    /// The run's variable values, keyed by name without the <c>%</c>, such as <c>HOSTIP</c>
    /// or <c>LOGDIR</c>. Values are inserted as UTF-8.
    /// </param>
    /// <param name="features">The features the run reports, such as <c>brotli</c> or <c>IPv6</c>; case-sensitive.</param>
    /// <returns>The expanded file with the variables and instructions it could not resolve.</returns>
    public static UpstreamTestFileExpansion Expand(ReadOnlySpan<byte> file, IReadOnlyDictionary<string, string> variables, IReadOnlySet<string> features)
    {
        ArgumentNullException.ThrowIfNull(variables);
        ArgumentNullException.ThrowIfNull(features);
        UpstreamTestVariableSubstitution substitution = new(variables);
        UpstreamTestConditionalLines conditions = new(features);
        List<string> unsupportedInstructions = [];
        StringBuilder output = new();
        int lineNumber = 0;
        foreach (string line in UpstreamTestLines.SplitAfterLineFeeds(Encoding.Latin1.GetString(file)))
        {
            lineNumber++;
            UpstreamTestLineDisposition disposition = conditions.ReadLine(line, lineNumber);
            if (disposition == UpstreamTestLineDisposition.Stopped)
            {
                break;
            }

            if (disposition == UpstreamTestLineDisposition.Kept)
            {
                string expanded = UpstreamTestInstructions.Apply(UpstreamTestInstructions.ReplaceCharacterMacros(substitution.Substitute(line)));
                UpstreamTestInstructions.AddUnsupported(expanded, unsupportedInstructions);
                output.Append(expanded);
            }
        }

        return new UpstreamTestFileExpansion(
            Encoding.Latin1.GetBytes(output.ToString()),
            substitution.UnknownVariables,
            unsupportedInstructions,
            conditions.Error);
    }
}
