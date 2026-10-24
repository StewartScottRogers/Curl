using System.Text;

namespace Curl.Conformance.Differential.Libcurl.Scenarios;

/// <summary>
/// Writes <see cref="LibcurlScenarioOutcome"/>s as the tester's <c>lib_diff.txt</c>: a count of
/// differing scenarios in all and per kind, then an <c>OURS</c> and a <c>REAL</c> line for every line
/// that differs, keeping only the differing <c> |</c> fields of a <c>U</c> line and cut at 700
/// characters (ADR-0791).
/// </summary>
public static class LibcurlScenarioReportWriter
{
    /// <summary>The characters of a line the report keeps, as <c>libtest.py</c> cut them.</summary>
    private const int LineLimit = 700;

    /// <summary>Stands for the line one library gave where the other gave none.</summary>
    private const string MissingLine = "(no line)";

    /// <summary>Separates the fields of a <c>U</c> line, one per part and flag set.</summary>
    private const string FieldSeparator = " |";

    /// <summary>Writes the report; <paramref name="excludedCount"/> is the scenarios left out for a reference-build limitation.</summary>
    public static string Write(IReadOnlyList<LibcurlScenarioOutcome> outcomes, int excludedCount)
    {
        var report = new StringBuilder();
        report.Append($"scenarios {outcomes.Count} differing {CountDiffering(outcomes)} excluded {excludedCount}\n");
        AppendKindCounts(report, outcomes);
        foreach (var outcome in outcomes)
        {
            AppendDifferingLines(report, outcome);
        }

        return report.ToString();
    }

    private static void AppendKindCounts(StringBuilder report, IReadOnlyList<LibcurlScenarioOutcome> outcomes)
    {
        foreach (var kind in outcomes.GroupBy(KindOf, StringComparer.Ordinal))
        {
            report.Append($"{kind.Key} {CountDiffering(kind)} of {kind.Count()} differ\n");
        }
    }

    private static string KindOf(LibcurlScenarioOutcome outcome) => outcome.Scenario.Kind;

    private static bool IsDiffering(LibcurlScenarioOutcome outcome) => outcome.Differs;

    private static int CountDiffering(IEnumerable<LibcurlScenarioOutcome> outcomes) => outcomes.Count(IsDiffering);

    private static void AppendDifferingLines(StringBuilder report, LibcurlScenarioOutcome outcome)
    {
        var count = Math.Max(outcome.Ours.Count, outcome.Reference.Count);
        for (var line = 0; line < count; line++)
        {
            var ours = LineAt(outcome.Ours, line);
            var reference = LineAt(outcome.Reference, line);
            if (!string.Equals(ours, reference, StringComparison.Ordinal))
            {
                var (oursFields, referenceFields) = DifferingFields(ours, reference);
                report.Append($"\nOURS {Cut(oursFields)}\nREAL {Cut(referenceFields)}\n");
            }
        }
    }

    /// <summary>
    /// Keeps, of two lines with the same number of <c> |</c> fields (a <c>U</c> line's parts), the
    /// first field and the fields that differ; other lines are kept whole.
    /// </summary>
    private static (string Ours, string Reference) DifferingFields(string ours, string reference)
    {
        var oursFields = ours.Split(FieldSeparator);
        var referenceFields = reference.Split(FieldSeparator);
        if (oursFields.Length != referenceFields.Length)
        {
            return (ours, reference);
        }

        var differing = Enumerable.Range(1, oursFields.Length - 1)
            .Where(field => !string.Equals(oursFields[field], referenceFields[field], StringComparison.Ordinal))
            .ToList();
        return (Keep(oursFields, differing), Keep(referenceFields, differing));
    }

    private static string Keep(string[] fields, List<int> differing) =>
        string.Join(FieldSeparator, differing.Select(field => fields[field]).Prepend(fields[0]));

    private static string LineAt(IReadOnlyList<string> lines, int line) => line < lines.Count ? lines[line] : MissingLine;

    private static string Cut(string line) => line.Length > LineLimit ? line[..LineLimit] : line;
}
