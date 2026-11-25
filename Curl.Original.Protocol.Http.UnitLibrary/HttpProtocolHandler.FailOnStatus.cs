using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that fails a transfer on its status code for --fail and --fail-with-body.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Ends the transfer with exit 22 before the body when <paramref name="fail" /> is
    /// <c>-f</c> and the final status is 400 or above. The head is written first.
    /// </summary>
    /// <exception cref="HttpTransferException">The status fails the transfer (exit 22).</exception>
    private static void ThrowIfFailingBeforeBody(HttpFailMode fail, HttpResponseHead head)
    {
        int statusCode = head.StatusLine.StatusCode;
        if (fail == HttpFailMode.Fail && statusCode >= 400)
        {
            throw new HttpTransferException(CurlExitCode.HttpReturnedError, HttpTransferMessages.RequestedUrlReturnedError(statusCode));
        }
    }

    /// <summary>
    /// Tells whether <c>--fail-with-body</c> fails an exchange that ran whole: the final status
    /// is 400 or above.
    /// </summary>
    private static bool FailsAfterBody(HttpFailMode fail, HttpResponseHead head) =>
        fail == HttpFailMode.FailWithBody && head.StatusLine.StatusCode >= 400;

    /// <summary>
    /// Builds the exit 22 result of an exchange <c>--fail-with-body</c> fails
    /// (<see cref="FailsAfterBody" />). curl 8.22.0's tool, not libcurl, raises this error once
    /// the transfer has succeeded, so <c>-v</c> gets no line for it and the connection is kept as
    /// after any response, for <c>--retry</c> to reuse (measured, BL-5891 Notes).
    /// </summary>
    private static TransferResult FailedAfterBody(HttpResponseHead head, TransferReport report, long bytesWritten) =>
        TransferResult.Failure(CurlExitCode.HttpReturnedError, HttpTransferMessages.RequestedUrlReturnedError(head.StatusLine.StatusCode), bytesWritten)
            with
        {
            Report = report,
            WriteOutErrorMessage = HttpTransferMessages.HttpResponseCodeSaidError,
        };
}
