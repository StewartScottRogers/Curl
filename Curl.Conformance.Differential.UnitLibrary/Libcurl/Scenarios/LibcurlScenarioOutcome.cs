namespace Curl.Conformance.Differential.Libcurl.Scenarios;

/// <summary>The lines one <see cref="LibcurlScenario"/> replayed to against Curl's Libcurl and against the reference libcurl.</summary>
/// <param name="Scenario">The scenario replayed.</param>
/// <param name="Ours">The lines Curl's Libcurl gave.</param>
/// <param name="Reference">The lines the reference libcurl 8.22.0 gave.</param>
public sealed record LibcurlScenarioOutcome(LibcurlScenario Scenario, IReadOnlyList<string> Ours, IReadOnlyList<string> Reference)
{
    /// <summary>Gets whether the two libraries gave different lines.</summary>
    public bool Differs => !Ours.SequenceEqual(Reference, StringComparer.Ordinal);
}
