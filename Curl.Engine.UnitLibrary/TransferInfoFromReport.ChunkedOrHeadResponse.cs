using Curl.Api.Info;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class TransferInfoFromReport
{
    private static bool IsHead(TransferReport report) => string.Equals(report.Method, "HEAD", StringComparison.OrdinalIgnoreCase);

    private static bool IsChunked(TransferReport report) =>
        HeaderValues(report.ResponseHeaders, "Transfer-Encoding").Any(value => value.Contains("chunked", StringComparison.OrdinalIgnoreCase));
}
