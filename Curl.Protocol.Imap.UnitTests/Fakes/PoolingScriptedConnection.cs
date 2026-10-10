using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Imap.Fakes;

/// <summary>
/// A <see cref="ScriptedConnection" /> that behaves as the run's pooled connection does: it
/// holds a protocol session, and a dispose that was not preceded by
/// <see cref="IConnection.MarkReusable" /> closes it, shutting the held session down first, as
/// the connection cache does at exit.
/// </summary>
/// <param name="reads">The reads the server sends, one array per read.</param>
public sealed class PoolingScriptedConnection(params byte[][] reads) : IConnection
{
    private readonly ScriptedConnection inner = new(reads);

    private bool markedReusable;

    /// <summary>Gets the bytes written so far.</summary>
    public byte[] Sent => inner.Sent;

    /// <summary>Gets a value indicating whether the connection was closed.</summary>
    public bool IsClosed { get; private set; }

    /// <inheritdoc />
    public IConnectionSession? Session { get; private set; }

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <inheritdoc />
    public bool TryHoldSession(IConnectionSession session)
    {
        Session = session;
        return true;
    }

    /// <inheritdoc />
    public void MarkReusable() => markedReusable = true;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => inner.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    /// <summary>Closes the connection as the cache does at exit, shutting down a held session.</summary>
    /// <returns>A task that completes once closed.</returns>
    public async ValueTask CloseAsync()
    {
        if (Session is not null)
        {
            await Session.ShutDownAsync(CancellationToken.None);
        }

        IsClosed = true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (markedReusable)
        {
            markedReusable = false;
            return;
        }

        await CloseAsync();
    }
}
