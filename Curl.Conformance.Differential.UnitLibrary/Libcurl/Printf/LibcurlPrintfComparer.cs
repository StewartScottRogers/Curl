using System.Globalization;
using System.Text;

namespace Curl.Conformance.Differential.Libcurl.Printf;

/// <summary>
/// Compares the two libraries' result lines for one printf input file as the tester's
/// <c>printftest.py</c> did (a scenario differs when its lines differ) and writes the
/// <c>pf_report.txt</c> report.
/// </summary>
public static class LibcurlPrintfComparer
{
    private static readonly Dictionary<char, Func<LibcurlPrintfArgument, string>> ValueDescribers = new()
    {
        ['s'] = argument => argument.Text is null ? "None" : PythonLiteral.FormatString(argument.Text),
        ['d'] = argument => PythonLiteral.FormatString(argument.Number.GetValueOrDefault().ToString("x16", CultureInfo.InvariantCulture)),
        ['u'] = argument => unchecked((ulong)argument.Number.GetValueOrDefault()).ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>Compares one input file's lines.</summary>
    /// <param name="name">The input file's name.</param>
    /// <param name="scenarios">Its scenarios.</param>
    /// <param name="reference">The reference libcurl's line for each scenario; <see cref="LibcurlPrintfLineWriter.NotRun"/> for one not run against it.</param>
    /// <param name="ours">Curl's line for each scenario.</param>
    /// <returns>The outcome.</returns>
    public static LibcurlPrintfOutcome Compare(string name, IReadOnlyList<LibcurlPrintfScenario> scenarios, IReadOnlyList<string> reference, IReadOnlyList<string> ours)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(ours);
        var notRun = reference.Count(line => line == LibcurlPrintfLineWriter.NotRun);
        var differences = scenarios
            .Select((scenario, index) => new LibcurlPrintfDifference(scenario, ours[index], reference[index]))
            .Where(pair => pair.Reference != LibcurlPrintfLineWriter.NotRun && pair.Reference != pair.Ours)
            .ToList();
        return new LibcurlPrintfOutcome(name, scenarios.Count - notRun - differences.Count, notRun, differences);
    }

    /// <summary>Writes a <c>### name</c> block per outcome with differences, each difference as the tester's report wrote it.</summary>
    /// <param name="outcomes">The outcomes.</param>
    /// <returns>The report.</returns>
    public static string Write(IEnumerable<LibcurlPrintfOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        var report = new StringBuilder();
        foreach (var outcome in outcomes.Where(o => o.Differences.Count > 0))
        {
            report.Append(CultureInfo.InvariantCulture, $"### {outcome.Name}\n");
            foreach (var difference in outcome.Differences)
            {
                var arguments = string.Join(", ", difference.Scenario.Arguments.Select(Describe));
                report.Append(CultureInfo.InvariantCulture, $"{PythonLiteral.FormatString(difference.Scenario.Format)} [{arguments}]\n  OURS {PythonLiteral.FormatString(Decode(difference.Ours))}\n  REAL {PythonLiteral.FormatString(Decode(difference.Reference))}\n");
            }

            report.Append('\n');
        }

        return report.ToString();
    }

    /// <summary>Writes the count line for each outcome.</summary>
    /// <param name="outcomes">The outcomes.</param>
    /// <returns>One line per outcome, such as <c>pf_in: cases 3 identical 1 different 1 not run 1</c>, joined with <c>; </c>.</returns>
    public static string Summary(IEnumerable<LibcurlPrintfOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        return string.Join("; ", outcomes.Select(o => string.Create(
            CultureInfo.InvariantCulture,
            $"{o.Name}: cases {o.Identical + o.Differences.Count + o.NotRunAgainstReference} identical {o.Identical} different {o.Differences.Count} not run {o.NotRunAgainstReference}")));
    }

    /// <summary>Decodes a <c>count HEX</c> line's bytes as UTF-8, as the tester's report did; any other line is kept.</summary>
    /// <param name="line">A result line.</param>
    /// <returns>The count and the text it formatted.</returns>
    public static string Decode(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var parts = line.Split(' ', 2);
        return parts.Length == 2 && int.TryParse(parts[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _)
            ? $"{parts[0]} {Encoding.UTF8.GetString(Convert.FromHexString(parts[1]))}"
            : line;
    }

    private static string Describe(LibcurlPrintfArgument argument)
    {
        var value = ValueDescribers.TryGetValue(argument.Kind, out var describe)
            ? describe(argument)
            : argument.Number?.ToString(CultureInfo.InvariantCulture) ?? "None";
        return $"('{argument.Kind}', {value})";
    }
}
