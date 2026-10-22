namespace Curl.Networking;

// The line for an A or AAAA answer that did not decode.
internal static partial class DohTraceLines
{
    private static string? DecodeFailureLine(string probe, DnsAnswer answer) =>
        answer.Failure == DnsMessageFailure.None
            ? null
            : $"{probe} decoding response: {DnsMessageFailureText.Describe(answer.Failure)}";
}
