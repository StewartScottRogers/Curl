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
    private static TransferInfo WithLengthAndCondition(TransferInfo info, TransferResult result, TransferReport report, string effectiveUrl, (bool Uploaded, bool ContentLengthIgnored) request) => info with
    {
        ConditionUnmet = result.TimeConditionUnmet || report.ResponseCode == 304 ? 1 : 0,
        ContentLengthDownload = SchemeOf(effectiveUrl) == FileScheme ? FileLengthOf(result, report, effectiveUrl, request.Uploaded) : ContentLengthOf(result, report, request.ContentLengthIgnored),
    };

    /// <summary>
    /// The last response's <c>Content-Length</c> as libcurl 8.22.0 answers <c>CURLINFO_CONTENT_LENGTH_DOWNLOAD_T</c>
    /// (report 23, BL-3194, BL-3323): -1 after a failure that stopped the transfer before its body (a refused
    /// protocol, <c>CURLOPT_FAILONERROR</c>, a refused range or resume, a file over <c>CURLOPT_MAXFILESIZE</c>), for a chunked response to anything but a HEAD (whose <c>Content-Length</c> is ignored), for
    /// lengths that differ, or for none that parses; 0 for a 204 and for any 304; the header's length after an unmet
    /// time condition, whatever status the engine reports for it; -1 under <c>CURLOPT_IGNORE_CONTENT_LENGTH</c>.
    /// </summary>
    private static long ContentLengthOf(TransferResult result, TransferReport report, bool contentLengthIgnored)
    {
        long[] lengths = [.. HeaderValues(report.ResponseHeaders, "Content-Length").Select(LengthOf).Distinct()];
        if (contentLengthIgnored)
        {
            return -1;
        }

        return result.TimeConditionUnmet ? SingleLengthOf(lengths) : AnsweredLengthOf(result, report, lengths);
    }

    private static long AnsweredLengthOf(TransferResult result, TransferReport report, long[] lengths) =>
        StoppedBeforeBody(result) || (IsChunked(report) && !IsHead(report)) ? -1 : BodylessOrSingleLengthOf(report.ResponseCode, lengths);

    private static readonly CurlExitCode[] ExitsBeforeBody =
        [CurlExitCode.UnsupportedProtocol, CurlExitCode.HttpReturnedError, CurlExitCode.RangeError, CurlExitCode.BadDownloadResume, CurlExitCode.FilesizeExceeded];

    private static bool StoppedBeforeBody(TransferResult result) => ExitsBeforeBody.Contains(result.ExitCode);

    private static bool IsHead(TransferReport report) => string.Equals(report.Method, "HEAD", StringComparison.OrdinalIgnoreCase);

    private static long BodylessOrSingleLengthOf(int responseCode, long[] lengths) =>
        responseCode is 204 or 304 ? 0 : SingleLengthOf(lengths);

    private static bool IsDirectory(string url) => url.EndsWith('/');

    private static long LengthOf(string value) => long.TryParse(value, out long length) ? length : -1;

    private static long SingleLengthOf(long[] lengths) => lengths.Length == 1 ? lengths[0] : -1;

    private static bool IsChunked(TransferReport report) =>
        HeaderValues(report.ResponseHeaders, "Transfer-Encoding").Any(value => value.Contains("chunked", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A <c>file://</c> transfer's length, which libcurl's <c>file_do</c> sets from the file's size: the
    /// <c>Content-Length</c> its <c>-I</c> pseudo-headers carry, else the bytes a successful download moved; -1 after a failure and after an upload, whose <c>file_upload</c> sets no download size (libcurl 8.22.0 lib/file.c).
    /// </summary>
    private static long FileLengthOf(TransferResult result, TransferReport report, string effectiveUrl, bool uploaded)
    {
        string? length = HeaderValues(report.PseudoHeaders, "Content-Length").LastOrDefault();
        if (length is not null)
        {
            return LengthOf(length);
        }

        return result.IsSuccess && !uploaded && !IsDirectory(effectiveUrl) ? result.BytesTransferred : -1;
    }

    private static IEnumerable<string> HeaderValues(IReadOnlyList<KeyValuePair<string, string>> headers, string name) =>
        headers.Where(header => string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase)).Select(header => header.Value.Trim());
}
