using Curl.Protocol.Abstractions;

namespace Curl.Networking;

// The lines for a query whose connection or exchange failed.
internal static partial class DohTraceLines
{
    private static IEnumerable<string> ExchangeFailureLines(Func<CurlExitCode, string> describeExitCode, IReadOnlyList<DohQueryResult> results) =>
        results.Where(result => result.ExchangeFailure != CurlExitCode.Ok).Select(failed => $"[DNS] DoH request {describeExitCode(failed.ExchangeFailure)}");
}
