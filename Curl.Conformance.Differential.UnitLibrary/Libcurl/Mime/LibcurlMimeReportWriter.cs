using System.Globalization;
using System.Text;

namespace Curl.Conformance.Differential.Libcurl.Mime;

/// <summary>Writes MIME outcomes in the shape the tester's <c>mimetest.py</c> printed.</summary>
public static class LibcurlMimeReportWriter
{
    /// <summary>Writes a <c>## name</c> block, its differences one per line, for every case that differs.</summary>
    /// <param name="outcomes">The outcomes.</param>
    /// <returns>The report.</returns>
    public static string Write(IEnumerable<LibcurlMimeCaseOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        var report = new StringBuilder();
        foreach (var outcome in outcomes.Where(o => o.Differences.Count > 0))
        {
            report.Append(CultureInfo.InvariantCulture, $"## {outcome.Name}\n{string.Join('\n', outcome.Differences)}\n");
        }

        return report.ToString();
    }

    /// <summary>Writes the count line <c>mimetest.py</c> printed.</summary>
    /// <param name="outcomes">The outcomes.</param>
    /// <returns>The line, such as <c>cases 3 identical 2</c>.</returns>
    public static string Summary(IReadOnlyCollection<LibcurlMimeCaseOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        return string.Create(CultureInfo.InvariantCulture, $"cases {outcomes.Count} identical {outcomes.Count(o => o.Differences.Count == 0)}");
    }
}
