namespace Curl.Networking;

// The DoH connections kept for later queries, and the numbers -v gives each one opened (ADR-1436).
public sealed partial class DohDnsResolver : IAsyncDisposable
{
    private readonly object _connectionsLock = new();

    // The open connections no query holds.
    private readonly List<DohConnection> _keptConnections = [];

    // The number of the last DoH connection opened: curl numbers them after the transfer's own #0, so
    // the first is #1 (ADR-0380).
    private long _lastConnectionNumber;

    private bool _isDisposed;

    /// <summary>
    /// Closes the DoH connections kept for later queries; a connection a query still holds closes as
    /// that query ends.
    /// </summary>
    /// <returns>A task that completes once the kept connections are closed.</returns>
    public async ValueTask DisposeAsync()
    {
        DohConnection[] kept;
        lock (_connectionsLock)
        {
            _isDisposed = true;
            kept = [.. _keptConnections];
            _keptConnections.Clear();
        }

        foreach (var connection in kept)
        {
            await connection.Transport.DisposeAsync().ConfigureAwait(false);
        }
    }

    // Takes the kept connections the queries can go on: one HTTP/2 connection, which carries them all,
    // else up to one HTTP/1.x connection each, the lowest numbered first.
    private List<DohConnection> TakeKeptConnections(int queryCount)
    {
        lock (_connectionsLock)
        {
            _keptConnections.Sort((first, second) => first.Number.CompareTo(second.Number));
            var shared = _keptConnections.Find(connection => connection.IsHttp2);
            List<DohConnection> taken = shared is null ? [.. _keptConnections.Take(queryCount)] : [shared];
            taken.ForEach(connection => _keptConnections.Remove(connection));
            return taken;
        }
    }

    private ValueTask KeepOrCloseAsync(DohConnection connection, bool isUsable)
    {
        lock (_connectionsLock)
        {
            if (isUsable && !_isDisposed)
            {
                _keptConnections.Add(connection);
                return ValueTask.CompletedTask;
            }
        }

        return connection.Transport.DisposeAsync();
    }

    // Takes the run's next %{conn_id} for a connection about to be opened and returns the number -v
    // gives it: that id, or the next DoH number when the id is behind it or none is taken.
    private long NextConnectionNumber()
    {
        lock (_connectionsLock)
        {
            _lastConnectionNumber = Math.Max(_lastConnectionNumber + 1, TakeConnectionId?.Invoke() ?? 0);
            return _lastConnectionNumber;
        }
    }

    // The -v line curl writes as a sub-transfer takes a connection already open.
    private void ReportReuse() =>
        SubTransferEvents.ReportInfo($"Reusing existing {_dohUrl.Scheme}: connection with host {_dohServer.Host}");

    // Holds a connection for one exchange: disposing it keeps the connection when the exchange left it
    // usable, else closes it, as when the exchange threw.
    private sealed class ConnectionLease(DohDnsResolver resolver, DohConnection connection) : IAsyncDisposable
    {
        public bool IsUsable { get; set; }

        public ValueTask DisposeAsync() => resolver.KeepOrCloseAsync(connection, IsUsable);
    }
}
