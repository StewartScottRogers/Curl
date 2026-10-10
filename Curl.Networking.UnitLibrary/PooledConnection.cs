using System.Net;
using System.Runtime.CompilerServices;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The <see cref="IConnection" /> a <see cref="PoolingConnector" /> hands out: it forwards
/// every read and write to the underlying connection, and on dispose returns that connection
/// to the pool when <see cref="MarkReusable" /> was called, or closes it otherwise (ADR-0050),
/// noting on the way whether a read found the server's close (ADR-0112).
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
    private bool _tlsClearingAsked;
    private bool _wasSharedAtEnd;

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
    public EndPoint? LocalEndPoint => _underlying.Connection.LocalEndPoint;

    /// <inheritdoc />
    /// <remarks>
    /// A read into a non-empty buffer that returns zero records that the server closed the
    /// connection, so the pool reports it dead rather than reusing it (ADR-0112); so does a
    /// read that finds a TLS connection ended without <c>close_notify</c> (ADR-0221).
    /// </remarks>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int read;
        try
        {
            read = await _underlying.Connection.ReadAsync(buffer, cancellationToken);
        }
        catch (MissingCloseNotifyException)
        {
            _underlying.HasReadPeerClose = true;
            throw;
        }

        if (read == 0 && !buffer.IsEmpty)
        {
            _underlying.HasReadPeerClose = true;
        }

        return read;
    }

    /// <inheritdoc />
    /// <remarks>
    /// True once a read found the server's close (ADR-0112), or when the underlying connection
    /// reports it now (ADR-0467).
    /// </remarks>
    public bool HasPeerClosed => _underlying.HasReadPeerClose || _underlying.Connection.HasPeerClosed;

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

    /// <inheritdoc />
    /// <remarks>
    /// The session belongs to the underlying connection, so a later lease of it gives the
    /// same one.
    /// </remarks>
    public IConnectionSession? Session => _underlying.Session;

    /// <inheritdoc />
    /// <remarks>
    /// Always holds it: the session stays with the underlying connection while it is pooled,
    /// and is shut down whenever the connection closes - on this dispose, or later by the
    /// pool (BL-817).
    /// </remarks>
    public bool TryHoldSession(IConnectionSession session)
    {
        _underlying.Session = session;
        _pool.SessionHeld(_underlying);
        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Asks the underlying connection. Once asked, the connection is never pooled again,
    /// whether TLS was cleared or not: its pool key names a TLS connection it no longer is.
    /// </remarks>
    public ValueTask<IConnection?> ClearTlsAsync(bool sendCloseNotifyFirst, CancellationToken cancellationToken)
    {
        _tlsClearingAsked = true;
        return _underlying.Connection.ClearTlsAsync(sendCloseNotifyFirst, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// True while the pool has handed the underlying connection to another transfer as well,
    /// because its session multiplexes (BL-717); once this lease is disposed, whether another
    /// transfer still held it when this one handed it back, so of transfers ending together
    /// only the last reports it left intact.
    /// </remarks>
    public bool IsSharedWithAnotherTransfer => _isDisposed ? _wasSharedAtEnd : _pool.IsShared(_underlying);

    /// <summary>
    /// Ends this lease. While another transfer shares the underlying connection it stays open
    /// for that one (BL-717); after the last lease it returns to the pool when every lease
    /// marked it reusable, its target is pooled and TLS clearing was never asked of it, and
    /// closes otherwise, shutting down any
    /// session it holds first. A second call does nothing.
    /// </summary>
    /// <returns>A task that completes when the connection is pooled or closed, or at once while it is still shared.</returns>
    public ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return ValueTask.CompletedTask;
        }

        _isDisposed = true;
        _wasSharedAtEnd = _pool.EndLease(_underlying, _isReusable && !_tlsClearingAsked);

        return _wasSharedAtEnd
            ? ValueTask.CompletedTask
            : _pool.HandBackAsync(_underlying, _events);
    }
}
