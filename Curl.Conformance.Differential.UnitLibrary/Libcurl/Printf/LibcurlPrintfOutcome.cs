namespace Curl.Conformance.Differential.Libcurl.Printf;

/// <summary>What replaying one printf input file through both libraries found.</summary>
/// <param name="Name">The input file's name, such as <c>pf_in</c>.</param>
/// <param name="Identical">How many scenarios gave the same line in both.</param>
/// <param name="NotRunAgainstReference">How many scenarios were not run against the reference build.</param>
/// <param name="Differences">The scenarios whose lines differ, in file order.</param>
public sealed record LibcurlPrintfOutcome(string Name, int Identical, int NotRunAgainstReference, IReadOnlyList<LibcurlPrintfDifference> Differences);
