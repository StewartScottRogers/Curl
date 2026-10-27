namespace Curl.Console;

/// <summary>
/// Keeps at most <c>--parallel-max</c> transfers running under <c>-Z</c>: the runner waits for a free
/// slot before it starts the next transfer in command-line order, and a slot frees as any running
/// transfer ends (ADR-0127, decision 1). Each time before it waits for a transfer to end, it runs
/// <see cref="BeforeEachWait" />, so that the transfers the run's host queue freed start after any the
/// freed slot started (ADR-0768).
/// </summary>
/// <param name="maxRunning">The most transfers running at once, at least 1.</param>
internal sealed class ParallelTransferQueue(int maxRunning)
{
    /// <summary>The transfers started and not yet seen to end.</summary>
    private readonly List<Task> running = [];

    /// <summary>The transfers started, ended or not.</summary>
    private readonly List<Task> started = [];

    /// <summary>
    /// Gets a value indicating whether fewer than the maximum transfers are counted as running, a
    /// transfer that ended and was not yet seen to end included.
    /// </summary>
    internal bool HasFreeSlot => running.Count < maxRunning;

    /// <summary>Gets a value indicating whether any transfer has been started.</summary>
    internal bool HasStartedAny => started.Count > 0;

    /// <summary>
    /// Gets or sets what runs each time before the queue waits for a transfer to end, once the caller has
    /// started every transfer it can for now: the run starts the transfers its host queue freed there
    /// (<see cref="ParallelHostQueue.StartReleased" />).
    /// </summary>
    internal Action BeforeEachWait { get; set; } = static () => { };

    /// <summary>
    /// Gets or sets the timestamp at which the run last began adding transfers: when it started its first,
    /// or when a slot came free after a wait. curl adds every transfer of such a batch at once and runs each
    /// one's <c>-m</c> from there, so a transfer that waits for its host counts its <c>-m</c> from this
    /// moment rather than from when its wait began (CGR-109, BL-3638).
    /// </summary>
    internal long BatchAddedTimestamp { get; set; }

    /// <summary>Waits until fewer than the maximum transfers are running.</summary>
    /// <returns>A task that completes when a transfer may start.</returns>
    internal async Task WaitForFreeSlotAsync()
    {
        while (running.Count >= maxRunning)
        {
            await WaitForOneToEndAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Counts a started transfer as running until it ends.</summary>
    /// <param name="transfer">The running transfer.</param>
    internal void Add(Task transfer)
    {
        running.Add(transfer);
        started.Add(transfer);
    }

    /// <summary>Waits for every started transfer to end.</summary>
    /// <returns>A task that completes when they have, faulted if one did.</returns>
    internal async Task WhenAllEndedAsync()
    {
        while (running.Count > 0)
        {
            await WaitForOneToEndAsync().ConfigureAwait(false);
        }

        await Task.WhenAll(started).ConfigureAwait(false);
    }

    /// <summary>Runs <see cref="BeforeEachWait" />, then waits for a running transfer to end and stops counting it.</summary>
    private async Task WaitForOneToEndAsync()
    {
        BeforeEachWait();
        running.Remove(await Task.WhenAny(running).ConfigureAwait(false));
    }
}
