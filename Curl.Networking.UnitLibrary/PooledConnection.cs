using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The <see cref="IConnection" /> a <see cref="PoolingConnector" /> hands out: it forwards
/// every read and write to the underlying connection, and on dispose returns that connection
/// to the pool when <see cref="MarkReusable" /> was called, or closes it otherwise (ADR-0050).
/// </summary>
/// <remarks>
/// One instance serves one lease. A connection taken from the pool again is handed out in a
/// new instance, so a mark never outlives the transfer that made it.
/// </remarks>
public sealed class PooledConnection : IConnection
{
    private readonly PoolingConnector _pool;
    private readonly PoolEntry _underlying;
    private readonly ITransferEvents _events;
    private bool _isReusable;
    private bool _isDisposed;

    internal PooledConnection(PoolingConnector pool, PoolEntry underlying, ITransferEvents events)
    {
        _pool = pool;
        _underlying = underlying;
        _events = events;
    }

    /// <summary>
    /// Gets curl's number for the underlying connection, the <c>N</c> of <c>#N</c>.
    /// </summary>
    public long ConnectionNumber => _underlying.ConnectionNumber;

    /// <inheritdoc />
    public bool IsSecure => _underlying.Connection.IsSecure;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => _underlying.Connection.RemoteEndPoint;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        _underlying.Connection.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        _underlying.Connection.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        _underlying.Connection.FlushAsync(cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// A connection whose target had no <see cref="ConnectTarget.PoolScheme" /> ignores the
    /// mark and closes on dispose.
    /// </remarks>
    public void MarkReusable() => _isReusable = true;

    /// <summary>
    /// Returns the underlying connection to the pool when it was marked reusable and its
    /// target is pooled, and closes it otherwise. A second call does nothing.
    /// </summary>
    /// <returns>A task that completes when the connection is pooled or closed.</returns>
    public ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return ValueTask.CompletedTask;
        }

        _isDisposed = true;

        return _isReusable && _underlying.Key is not null
            ? _pool.ReturnAsync(_underlying, _events)
            : _underlying.Connection.DisposeAsync();
    }
}
