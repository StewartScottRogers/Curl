namespace Curl.Conformance;

/// <summary>
/// One test file expanded for one run by <see cref="UpstreamTestFileExpander"/>, with what it
/// could not resolve, so a case that needs any of it can be skipped with a reason.
/// </summary>
public sealed class UpstreamTestFileExpansion
{
    /// <summary>Creates an expansion.</summary>
    /// <param name="file">The expanded file.</param>
    /// <param name="unknownVariables">Upstream variables the run had no value for, each with its <c>%</c>.</param>
    /// <param name="unsupportedInstructions">Instructions left as written, such as <c>%days</c>.</param>
    /// <param name="conditionError">The stray <c>%else</c> or <c>%endif</c> that stopped expansion, or <see langword="null"/>.</param>
    public UpstreamTestFileExpansion(ReadOnlyMemory<byte> file, IReadOnlyList<string> unknownVariables, IReadOnlyList<string> unsupportedInstructions, string? conditionError)
    {
        ArgumentNullException.ThrowIfNull(unknownVariables);
        ArgumentNullException.ThrowIfNull(unsupportedInstructions);
        File = file;
        UnknownVariables = unknownVariables;
        UnsupportedInstructions = unsupportedInstructions;
        ConditionError = conditionError;
    }

    /// <summary>
    /// The expanded file: the lines <c>%if</c> blocks keep, with variables, character macros and
    /// <c>%b64</c>, <c>%hex</c> and <c>%repeat</c> instructions replaced. When
    /// <see cref="ConditionError"/> is set it ends before the line that stopped expansion.
    /// </summary>
    public ReadOnlyMemory<byte> File { get; }

    /// <summary>
    /// Each upstream variable, such as <c>%SSHPORT</c>, that the file uses in a kept line and the
    /// run supplied no value for, in the order first met. Each is left in <see cref="File"/> as written.
    /// </summary>
    public IReadOnlyList<string> UnknownVariables { get; }

    /// <summary>
    /// Each instruction the harness does not carry out that a kept line uses, left as written:
    /// <c>%days</c>, a <c>%repeat</c> that would produce more than 16 MiB characters (ADR-0423),
    /// and <c>%include</c>, <c>%includetext</c>, <c>%sha256b64file</c> or
    /// <c>%strippemfile</c> when the expansion was given no way to read files.
    /// </summary>
    public IReadOnlyList<string> UnsupportedInstructions { get; }

    /// <summary>
    /// A sentence naming the <c>%else</c> or <c>%endif</c> with no <c>%if</c> that stopped
    /// expansion, as it stops upstream's; <see langword="null"/> when there was none.
    /// </summary>
    public string? ConditionError { get; }

    /// <summary>Parses <see cref="File"/> with <see cref="UpstreamTestCaseParser"/>.</summary>
    /// <returns>The expanded test case, or the failure that stopped parsing it.</returns>
    public UpstreamTestCaseParseResult Parse() => UpstreamTestCaseParser.Parse(File.Span);
}
