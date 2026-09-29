namespace Curl.Console;

/// <summary>
/// Keeps at most <c>--parallel-max</c> transfers running under <c>-Z</c>: the runner waits for a free
/// slot before it starts the next transfer in command-line order, and a slot frees as any running
/// transfer ends (ADR-0127, decision 1).
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

    /// <summary>Waits until fewer than the maximum transfers are running.</summary>
    /// <returns>A task that completes when a transfer may start.</returns>
    internal async Task WaitForFreeSlotAsync()
    {
        while (running.Count >= maxRunning)
        {
            running.Remove(await Task.WhenAny(running).ConfigureAwait(false));
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
    internal Task WhenAllEndedAsync() => Task.WhenAll(started);
}
