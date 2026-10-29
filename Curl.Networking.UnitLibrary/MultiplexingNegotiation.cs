namespace Curl.Networking;

/// <summary>
/// A connection a <see cref="PoolingConnector" /> is opening, or has opened, that may yet turn
/// out to carry several transfers at once: while it is pending, another transfer to the same
/// <see cref="ConnectionPoolKey" /> waits for it rather than opening a connection of its own, as
/// curl 8.21.0 does with <c>CURLOPT_PIPEWAIT</c> under <c>-Z</c> (measured, BL-717 Notes).
/// Once it is decided the waiting transfers are released one at a time, in the order they began
/// to wait, so they open their streams in command-line order (CGR-279).
/// </summary>
/// <param name="key">The key of the connection.</param>
internal sealed class MultiplexingNegotiation(ConnectionPoolKey key)
{
    private readonly Lock _gate = new();
    private readonly Queue<TaskCompletionSource> _waiters = new();
    private bool _decided;

    /// <summary>Gets the key of the connection.</summary>
    public ConnectionPoolKey Key { get; } = key;

    /// <summary>
    /// Waits until it is known whether the connection multiplexes (its connect failed or gave a
    /// protocol that does not, a transfer handed it a session, or its last lease ended) and it is
    /// this waiter's turn. The caller then calls <see cref="ReleaseNext" /> once it has acted.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when it is this waiter's turn.</returns>
    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource turn;
        lock (_gate)
        {
            if (_decided && _waiters.Count == 0)
            {
                return;
            }

            turn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Enqueue(turn);
        }

        await using var registration = cancellationToken.Register(static state => ((TaskCompletionSource)state!).TrySetCanceled(), turn).ConfigureAwait(false);
        await turn.Task.ConfigureAwait(false);
    }

    /// <summary>Marks the connection decided and releases the first waiter; a second call does nothing.</summary>
    public void Decide()
    {
        lock (_gate)
        {
            _decided = true;
        }

        ReleaseNext();
    }

    /// <summary>Releases the next waiter, if any, once the one released before it has acted.</summary>
    public void ReleaseNext()
    {
        lock (_gate)
        {
            if (!_decided)
            {
                return;
            }

            while (_waiters.TryDequeue(out var next))
            {
                if (next.TrySetResult())
                {
                    return;
                }
            }
        }
    }
}
