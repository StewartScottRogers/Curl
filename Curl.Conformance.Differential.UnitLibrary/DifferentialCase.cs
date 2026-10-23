namespace Curl.Conformance.Differential;

/// <summary>
/// One differential case: the command line both binaries run, what they read, and which parts of
/// their results are compared.
/// </summary>
/// <param name="Name">The case's name, unique in its case file.</param>
/// <param name="Arguments">The arguments after the program name, placeholders such as <c>{U}</c> still in them.</param>
/// <param name="StandardInput">The bytes written to standard input, or <see langword="null"/> for none.</param>
/// <param name="Files">The files written into the working directory before the run, by relative path.</param>
/// <param name="Timeout">How long a binary may run before it counts as timed out.</param>
/// <param name="ComparesStandardError">Whether standard error is compared.</param>
/// <param name="ComparesWire">Whether the bytes the loopback server received are compared.</param>
/// <param name="Limitations">The reference build limitations that keep the case off the reference build.</param>
public sealed record DifferentialCase(
    string Name,
    IReadOnlyList<string> Arguments,
    byte[]? StandardInput,
    IReadOnlyDictionary<string, byte[]> Files,
    TimeSpan Timeout,
    bool ComparesStandardError,
    bool ComparesWire,
    IReadOnlyList<ReferenceBuildLimitation> Limitations)
{
    /// <summary>Gets whether the case runs against the reference build: it names no limitation.</summary>
    public bool RunsAgainstReferenceBuild => Limitations.Count == 0;
}
