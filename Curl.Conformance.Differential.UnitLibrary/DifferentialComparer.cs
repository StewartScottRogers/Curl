namespace Curl.Conformance.Differential;

/// <summary>
/// Compares Curl's normalised run of a case with the reference curl's, part by part, and writes each
/// difference as the tester's report block: the exit code, then standard output (first 600 bytes),
/// standard error (first 700, when the case compares it), the files (first 200 of each) and the
/// wire (first 900, when the case compares it).
/// </summary>
public static class DifferentialComparer
{
    /// <summary>Compares two runs of one case.</summary>
    /// <param name="differentialCase">The case both ran.</param>
    /// <param name="ours">Curl's run, normalised.</param>
    /// <param name="real">The reference curl's run, normalised.</param>
    /// <returns>The outcome, whose differences are empty when the runs agree.</returns>
    public static DifferentialCaseOutcome Compare(DifferentialCase differentialCase, DifferentialRunResult ours, DifferentialRunResult real)
    {
        ArgumentNullException.ThrowIfNull(differentialCase);
        ArgumentNullException.ThrowIfNull(ours);
        ArgumentNullException.ThrowIfNull(real);

        var differences = new List<string>();
        if (ours.ExitCode != real.ExitCode)
        {
            differences.Add($"exit: ours={FormatExitCode(ours.ExitCode)} real={FormatExitCode(real.ExitCode)}");
        }

        AddOutputDifference(differences, "stdout", ours.StandardOutput, real.StandardOutput, 600, compares: true);
        AddOutputDifference(differences, "stderr", ours.StandardError, real.StandardError, 700, differentialCase.ComparesStandardError);
        if (!FilesEqual(ours.Files, real.Files))
        {
            differences.Add($"files:\n  ours={FormatFiles(ours.Files)}\n  real={FormatFiles(real.Files)}");
        }

        AddOutputDifference(differences, "wire", ours.Wire, real.Wire, 900, differentialCase.ComparesWire);
        return new DifferentialCaseOutcome(differentialCase, differences, ours.Elapsed, real.Elapsed);
    }

    private static string FormatExitCode(int? exitCode) => exitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "TIMEOUT";

    private static void AddOutputDifference(List<string> differences, string label, byte[] ours, byte[] real, int shownBytes, bool compares)
    {
        if (compares && !ours.AsSpan().SequenceEqual(real))
        {
            differences.Add($"{label}:\n  ours={FormatPrefix(ours, shownBytes)}\n  real={FormatPrefix(real, shownBytes)}");
        }
    }

    private static string FormatPrefix(byte[] bytes, int shownBytes) =>
        PythonLiteral.FormatBytes(bytes.AsSpan(0, Math.Min(bytes.Length, shownBytes)));

    private static bool FilesEqual(IReadOnlyDictionary<string, byte[]> ours, IReadOnlyDictionary<string, byte[]> real) =>
        ours.Count == real.Count
        && ours.All(file => real.TryGetValue(file.Key, out var content) && file.Value.AsSpan().SequenceEqual(content));

    private static string FormatFiles(IReadOnlyDictionary<string, byte[]> files) =>
        "{" + string.Join(", ", files.OrderBy(file => file.Key, StringComparer.Ordinal)
            .Select(file => PythonLiteral.FormatString(file.Key) + ": " + FormatPrefix(file.Value, 200))) + "}";
}
