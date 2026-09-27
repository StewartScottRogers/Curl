using System.Globalization;

namespace Curl.Conformance;

/// <summary>
/// The ratchet of ADR-0013, decision 5: turns a case's outcome and whether it is on the committed
/// passing list into the verdict its data-driven test row reports, so a listed case can never
/// silently regress and every other case says what it would take to list it.
/// </summary>
public static class UpstreamCaseRatchet
{
    /// <summary>The name of the committed list of passing case numbers.</summary>
    public const string PassingListFileName = "PassingUpstreamCases.txt";

    /// <summary>Reads the passing list: one case number per line; blank lines and lines starting with <c>#</c> are ignored.</summary>
    /// <param name="text">The list's text.</param>
    /// <returns>The listed case numbers.</returns>
    /// <exception cref="FormatException">A line is neither a number, blank nor a comment.</exception>
    public static IReadOnlySet<int> ReadPassingList(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Split('\n', StringSplitOptions.TrimEntries)
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => int.Parse(line, NumberStyles.None, CultureInfo.InvariantCulture))
            .ToHashSet();
    }

    /// <summary>Judges one case.</summary>
    /// <param name="testNumber">The case's number.</param>
    /// <param name="outcome">How its run ended.</param>
    /// <param name="isListed">Whether it is on the passing list.</param>
    /// <returns>The verdict its test row reports.</returns>
    public static UpstreamCaseVerdict Judge(int testNumber, UpstreamCaseOutcome outcome, bool isListed)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return (outcome.Kind, isListed) switch
        {
            (UpstreamCaseOutcomeKind.Passed, true) => new(UpstreamCaseVerdictKind.Pass, $"test{testNumber} passes"),
            (UpstreamCaseOutcomeKind.Passed, false) => new(UpstreamCaseVerdictKind.Inconclusive, $"test{testNumber} passes; add {testNumber} to {PassingListFileName}"),
            (UpstreamCaseOutcomeKind.Failed, true) => new(UpstreamCaseVerdictKind.Fail, $"test{testNumber} is on {PassingListFileName} and now fails: {outcome.Detail}"),
            (UpstreamCaseOutcomeKind.Failed, false) => new(UpstreamCaseVerdictKind.Inconclusive, $"test{testNumber} fails: {outcome.Detail}"),
            (_, true) => new(UpstreamCaseVerdictKind.Fail, $"test{testNumber} is on {PassingListFileName} and is now skipped: {outcome.Detail}"),
            _ => new(UpstreamCaseVerdictKind.Inconclusive, $"test{testNumber} is skipped: {outcome.Detail}"),
        };
    }
}
