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

    /// <summary>
    /// Gets the keys whose first connection turned out to carry one transfer at a time, for which
    /// no transfer waits for a connection being opened any more, as curl 8.22.0 opens the waiting
    /// transfers of a host side by side once its first connection showed HTTP/1.1 (CGR-171, ADR-0866).
    /// </summary>
    internal HashSet<ConnectionPoolKey> KeysKnownNotToMultiplex { get; } = [];

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
    /// Closes every idle connection, as libcurl's <c>CURLMNWC_CLEAR_CONNS</c> does when the network
    /// changed: they leave the cache before this returns its task, so no later transfer reuses one. The
    /// cache stays open, and a connection in use is kept when it is handed back, as before.
    /// </summary>
    /// <returns>A task that completes when every idle connection is closed.</returns>
    public async ValueTask CloseIdleAsync()
    {
        List<PoolEntry> closing;

        lock (Gate)
        {
            closing = [.. Idle];
            Idle.Clear();
        }

        await CloseAllAsync(closing);
    }

    /// <summary>
    /// Closes the oldest idle connections until no more than <paramref name="keep" /> connections, idle and
    /// in use, remain or none is idle, as libcurl's connection pool closes its oldest idle connection while
    /// it holds more than <c>CURLMOPT_MAXCONNECTS</c> (BL-2468). A connection in use is never closed; the ones
    /// closed leave the cache before this returns its task.
    /// </summary>
    /// <param name="keep">The most connections to keep; 0 or less closes every idle one.</param>
    /// <returns>A task that completes when every connection beyond the cap is closed.</returns>
    public async ValueTask CloseIdleBeyondAsync(long keep)
    {
        List<PoolEntry> closing;

        lock (Gate)
        {
            long excess = Idle.Count + Leased.Count - Math.Max(keep, 0);
            closing = Idle.GetRange(0, (int)Math.Clamp(excess, 0, Idle.Count));
            Idle.RemoveRange(0, closing.Count);
        }

        await CloseAllAsync(closing);
    }

    /// <summary>
    /// Gets or sets the owner each connection opened from here on, in this asynchronous flow only, belongs to, which
    /// <see cref="CloseIdleOwnedByAsync" /> closes; <see langword="null" />, the default, for none. A value set inside an
    /// <see langword="async" /> method reaches the connects it awaits and is undone when it returns, as an
    /// <see cref="AsyncLocal{T}" />'s is.
    /// </summary>
    public object? OpeningOwner
    {
        get => _openingOwner.Value;
        set => _openingOwner.Value = value;
    }

    private readonly AsyncLocal<object?> _openingOwner = new();

    /// <summary>
    /// Closes the idle connections opened while <see cref="OpeningOwner" /> was <paramref name="owner" />, as libcurl's
    /// <c>curl_easy_cleanup</c> closes the connections in the handle's own cache, an FTP one with <c>QUIT</c> (upstream
    /// test661, BL-2629). A connection in use is never closed; the ones closed leave the cache before this returns its task.
    /// </summary>
    /// <param name="owner">The owner whose connections close.</param>
    /// <returns>A task that completes when every one of them that was idle is closed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner" /> is <see langword="null" />.</exception>
    public async ValueTask CloseIdleOwnedByAsync(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        List<PoolEntry> closing;

        lock (Gate)
        {
            closing = Idle.FindAll(entry => ReferenceEquals(entry.Owner, owner));
            Idle.RemoveAll(closing.Contains);
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
