using Curl.Api.Info;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class TransferInfoFromReport
{
    private static readonly CurlExitCode[] ExitsBeforeBody =
        [CurlExitCode.UnsupportedProtocol, CurlExitCode.HttpReturnedError, CurlExitCode.RangeError, CurlExitCode.BadDownloadResume, CurlExitCode.FilesizeExceeded];

    private static bool StoppedBeforeBody(TransferResult result) => ExitsBeforeBody.Contains(result.ExitCode) || RefusedContentLength(result);

    /// <summary>
    /// Whether the HTTP handler refused the response's <c>Content-Length</c> headers (differing duplicates or an unparsable value,
    /// exit 8): its report holds only the headers before the refused one, while libcurl 8.22.0 never sets the length (BL-3341).
    /// </summary>
    private static bool RefusedContentLength(TransferResult result) =>
        result.ExitCode == CurlExitCode.WeirdServerReply && result.ErrorMessage == InvalidContentLengthMessage;

    private const string InvalidContentLengthMessage = "Invalid Content-Length: value";
}
