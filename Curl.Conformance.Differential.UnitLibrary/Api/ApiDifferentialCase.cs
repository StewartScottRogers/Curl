namespace Curl.Conformance.Differential.Api;

/// <summary>
/// One <c>Curl.Api</c> differential case: a request through <c>Curl.Api</c> beside the curl
/// command line that makes the same transfer (BL-3242, ADR-0790).
/// </summary>
/// <param name="Name">The case's name in the report.</param>
/// <param name="Command">The equivalent curl command line, with <c>{U}</c> for the loopback server's base URL.</param>
/// <param name="Request">The <c>Curl.Api</c> request.</param>
public sealed record ApiDifferentialCase(string Name, string Command, ApiDifferentialRequest Request)
{
    /// <summary>
    /// The arguments put before <see cref="Command"/>'s: <c>-A ''</c> sends no <c>User-Agent</c>, as
    /// <c>Curl.Api</c> by default (only the curl tool sets one), and <c>-w</c> writes the response
    /// code after the body, as <see cref="ApiDifferentialOutput.Render"/> does for <c>Curl.Api</c>.
    /// A case's own <c>-A</c> comes later and wins.
    /// </summary>
    public static IReadOnlyList<string> LeadingArguments { get; } = ["-A", "", "-w", "\\n%{response_code}"];

    /// <summary>How long either side may take before it counts as timed out, as the CLI cases' default.</summary>
    public static TimeSpan Timeout { get; } = TimeSpan.FromSeconds(20);

    /// <summary>Gets the case the reference curl runs: <see cref="LeadingArguments"/> then <see cref="Command"/> split as <c>shlex</c> does.</summary>
    public DifferentialCase ToDifferentialCase() =>
        new(Name, [.. LeadingArguments, .. CommandLineSplitter.Split(Command)], null, new Dictionary<string, byte[]>(), Timeout, true, true, []);
}
