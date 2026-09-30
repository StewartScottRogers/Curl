namespace Curl.Networking;

/// <summary>
/// A connection a <see cref="PoolingConnector" /> is opening, or has opened, that may yet turn
/// out to carry several transfers at once: while it is pending, another transfer to the same
/// <see cref="ConnectionPoolKey" /> waits for it rather than opening a connection of its own, as
/// curl 8.21.0 does with <c>CURLOPT_PIPEWAIT</c> under <c>-Z</c> (measured, BL-717 Notes).
/// </summary>
/// <param name="key">The key of the connection.</param>
internal sealed class MultiplexingNegotiation(ConnectionPoolKey key)
{
    private readonly TaskCompletionSource decided = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the key of the connection.</summary>
    public ConnectionPoolKey Key { get; } = key;

    /// <summary>
    /// Gets a task that completes once it is known whether the connection multiplexes: its
    /// connect failed or gave a protocol that does not, a transfer handed it a session, or its
    /// last lease ended.
    /// </summary>
    public Task Decided => decided.Task;

    /// <summary>Completes <see cref="Decided" />; a second call does nothing.</summary>
    public void Decide() => decided.TrySetResult();
}
