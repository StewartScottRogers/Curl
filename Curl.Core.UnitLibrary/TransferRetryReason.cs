namespace Curl.Core;

/// <summary>
/// Why <see cref="TransferRetrier" /> runs a transfer again, named as curl 8.21.0 names it
/// in its warning line.
/// </summary>
public enum TransferRetryReason
{
    /// <summary>
    /// The transfer timed out (exit 28), could not resolve its host (6) or proxy (5), or an
    /// FTP server did not connect back in time (12): <c>: timeout</c>.
    /// </summary>
    Timeout,

    /// <summary>
    /// An HTTP or HTTPS server answered 408, 429, 500, 502, 503, 504, 522 or 524:
    /// <c>: HTTP error</c>.
    /// </summary>
    HttpError,

    /// <summary>
    /// An FTP or FTPS transfer failed after the server's last reply was a 4xx:
    /// <c>: FTP error</c>.
    /// </summary>
    FtpError,

    /// <summary>
    /// The transfer failed for a reason curl does not retry by itself, and
    /// <c>--retry-all-errors</c> was given: <c>(retrying all errors)</c>.
    /// </summary>
    AllErrors,
}
