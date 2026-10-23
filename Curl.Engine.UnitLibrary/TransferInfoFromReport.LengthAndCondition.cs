using Curl.Api.Info;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class TransferInfoFromReport
{
    private const string FileScheme = "file";

    /// <summary>
    /// The info with the response's <c>Content-Length</c> and whether a time condition was unmet: libcurl 8.22.0
    /// answers <c>CURLINFO_CONDITION_UNMET</c> 1 for every 304, a time condition set or not (BL-3194).
    /// </summary>
    private static TransferInfo WithLengthAndCondition(TransferInfo info, TransferResult result, TransferReport report, string effectiveUrl) => info with
    {
        ConditionUnmet = result.TimeConditionUnmet || report.ResponseCode == 304 ? 1 : 0,
        ContentLengthDownload = SchemeOf(effectiveUrl) == FileScheme ? FileLengthOf(result, report) : ContentLengthOf(result, report),
    };

    /// <summary>
    /// The last response's <c>Content-Length</c> as libcurl 8.22.0 answers <c>CURLINFO_CONTENT_LENGTH_DOWNLOAD_T</c>
    /// (report 23, BL-3194): -1 after <c>CURLOPT_FAILONERROR</c> stopped at the status line, for a chunked response
    /// (whose <c>Content-Length</c> is ignored), for lengths that differ, or for none that parses; 0 for a 204, and for a
    /// 304 that sends one.
    /// </summary>
    private static long ContentLengthOf(TransferResult result, TransferReport report)
    {
        long[] lengths = [.. HeaderValues(report.ResponseHeaders, "Content-Length").Select(LengthOf).Distinct()];
        return result.ExitCode == CurlExitCode.HttpReturnedError || IsChunked(report) ? -1 : BodylessOrSingleLengthOf(report.ResponseCode, lengths);
    }

    private static long BodylessOrSingleLengthOf(int responseCode, long[] lengths) =>
        responseCode == 204 || (responseCode == 304 && lengths.Length > 0) ? 0 : SingleLengthOf(lengths);

    private static long LengthOf(string value) => long.TryParse(value, out long length) ? length : -1;

    private static long SingleLengthOf(long[] lengths) => lengths.Length == 1 ? lengths[0] : -1;

    private static bool IsChunked(TransferReport report) =>
        HeaderValues(report.ResponseHeaders, "Transfer-Encoding").Any(value => value.Contains("chunked", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A <c>file://</c> transfer's length, which libcurl's <c>file_do</c> sets from the file's size: the
    /// <c>Content-Length</c> its <c>-I</c> pseudo-headers carry, else the bytes a successful transfer moved; -1 after a failure.
    /// </summary>
    private static long FileLengthOf(TransferResult result, TransferReport report)
    {
        string? length = HeaderValues(report.PseudoHeaders, "Content-Length").LastOrDefault();
        if (length is not null)
        {
            return LengthOf(length);
        }

        return result.IsSuccess ? result.BytesTransferred : -1;
    }

    private static IEnumerable<string> HeaderValues(IReadOnlyList<KeyValuePair<string, string>> headers, string name) =>
        headers.Where(header => string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase)).Select(header => header.Value.Trim());
}
