using System.Globalization;
using System.Text;

namespace Curl.Conformance.Differential.Libcurl.Easy;

/// <summary>Writes easy-handle outcomes in the tester's <c>easy_report.txt</c> shape.</summary>
public static class LibcurlEasyReportWriter
{
    /// <summary>Writes a <c>### name</c> block, its differences one per line, for every case that differs.</summary>
    /// <param name="outcomes">The outcomes.</param>
    /// <returns>The report.</returns>
    public static string Write(IEnumerable<LibcurlEasyCaseOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        var report = new StringBuilder();
        foreach (var outcome in outcomes.Where(o => o.Differences.Count > 0))
        {
            report.Append(CultureInfo.InvariantCulture, $"### {outcome.Name}\n{string.Join('\n', outcome.Differences)}\n\n");
        }

        return report.ToString();
    }

    /// <summary>Writes the count line <c>easytest.py</c> printed.</summary>
    /// <param name="outcomes">The outcomes.</param>
    /// <returns>The line, such as <c>cases 3 identical 2 different 1</c>.</returns>
    public static string Summary(IReadOnlyCollection<LibcurlEasyCaseOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        var different = outcomes.Count(o => o.Differences.Count > 0);
        return string.Create(CultureInfo.InvariantCulture, $"cases {outcomes.Count} identical {outcomes.Count - different} different {different}");
    }
}
