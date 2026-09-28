namespace Curl.Console;

/// <summary>
/// The run-wide gate every write to standard output and standard error goes through, so that two
/// transfers running at once under <c>-Z</c> never split each other's writes, and a finished
/// transfer's failure lines and <c>-w</c> text are written together (ADR-0127, decision 2). A flow
/// that holds the gate writes straight through it: the gate is re-entered, not waited on.
/// </summary>
internal sealed class WriteGate
{
    /// <summary>The one permit: whoever holds it writes.</summary>
    private readonly SemaphoreSlim permit = new(1, 1);

    /// <summary>Whether the current asynchronous flow holds <see cref="permit" />.</summary>
    private readonly AsyncLocal<bool> heldByThisFlow = new();

    /// <summary>
    /// Runs <paramref name="write" /> holding the gate, waiting for it unless this flow holds it already.
    /// </summary>
    /// <param name="write">The writes.</param>
    /// <returns>A task that completes when <paramref name="write" /> has and the gate is let go.</returns>
    internal async Task RunExclusiveAsync(Func<Task> write)
    {
        if (heldByThisFlow.Value)
        {
            await write().ConfigureAwait(false);
            return;
        }

        await permit.WaitAsync().ConfigureAwait(false);
        try
        {
            heldByThisFlow.Value = true;
            await write().ConfigureAwait(false);
        }
        finally
        {
            heldByThisFlow.Value = false;
            permit.Release();
        }
    }

    /// <summary>
    /// Runs <paramref name="write" /> holding the gate, blocking until it is free unless this flow
    /// holds it already; for the synchronous writes a handler's progress reports make.
    /// </summary>
    /// <param name="write">The writes.</param>
    internal void RunExclusive(Action write)
    {
        if (heldByThisFlow.Value)
        {
            write();
            return;
        }

        permit.Wait();
        try
        {
            heldByThisFlow.Value = true;
            write();
        }
        finally
        {
            heldByThisFlow.Value = false;
            permit.Release();
        }
    }

    /// <summary>Wraps <paramref name="inner" /> so that every write and flush goes through this gate.</summary>
    /// <param name="inner">The stream written to.</param>
    /// <returns>The gated stream.</returns>
    internal Stream Guard(Stream inner) => new WriteGateStream(inner, this);
}
