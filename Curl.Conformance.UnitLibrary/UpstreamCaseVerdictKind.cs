namespace Curl.Conformance;

/// <summary>What a case's test row reports.</summary>
public enum UpstreamCaseVerdictKind
{
    /// <summary>A listed case that passed.</summary>
    Pass,

    /// <summary>A listed case that failed or was skipped: a regression.</summary>
    Fail,

    /// <summary>An unlisted case, whatever its outcome.</summary>
    Inconclusive,
}
