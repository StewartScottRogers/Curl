namespace Curl.Conformance.Differential;

/// <summary>
/// A known limitation of the reference curl 8.22.0 build (Git for Windows' ucrt64 debug build) that
/// makes a case's difference the reference's fault rather than Curl's (ADR-0781). A case tagged with
/// one is not run against that build.
/// </summary>
public enum ReferenceBuildLimitation
{
    /// <summary>The reference reads its command line as ANSI, so non-ASCII arguments arrive mangled.</summary>
    AnsiArguments,

    /// <summary>The reference has a 32-bit <c>time_t</c>, so dates past 2038 overflow.</summary>
    ThirtyTwoBitTimeT,

    /// <summary>The reference is built without HTTP/2.</summary>
    NoHttp2,

    /// <summary>The reference's IDN conversion produces wrong punycode for non-ASCII host names.</summary>
    BrokenIdn,
}
