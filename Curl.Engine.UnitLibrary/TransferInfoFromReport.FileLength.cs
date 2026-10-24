using Curl.Api.Info;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class TransferInfoFromReport
{
    private const string FileScheme = "file";

    private static bool IsDirectory(string url) => url.EndsWith('/');

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
}
