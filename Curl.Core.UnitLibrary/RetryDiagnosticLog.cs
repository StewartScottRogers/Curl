using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Writes <see cref="TransferRetrier" />'s decisions to Curl's own diagnostic log, component
/// <see cref="DiagnosticLogComponents.Retry" /> (ADR-0222, BL-921): the retries running out as
/// <c>error</c> with the last attempt's <see cref="CurlExitCode" />, each retry with its reason
/// and wait, and retries stopped by <c>--retry-max-time</c>, as <c>warning</c>, and each
/// attempt's outcome and the counters behind the decision as <c>verbose</c>.
/// </summary>
/// <param name="log">Where the lines go; <see cref="NoDiagnosticLog.Instance" /> writes nothing.</param>
/// <remarks>
/// Every method tests <see cref="IDiagnosticLog.IsEnabled" /> before it builds its message, so a
/// disabled level costs no formatting. No URL is written, so no credential can be.
/// </remarks>
internal sealed class RetryDiagnosticLog(IDiagnosticLog log)
{
    /// <summary>
    /// Logs, at <c>verbose</c>, how attempt <paramref name="attempt" /> ended and the counters the
    /// retry decision reads.
    /// </summary>
    /// <param name="attempt">The attempt's number, from 1.</param>
    /// <param name="result">The attempt's result.</param>
    /// <param name="retriesLeft">The retries left before this attempt's decision.</param>
    /// <param name="elapsed">The time since the first attempt began.</param>
    public void AttemptEnded(int attempt, TransferResult result, long retriesLeft, TimeSpan elapsed)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, string.Create(
                CultureInfo.InvariantCulture,
                $"attempt {attempt} ended with exit {(int)result.ExitCode} ({result.ExitCode}); {retriesLeft} retries left, {(long)elapsed.TotalMilliseconds} ms since the first attempt began"));
        }
    }

    /// <summary>Logs, at <c>warning</c>, that attempt <paramref name="attempt" /> is retried, why and after how long.</summary>
    /// <param name="attempt">The attempt's number, from 1.</param>
    /// <param name="reason">Why it is retried.</param>
    /// <param name="wait">The wait before the retry.</param>
    /// <param name="retriesLeft">The retries left, counting the one about to run.</param>
    public void Retrying(int attempt, TransferRetryReason reason, TimeSpan wait, long retriesLeft)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Warning))
        {
            Write(DiagnosticLogLevel.Warning, string.Create(
                CultureInfo.InvariantCulture,
                $"attempt {attempt} failed ({reason}); retrying in {(long)wait.TotalMilliseconds} ms, {retriesLeft} retries left"));
        }
    }

    /// <summary>
    /// Logs, at <c>error</c>, that a <c>--retry</c> budget ran out with attempt
    /// <paramref name="attempt" /> still failing; nothing without <c>--retry</c>.
    /// </summary>
    /// <param name="retries">The <c>--retry</c> count.</param>
    /// <param name="attempt">The last attempt's number, from 1.</param>
    /// <param name="result">The last attempt's result.</param>
    public void RetriesExhausted(long retries, int attempt, TransferResult result)
    {
        if (retries > 0 && log.IsEnabled(DiagnosticLogLevel.Error))
        {
            Write(DiagnosticLogLevel.Error, string.Create(
                CultureInfo.InvariantCulture,
                $"--retry {retries} exhausted after {attempt} attempts; the last ended with exit {(int)result.ExitCode} ({result.ExitCode})"));
        }
    }

    /// <summary>Logs, at <c>warning</c>, that <c>--retry-max-time</c> stops the retries after attempt <paramref name="attempt" />.</summary>
    /// <param name="attempt">The last attempt's number, from 1.</param>
    /// <param name="maxTime">The <c>--retry-max-time</c>.</param>
    public void MaxTimeReached(int attempt, TimeSpan maxTime)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Warning))
        {
            Write(DiagnosticLogLevel.Warning, string.Create(
                CultureInfo.InvariantCulture,
                $"--retry-max-time {(long)maxTime.TotalMilliseconds} ms reached after attempt {attempt}; not retrying"));
        }
    }

    private void Write(DiagnosticLogLevel level, string message) => log.Write(level, DiagnosticLogComponents.Retry, message);
}
