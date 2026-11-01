using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ssh;

/// <summary>
/// Holds an SSH transfer's connect phase - the identification exchange, the key exchange,
/// the <c>ssh-userauth</c> request, the host-key check and the authentication - to
/// <c>--connect-timeout</c>, counted from the start of the request, before its TCP connect,
/// as libcurl holds every state before <c>DO</c> to it (CGR-010, BL-3539). Once the limit has
/// passed on the context's <see cref="TimeProvider" /> it cancels <see cref="Token" />, and the
/// transfer ends with <see cref="Failure" />: exit 28 and <c>Operation timed out after N
/// milliseconds with 0 bytes received</c>, as curl 8.22.0 reports a server that accepts the
/// connection and never sends its identification (measured 2026-10-26).
/// </summary>
internal sealed class SshConnectPhaseLimit : IDisposable
{
    /// <summary>
    /// curl's <c>DEFAULT_CONNECT_TIMEOUT</c>, the limit when <c>--connect-timeout</c> is not
    /// given or is 0.
    /// </summary>
    internal static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(300);

    private readonly TimeProvider timeProvider;

    private readonly long started;

    private readonly CancellationTokenSource deadline;

    private readonly CancellationTokenSource linked;

    /// <summary>
    /// Initializes a new instance of the <see cref="SshConnectPhaseLimit" /> class, counting
    /// from now, and starts its timer.
    /// </summary>
    /// <param name="context">The transfer, whose <c>--connect-timeout</c>, clock and token it takes.</param>
    internal SshConnectPhaseLimit(ITransferContext context)
    {
        timeProvider = context.TimeProvider;
        started = timeProvider.GetTimestamp();
        TimeSpan limit = context.ConnectTimeout is { } given && given > TimeSpan.Zero ? given : DefaultConnectTimeout;
        deadline = new CancellationTokenSource(limit, timeProvider);
        linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, deadline.Token);
    }

    /// <summary>Gets the token cancelled with the transfer's, or once the limit has passed.</summary>
    internal CancellationToken Token => linked.Token;

    /// <summary>Gets a value indicating whether the limit has passed.</summary>
    internal bool HasPassed => deadline.IsCancellationRequested;

    /// <summary>
    /// Gets the failure the transfer ends with once the limit has passed, its milliseconds
    /// counted from the start of the request to now.
    /// </summary>
    /// <returns>The failure.</returns>
    internal TransferResult Failure() =>
        TransferResult.Failure(
            CurlExitCode.OperationTimedOut,
            string.Create(CultureInfo.InvariantCulture, $"Operation timed out after {(long)timeProvider.GetElapsedTime(started).TotalMilliseconds} milliseconds with 0 bytes received"));

    /// <summary>Stops the timer and releases the token sources.</summary>
    public void Dispose()
    {
        linked.Dispose();
        deadline.Dispose();
    }
}
