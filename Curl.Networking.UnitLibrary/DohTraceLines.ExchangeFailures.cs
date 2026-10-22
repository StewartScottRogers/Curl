using Curl.Protocol.Abstractions;

namespace Curl.Networking;

// The line for an A or AAAA query whose connection or exchange failed, or whose status is outside 200-299.
internal static partial class DohTraceLines
{
    // curl 8.22.0's doh_probe_done fails a probe with a status outside 200-299 before it decodes
    // the body, and says which status (measured, BL-2972).
    private static string ExchangeFailureLine(Func<CurlExitCode, string> describeExitCode, string probe, DohQueryResult result) =>
        result.ExchangeFailure != CurlExitCode.Ok
            ? $"{probe}: {describeExitCode(result.ExchangeFailure)}"
            : FormattableString.Invariant($"{probe}: HTTP status {result.StatusCode}");
}
