namespace Curl.Conformance;

/// <summary>How one test case ended.</summary>
public enum UpstreamCaseOutcomeKind
{
    /// <summary>The case ran and matched everything it verifies.</summary>
    Passed,

    /// <summary>The case ran and differed; it counts as runnable.</summary>
    Failed,

    /// <summary>The harness cannot run the case yet; it does not count as runnable.</summary>
    Skipped,
}
