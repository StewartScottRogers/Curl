namespace Curl.Conformance.Extended.Harness;

/// <summary>What one in-process curl run ended with, and what went over each connection or datagram channel it opened.</summary>
/// <param name="ExitCode">curl's exit code.</param>
/// <param name="StandardOutput">The bytes curl wrote to standard output.</param>
/// <param name="StandardError">The bytes curl wrote to standard error.</param>
/// <param name="Exchanges">One exchange per connection or datagram channel, in the order curl opened them.</param>
public sealed record CurlRunResult(int ExitCode, byte[] StandardOutput, byte[] StandardError, IReadOnlyList<WireExchange> Exchanges);
