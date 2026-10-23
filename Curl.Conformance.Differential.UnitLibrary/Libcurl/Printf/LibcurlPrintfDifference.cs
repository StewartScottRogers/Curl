namespace Curl.Conformance.Differential.Libcurl.Printf;

/// <summary>A scenario whose result lines differ between the two libraries.</summary>
/// <param name="Scenario">The scenario.</param>
/// <param name="Ours">Curl's line.</param>
/// <param name="Reference">The reference libcurl's line.</param>
public sealed record LibcurlPrintfDifference(LibcurlPrintfScenario Scenario, string Ours, string Reference);
