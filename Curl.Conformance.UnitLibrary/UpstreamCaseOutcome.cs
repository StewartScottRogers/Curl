namespace Curl.Conformance;

/// <summary>How one test case ended when <see cref="UpstreamCaseRunner"/> ran it, and why.</summary>
public sealed class UpstreamCaseOutcome
{
    private UpstreamCaseOutcome(UpstreamCaseOutcomeKind kind, string detail, bool runsOnlyOnAnotherPlatform = false)
    {
        Kind = kind;
        Detail = detail;
        RunsOnlyOnAnotherPlatform = runsOnlyOnAnotherPlatform;
    }

    /// <summary>Whether the case passed, failed, or was not run.</summary>
    public UpstreamCaseOutcomeKind Kind { get; }

    /// <summary>
    /// For a failure, the first difference; for a skipped case, the reason it was not run; empty
    /// for a pass.
    /// </summary>
    public string Detail { get; }

    /// <summary>
    /// Whether the case was skipped only because its <c>&lt;features&gt;</c> are met by Curl on
    /// another platform and not on this one, such as a case needing <c>Schannel</c> off Windows
    /// (ADR-0488); <see langword="false"/> for every other outcome.
    /// </summary>
    public bool RunsOnlyOnAnotherPlatform { get; }

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

    /// <summary>
    /// A case not run because its <c>&lt;features&gt;</c> are met by Curl on another platform and
    /// not on this one; see <see cref="RunsOnlyOnAnotherPlatform"/>.
    /// </summary>
    /// <param name="reason">The feature this platform's Curl lacks, or has when the case needs it absent.</param>
    /// <returns>The outcome.</returns>
    public static UpstreamCaseOutcome SkippedOnThisPlatform(string reason) => new(UpstreamCaseOutcomeKind.Skipped, reason, runsOnlyOnAnotherPlatform: true);
}
