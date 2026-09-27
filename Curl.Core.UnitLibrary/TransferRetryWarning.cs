using System.Globalization;

namespace Curl.Core;

/// <summary>
/// curl 8.21.0's <c>Warning: Problem : HTTP error. Retrying in 1 second. 3 retries left.</c>
/// line, printed before each wait for a retry.
/// </summary>
/// <remarks>
/// Measured with curl 8.21.0 (mingw, Schannel) on 2026-09-26; the commands are in BL-208's
/// notes. The wait is whole seconds, or seconds and three decimals when it has a millisecond
/// part (<c>1.500 seconds</c>); <c>second</c> is singular only for exactly one second, and
/// <c>retry</c> only for one retry left. The line is not wrapped here: the caller wraps it at
/// the terminal width as it does every <c>Warning:</c> line.
/// </remarks>
public static class TransferRetryWarning
{
    /// <summary>
    /// The line curl prints instead, and then stops retrying, when a <c>Retry-After</c> wait
    /// would end past <c>--retry-max-time</c>; measured with curl 8.21.0 on 2026-09-27 (BL-317).
    /// </summary>
    public const string RetryAfterExceedsMaxTime =
        "Warning: The Retry-After: time would make this command line exceed the maximum allowed time for retries.";

    /// <summary>
    /// Builds the warning line, without a line terminator.
    /// </summary>
    /// <param name="reason">Why the transfer is retried.</param>
    /// <param name="wait">The wait before the retry, in whole milliseconds.</param>
    /// <param name="retriesLeft">The retries left, counting the one about to run.</param>
    /// <returns>The warning line.</returns>
    public static string For(TransferRetryReason reason, TimeSpan wait, long retriesLeft)
    {
        long milliseconds = (long)wait.TotalMilliseconds;
        string problem = reason switch
        {
            TransferRetryReason.Timeout => ": timeout",
            TransferRetryReason.HttpError => ": HTTP error",
            TransferRetryReason.FtpError => ": FTP error",
            _ => "(retrying all errors)",
        };
        string fraction = milliseconds % 1000 == 0
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $".{milliseconds % 1000:D3}");
        string seconds = milliseconds == 1000 ? "second" : "seconds";
        string retries = retriesLeft > 1 ? "retries" : "retry";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Warning: Problem {problem}. Retrying in {milliseconds / 1000}{fraction} {seconds}. {retriesLeft} {retries} left.");
    }
}
