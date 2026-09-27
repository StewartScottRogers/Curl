using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Runs a transfer and, under <c>--retry</c>, runs it again after curl 8.21.0's transient
/// failures, waiting curl's backoff or the server's <c>Retry-After</c> on the transfer's
/// <see cref="ITransferContext.TimeProvider" />.
/// </summary>
/// <remarks>
/// <para>
/// A transfer is retried when it failed with exit 28, 6, 5 or 12
/// (<see cref="TransferRetryReason.Timeout" />), or when an http:// or https:// transfer
/// succeeded, or failed with exit 22 under <c>-f</c>, with status 408, 429, 500, 502, 503,
/// 504, 522 or 524 (<see cref="TransferRetryReason.HttpError" />). The scheme is that of the
/// last URL requested, so a redirect chain's last hop decides. Every other result is final,
/// and so is the result of the last retry.
/// </para>
/// <para>
/// The wait before a retry is the <c>Retry-After</c> of an HTTP error, read by
/// <see cref="RetryAfterHeader" />, when it asks for one; otherwise the fixed
/// <see cref="RetryPolicy.Delay" />; otherwise curl's backoff, one second before the first
/// backoff wait and double the last before each later one, capped at ten minutes. A
/// <c>Retry-After</c> wait does not advance the backoff: <c>Retry-After: 5</c> on the first
/// failure and none after waits 5, 1, 2 seconds. Measured with curl 8.21.0 (mingw, Schannel)
/// on 2026-09-26; the commands are in BL-208's notes.
/// </para>
/// </remarks>
/// <param name="transfer">Performs one attempt of the transfer.</param>
public sealed class TransferRetrier(Func<ITransferContext, ValueTask<TransferResult>> transfer)
{
    /// <summary>curl's first backoff wait: one second.</summary>
    public static readonly TimeSpan FirstBackoff = TimeSpan.FromSeconds(1);

    /// <summary>curl's longest backoff wait: ten minutes.</summary>
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(10);

    private static readonly HashSet<CurlExitCode> TimeoutFailures =
    [
        CurlExitCode.OperationTimedOut,
        CurlExitCode.CouldntResolveHost,
        CurlExitCode.CouldntResolveProxy,
        CurlExitCode.FtpAcceptTimeout,
    ];

    private static readonly HashSet<int> TransientHttpStatuses = [408, 429, 500, 502, 503, 504, 522, 524];

    /// <summary>
    /// Runs the transfer, retrying it under <paramref name="policy" />.
    /// </summary>
    /// <param name="context">The transfer; every attempt runs it unchanged.</param>
    /// <param name="policy">The retry options.</param>
    /// <param name="retrying">
    /// Called with each attempt that will be retried and the warning line curl prints for it
    /// (<see cref="TransferRetryWarning" />), before the wait; the caller reports the attempt's
    /// failure, prints the warning unless silenced, and readies the output for the next attempt.
    /// </param>
    /// <returns>The result of the first attempt not retried.</returns>
    /// <exception cref="OperationCanceledException">
    /// <see cref="ITransferContext.CancellationToken" /> was cancelled during a wait.
    /// </exception>
    public async ValueTask<TransferResult> RunAsync(
        ITransferContext context,
        RetryPolicy policy,
        Action<TransferResult, string> retrying)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(retrying);

        RetryWaits waits = new(policy.Delay);
        long retriesLeft = policy.Retries;
        while (true)
        {
            TransferResult result = await transfer(context).ConfigureAwait(false);
            if (retriesLeft <= 0 || RetryReason(context.Url, result) is not { } reason)
            {
                return result;
            }

            TimeSpan wait = waits.Next(RetryAfter(reason, result.Report, context.TimeProvider));
            retrying(result, TransferRetryWarning.For(reason, wait, retriesLeft));
            retriesLeft--;
            await Task.Delay(wait, context.TimeProvider, context.CancellationToken).ConfigureAwait(false);
        }
    }

    private static TransferRetryReason? RetryReason(CurlUrl url, TransferResult result)
    {
        if (TimeoutFailures.Contains(result.ExitCode))
        {
            return TransferRetryReason.Timeout;
        }

        return result.ExitCode is CurlExitCode.Ok or CurlExitCode.HttpReturnedError && IsTransientHttpError(url, result.Report)
            ? TransferRetryReason.HttpError
            : null;
    }

    private static bool IsTransientHttpError(CurlUrl url, TransferReport? report) =>
        report is not null
        && TransientHttpStatuses.Contains(report.ResponseCode)
        && LastScheme(url, report) is "http" or "https";

    private static string LastScheme(CurlUrl url, TransferReport report) =>
        report.EffectiveUrl is { } effective && CurlUrl.TryParse(effective, pathAsIs: false, out CurlUrl? last)
            ? last.Scheme
            : url.Scheme;

    private static TimeSpan RetryAfter(TransferRetryReason reason, TransferReport? report, TimeProvider timeProvider)
    {
        if (reason != TransferRetryReason.HttpError)
        {
            return TimeSpan.Zero;
        }

        // An HTTP error was only found in a report, so the report is set.
        string? value = report!.ResponseHeaders
            .LastOrDefault(header => string.Equals(header.Key, "Retry-After", StringComparison.OrdinalIgnoreCase))
            .Value;
        return value is null
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(RetryAfterHeader.ParseSeconds(value, timeProvider.GetUtcNow()));
    }

    /// <summary>
    /// The waits of one run: a <c>Retry-After</c> when there is one, else the fixed delay,
    /// else curl's doubling backoff, which only advances when it is used.
    /// </summary>
    /// <param name="delay">The fixed <c>--retry-delay</c>, or zero for the backoff.</param>
    private sealed class RetryWaits(TimeSpan delay)
    {
        private TimeSpan backoff;

        public TimeSpan Next(TimeSpan retryAfter)
        {
            if (retryAfter != TimeSpan.Zero)
            {
                return retryAfter;
            }

            if (delay != TimeSpan.Zero)
            {
                return delay;
            }

            backoff = backoff == TimeSpan.Zero ? FirstBackoff : TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, MaxBackoff.Ticks));
            return backoff;
        }
    }
}
