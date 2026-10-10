namespace Curl.Conformance;

/// <summary>What an interpreted <c>%PERL -e</c> one-liner did: its exit code and what it printed.</summary>
/// <param name="ExitCode">The exit code Perl would end with: 0 when a check passes.</param>
/// <param name="Output">What the one-liner printed to standard output, the text a precheck skips a case with.</param>
internal sealed record UpstreamPerlOneLinerResult(int ExitCode, string Output);
