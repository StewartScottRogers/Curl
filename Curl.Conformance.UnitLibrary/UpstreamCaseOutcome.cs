namespace Curl.Conformance;

/// <summary>How one test case ended when <see cref="UpstreamCaseRunner"/> ran it, and why.</summary>
public sealed class UpstreamCaseOutcome
{
    private UpstreamCaseOutcome(UpstreamCaseOutcomeKind kind, string detail)
    {
        Kind = kind;
        Detail = detail;
    }

    /// <summary>Whether the case passed, failed, or was not run.</summary>
    public UpstreamCaseOutcomeKind Kind { get; }

    /// <summary>
    /// For a failure, the first difference; for a skipped case, the reason it was not run; empty
    /// for a pass.
    /// </summary>
    public string Detail { get; }

    /// <summary>A case whose run matched everything it verifies.</summary>
    public static UpstreamCaseOutcome Passed { get; } = new(UpstreamCaseOutcomeKind.Passed, string.Empty);

    /// <summary>A case that ran and differed.</summary>
    /// <param name="firstDifference">The first difference.</param>
    /// <returns>The outcome.</returns>
    public static UpstreamCaseOutcome Failed(string firstDifference) => new(UpstreamCaseOutcomeKind.Failed, firstDifference);

    /// <summary>A case the harness cannot run yet.</summary>
    /// <param name="reason">Why it was not run.</param>
    /// <returns>The outcome.</returns>
    public static UpstreamCaseOutcome Skipped(string reason) => new(UpstreamCaseOutcomeKind.Skipped, reason);
}
