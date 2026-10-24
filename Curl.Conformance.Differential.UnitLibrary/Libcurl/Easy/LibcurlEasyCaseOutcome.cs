namespace Curl.Conformance.Differential.Libcurl.Easy;

/// <summary>How Curl's Libcurl run of an easy-handle case compared with the reference libcurl's.</summary>
/// <param name="Name">The case's name.</param>
/// <param name="Differences">Each difference as its report line; empty when the runs agree.</param>
public sealed record LibcurlEasyCaseOutcome(string Name, IReadOnlyList<string> Differences);
