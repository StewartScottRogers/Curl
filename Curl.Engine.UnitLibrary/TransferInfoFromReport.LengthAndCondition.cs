using Curl.Api.Info;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class TransferInfoFromReport
{
    /// <summary>The info with the response's <c>Content-Length</c> and whether a time condition was unmet.</summary>
    private static TransferInfo WithLengthAndCondition(TransferInfo info, TransferResult result, TransferReport report) => info with
    {
        ConditionUnmet = result.TimeConditionUnmet ? 1 : 0,
        ContentLengthDownload = ContentLengthOf(report),
    };

    /// <summary>The last response's <c>Content-Length</c>; -1 when it has none, or none that parses.</summary>
    private static long ContentLengthOf(TransferReport report) =>
        report.ResponseHeaders
            .Where(header => string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
            .Select(header => long.TryParse(header.Value.Trim(), out long length) ? length : -1)
            .LastOrDefault(-1);
}
