using Curl.Api.Info;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class TransferInfoFromReport
{
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

    private static long BodylessOrSingleLengthOf(int responseCode, long[] lengths) =>
        responseCode is 204 or 304 ? 0 : SingleLengthOf(lengths);

    private static long LengthOf(string value) => long.TryParse(value, out long length) ? length : -1;

    private static long SingleLengthOf(long[] lengths) => lengths.Length == 1 ? lengths[0] : -1;
}
