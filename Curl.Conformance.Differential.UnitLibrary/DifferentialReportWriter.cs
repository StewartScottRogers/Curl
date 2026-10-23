using System.Globalization;
using System.Text;

namespace Curl.Conformance.Differential;

/// <summary>
/// Writes outcomes as the tester's <c>report.txt</c>: a <c>### DIFF name   args=...</c> entry
/// with its difference blocks for each case whose runs differ, a
/// <c>### SLOW name: ours Xs real Ys</c> entry for each case where Curl was slow, entries
/// separated by a blank line.
/// </summary>
public static class DifferentialReportWriter
{
    /// <summary>Writes the report for <paramref name="outcomes"/>.</summary>
    /// <param name="outcomes">The outcomes, in the order the cases ran.</param>
    /// <returns>The report text, with <c>\n</c> line ends.</returns>
    public static string Write(IEnumerable<DifferentialCaseOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        var entries = new List<string>();
        foreach (var outcome in outcomes)
        {
            AddEntries(entries, outcome);
        }

        return string.Join("\n", entries);
    }

    private static void AddEntries(List<string> entries, DifferentialCaseOutcome outcome)
    {
        if (outcome.Differences.Count > 0)
        {
            var entry = new StringBuilder("### DIFF ").Append(outcome.Case.Name)
                .Append("   args=").AppendJoin(' ', outcome.Case.Arguments).Append('\n')
                .AppendJoin('\n', outcome.Differences).Append('\n');
            entries.Add(entry.ToString());
        }

        if (outcome.IsSlow)
        {
            entries.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"### SLOW {outcome.Case.Name}: ours {outcome.OursElapsed.TotalSeconds:F1}s real {outcome.RealElapsed.TotalSeconds:F1}s\n"));
        }
    }
}
