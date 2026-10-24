using Curl.Api.Info;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class TransferInfoFromReport
{
    /// <summary>
    /// The info with the response's <c>Content-Length</c> and whether a time condition was unmet: libcurl 8.22.0
    /// answers <c>CURLINFO_CONDITION_UNMET</c> 1 for every 304, a time condition set or not (BL-3194).
    /// </summary>
    private static TransferInfo WithLengthAndCondition(TransferInfo info, TransferResult result, TransferReport report, string effectiveUrl, (bool Uploaded, bool ContentLengthIgnored) request) => info with
    {
        ConditionUnmet = result.TimeConditionUnmet || report.ResponseCode == 304 ? 1 : 0,
        ContentLengthDownload = SchemeOf(effectiveUrl) == FileScheme ? FileLengthOf(result, report, effectiveUrl, request.Uploaded) : ContentLengthOf(result, report, request.ContentLengthIgnored),
    };
}
