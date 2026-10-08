using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Expands one upstream curl test file for one run, the way <c>runtests.pl</c>'s <c>prepro</c>
/// does at <c>curl-8_21_0</c> before <c>getpart.pm</c> reads it: <c>%if</c> / <c>%else</c> /
/// <c>%endif</c> lines are resolved against the run's features, and every line that is kept has
/// its variables (<c>%HOSTIP</c>, <c>%TESTNUMBER</c>, …), its <c>%includetext</c> files (then its
/// variables again, when it had one), its character macros (<c>%SP</c>, <c>%TAB</c>, <c>%CR</c>,
/// <c>%LT</c>, <c>%GT</c>, <c>%AMP</c>), its <c>%b64[…]b64%</c>, <c>%hex[…]hex%</c> and
/// <c>%repeat[…]%</c> instructions, its <c>%include</c> files, and its <c>%sha256b64file[…]sha256b64file%</c>
/// and <c>%strippemfile[…]strippemfile%</c> instructions replaced, in that order.
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
    /// <summary>
    /// Expands the bytes of one test file, leaving <c>%include</c>, <c>%includetext</c>,
    /// <c>%sha256b64file</c> and <c>%strippemfile</c> as written and listing them as unsupported,
    /// since it has no way to read the files they name.
    /// </summary>
    /// <param name="file">The whole test file, as written.</param>
    /// <param name="variables">
    /// The run's variable values, keyed by name without the <c>%</c>, such as <c>HOSTIP</c>
    /// or <c>LOGDIR</c>. Values are inserted as UTF-8.
    /// </param>
    /// <param name="features">The features the run reports, such as <c>brotli</c> or <c>IPv6</c>; case-sensitive.</param>
    /// <returns>The expanded file with the variables and instructions it could not resolve.</returns>
    public static UpstreamTestFileExpansion Expand(ReadOnlySpan<byte> file, IReadOnlyDictionary<string, string> variables, IReadOnlySet<string> features) =>
        Expand(file, variables, features, null);

    /// <summary>
    /// Expands the bytes of one test file, reading the files <c>%include</c>, <c>%includetext</c>,
    /// <c>%sha256b64file</c> and <c>%strippemfile</c> name.
    /// </summary>
    /// <param name="file">The whole test file, as written.</param>
    /// <param name="variables">
    /// The run's variable values, keyed by name without the <c>%</c>, such as <c>HOSTIP</c>
    /// or <c>LOGDIR</c>. Values are inserted as UTF-8.
    /// </param>
    /// <param name="features">The features the run reports, such as <c>brotli</c> or <c>IPv6</c>; case-sensitive.</param>
    /// <param name="readFile">
    /// Reads the file an instruction names, by its path after variable substitution, or returns
    /// <see langword="null"/> when it cannot, so it counts as empty; <see langword="null"/>
    /// leaves those instructions as written and lists them as unsupported.
    /// </param>
    /// <returns>The expanded file with the variables and instructions it could not resolve.</returns>
    public static UpstreamTestFileExpansion Expand(ReadOnlySpan<byte> file, IReadOnlyDictionary<string, string> variables, IReadOnlySet<string> features, Func<string, byte[]?>? readFile)
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
                output.Append(ExpandLine(line, substitution, readFile, unsupportedInstructions));
            }
        }

        return new UpstreamTestFileExpansion(
            Encoding.Latin1.GetBytes(output.ToString()),
            substitution.UnknownVariables,
            unsupportedInstructions,
            conditions.Error);
    }

    // prepro's order: subvariables; subtextfile, then subvariables again when it included
    // anything; subchars; subbase64, which ends with %include.
    private static string ExpandLine(string line, UpstreamTestVariableSubstitution substitution, Func<string, byte[]?>? readFile, List<string> unsupportedInstructions)
    {
        string expanded = substitution.Substitute(line);
        if (readFile is not null)
        {
            expanded = UpstreamTestFileInclusions.ReplaceTextIncludes(expanded, readFile, out bool included);
            expanded = included ? substitution.Substitute(expanded) : expanded;
        }

        expanded = UpstreamTestInstructions.Apply(UpstreamTestInstructions.ReplaceCharacterMacros(expanded), unsupportedInstructions);
        if (readFile is null)
        {
            UpstreamTestFileInclusions.AddUnread(expanded, unsupportedInstructions);
            UpstreamTestFileContentInstructions.AddUnread(expanded, unsupportedInstructions);
        }
        else
        {
            expanded = UpstreamTestFileContentInstructions.Replace(UpstreamTestFileInclusions.ReplaceRawIncludes(expanded, readFile), readFile);
        }

        UpstreamTestInstructions.AddUnsupported(expanded, unsupportedInstructions);
        return expanded;
    }
}
