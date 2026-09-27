namespace Curl.Conformance;

/// <summary>What one case's test row reports, decided by <see cref="UpstreamCaseRatchet.Judge"/>.</summary>
/// <param name="kind">Pass, fail or inconclusive.</param>
/// <param name="message">The sentence the row reports.</param>
public sealed class UpstreamCaseVerdict(UpstreamCaseVerdictKind kind, string message)
{
    /// <summary>Pass, fail or inconclusive.</summary>
    public UpstreamCaseVerdictKind Kind { get; } = kind;

    /// <summary>The sentence the row reports: the case, and why it passed, failed or is inconclusive.</summary>
    public string Message { get; } = message;
}
