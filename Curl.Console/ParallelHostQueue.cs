namespace Curl.Console;

/// <summary>
/// Holds back the transfers of a <c>-Z</c> run that would talk to a busy host (BL-520): while
/// <c>--parallel-max-host</c> transfers to a host are running, and, without
/// <c>--parallel-immediate</c>, while no <c>http://</c> transfer to it has ended yet, the next
/// <c>http://</c> transfer to it waits. curl 8.21.0 waits there for the first connection to show whether
/// it can multiplex, which an HTTP/1.1 connection over plain TCP only shows once its transfer ends
/// (measured 2026-09-28, BL-520 Notes). A waiting transfer keeps its <c>--parallel-max</c> slot, as it
/// does in curl, and waiting transfers start in the order they began to wait.
/// </summary>
/// <param name="maxPerHost">The <c>--parallel-max-host</c> limit; zero for none.</param>
/// <param name="parallelImmediate">Whether <c>--parallel-immediate</c> was given.</param>
internal sealed class ParallelHostQueue(int maxPerHost, bool parallelImmediate)
{
    /// <summary>The transfers to each host, by <c>host:port</c>.</summary>
    private readonly Dictionary<string, HostTransfers> hosts = new(StringComparer.Ordinal);

    /// <summary>
    /// Waits until the transfer of <paramref name="url" /> may start talking to its host, then counts it
    /// as running there until <see cref="Leave" />. A URL with no host, such as <c>file://</c>, never waits.
    /// </summary>
    /// <param name="url">The transfer's URL, with its scheme.</param>
    /// <param name="abortToken">Ends the wait, which then throws, without counting the transfer.</param>
    /// <returns>A task that completes when the transfer may start.</returns>
    internal Task WaitForHostAsync(string url, CancellationToken abortToken)
    {
        if (DestinationOf(url) is not { } destination)
        {
            return Task.CompletedTask;
        }

        lock (hosts)
        {
            HostTransfers transfers = TransfersTo(destination.Host);
            WaitingTransfer transfer = new(destination.WaitsForFirstToEnd);
            if (transfers.MayStart(transfer, maxPerHost))
            {
                transfers.Running++;
                return Task.CompletedTask;
            }

            transfers.Waiting.AddLast(transfer);
            return WaitForTurnAsync(transfers, transfer, abortToken);
        }
    }

    /// <summary>
    /// Counts the transfer of <paramref name="url" /> as no longer running at its host, and starts the
    /// transfers waiting for it that may now start.
    /// </summary>
    /// <param name="url">The URL <see cref="WaitForHostAsync" /> was given.</param>
    internal void Leave(string url)
    {
        if (DestinationOf(url) is not { } destination)
        {
            return;
        }

        lock (hosts)
        {
            HostTransfers transfers = hosts[destination.Host];
            transfers.Running--;
            transfers.FirstHasEnded = true;
            while (transfers.Waiting.First is { } next && transfers.MayStart(next.Value, maxPerHost))
            {
                transfers.Waiting.RemoveFirst();
                transfers.Running++;
                next.Value.Turn.SetResult();
            }
        }
    }

    /// <summary>
    /// Waits for a queued transfer's turn; when <paramref name="abortToken" /> is cancelled first, the
    /// transfer leaves the queue and the wait throws.
    /// </summary>
    private async Task WaitForTurnAsync(HostTransfers transfers, WaitingTransfer transfer, CancellationToken abortToken)
    {
        using CancellationTokenRegistration withdrawal = abortToken.Register(() =>
        {
            lock (hosts)
            {
                transfer.Turn.TrySetCanceled(abortToken);
                transfers.Waiting.Remove(transfer);
            }
        });
        await transfer.Turn.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the host a URL talks to, as <c>host:port</c>, and whether its transfer waits for the host's
    /// first to end: an <c>http://</c> URL without <c>--parallel-immediate</c>.
    /// </summary>
    private (string Host, bool WaitsForFirstToEnd)? DestinationOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && uri.Host.Length > 0
            ? ($"{uri.Host}:{uri.Port}", !parallelImmediate && uri.Scheme == Uri.UriSchemeHttp)
            : null;

    /// <summary>Gets the transfers to a host, creating the entry for its first.</summary>
    private HostTransfers TransfersTo(string host)
    {
        if (!hosts.TryGetValue(host, out HostTransfers? transfers))
        {
            transfers = new HostTransfers();
            hosts.Add(host, transfers);
        }

        return transfers;
    }

    /// <summary>The transfers to one host: how many run, whether one has ended, and which wait.</summary>
    private sealed class HostTransfers
    {
        /// <summary>Gets or sets how many transfers to the host are running.</summary>
        internal int Running { get; set; }

        /// <summary>Gets or sets a value indicating whether a transfer to the host has ended.</summary>
        internal bool FirstHasEnded { get; set; }

        /// <summary>Gets the transfers waiting for the host, in the order they began to wait.</summary>
        internal LinkedList<WaitingTransfer> Waiting { get; } = [];

        /// <summary>
        /// Tells whether <paramref name="transfer" /> may start: fewer than one transfer runs while it
        /// waits for the host's first to end, and fewer than <paramref name="maxPerHost" />, unless zero,
        /// otherwise.
        /// </summary>
        internal bool MayStart(WaitingTransfer transfer, int maxPerHost)
        {
            int limit = transfer.WaitsForFirstToEnd && !FirstHasEnded ? 1 : maxPerHost;

            return limit == 0 || Running < limit;
        }
    }

    /// <summary>A transfer waiting for its host, and the signal that it may start.</summary>
    /// <param name="waitsForFirstToEnd">Whether it waits for the host's first transfer to end.</param>
    private sealed class WaitingTransfer(bool waitsForFirstToEnd)
    {
        /// <summary>Gets a value indicating whether it waits for the host's first transfer to end.</summary>
        internal bool WaitsForFirstToEnd { get; } = waitsForFirstToEnd;

        /// <summary>Gets the signal set when it may start, or cancelled when its wait is abandoned.</summary>
        internal TaskCompletionSource Turn { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
