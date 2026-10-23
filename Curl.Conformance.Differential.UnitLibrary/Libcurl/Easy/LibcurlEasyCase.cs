namespace Curl.Conformance.Differential.Libcurl.Easy;

/// <summary>An easy-handle case: rounds run one after another on one easy handle.</summary>
/// <param name="Name">The case's name in the report.</param>
/// <param name="Rounds">The rounds.</param>
public sealed record LibcurlEasyCase(string Name, IReadOnlyList<LibcurlEasyRound> Rounds);
