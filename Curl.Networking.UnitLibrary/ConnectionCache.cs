using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The connections one run keeps for reuse, shared by every <see cref="PoolingConnector" />
/// over it: the idle ones, the ones in use, the ones being opened that may yet multiplex, and
/// the next connection number, as curl 8.21.0's one connection cache serves every
/// <c>-:</c>/<c>--next</c> option group of a command line (ADR-0285, BL-754).
/// </summary>
/// <param name="timeProvider">Measures how long each connection has been idle.</param>
public sealed class ConnectionCache(TimeProvider timeProvider) : IAsyncDisposable, IConnectionNumbers
{
    private long _nextConnectionNumber;

    /// <summary>Gets the lock every change to the cache is made under.</summary>
    internal Lock Gate { get; } = new();

    /// <summary>Gets the idle connections, oldest first.</summary>
    internal List<PoolEntry> Idle { get; } = [];

    /// <summary>Gets the connections with a key that transfers hold leases of.</summary>
    internal List<PoolEntry> Leased { get; } = [];

    /// <summary>Gets the connections being opened, or opened, that may yet multiplex.</summary>
    internal List<MultiplexingNegotiation> Negotiations { get; } = [];

    /// <summary>Gets the clock idle times are measured on.</summary>
    internal TimeProvider TimeProvider { get; } = timeProvider;

    /// <summary>
    /// Gets a value indicating whether <see cref="DisposeAsync" /> has run, after which a
    /// returned connection is closed instead of kept. Read and written under <see cref="Gate" />.
    /// </summary>
    internal bool IsDisposed { get; private set; }

    /// <summary>
    /// Closes every idle connection without reporting anything; a connection returned
    /// afterwards is closed instead of kept.
    /// </summary>
    /// <returns>A task that completes when every idle connection is closed.</returns>
    public async ValueTask DisposeAsync()
    {
        List<PoolEntry> closing;

        lock (Gate)
        {
            IsDisposed = true;
            closing = [.. Idle];
            Idle.Clear();
        }

        await CloseAllAsync(closing);
    }

    /// <summary>
    /// Takes the next connection number, from <c>0</c> across the whole run, whether a TCP
    /// connection, a TFTP channel or a <c>file://</c> transfer takes it (ADR-0109, BL-977).
    /// </summary>
    /// <returns>The number.</returns>
    public long NumberNextConnection() => Interlocked.Increment(ref _nextConnectionNumber) - 1;

    /// <summary>Closes each of <paramref name="entries" /> in turn.</summary>
    /// <param name="entries">The connections to close.</param>
    /// <returns>A task that completes when every one is closed.</returns>
    internal static async ValueTask CloseAllAsync(List<PoolEntry> entries)
    {
        foreach (var entry in entries)
        {
            await entry.CloseAsync();
        }
    }
}
