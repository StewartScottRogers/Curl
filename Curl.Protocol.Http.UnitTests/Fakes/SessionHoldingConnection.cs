using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IConnection" /> that behaves as a pooled connection does towards the
/// handler (BL-817): it holds the session a transfer hands it, keeps it and itself open when
/// the transfer marked it reusable, and otherwise shuts the session down and closes. Reads
/// and writes go to a <see cref="ScriptedConnection" />, so the same instance can be handed
/// out for one transfer after another.
/// </summary>
/// <param name="inner">The connection the bytes go through.</param>
public sealed class SessionHoldingConnection(ScriptedConnection inner) : IConnection
{
    private bool isMarkedReusable;

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <inheritdoc />
    public IConnectionSession? Session { get; private set; }

    /// <summary>Gets how many transfers returned the connection marked reusable.</summary>
    public int ReturnedReusableCount { get; private set; }

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => inner.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public void MarkReusable() => isMarkedReusable = true;

    /// <inheritdoc />
    public bool TryHoldSession(IConnectionSession session)
    {
        Session = session;
        return true;
    }

    /// <summary>
    /// Keeps the connection when the transfer marked it reusable, as the pool keeps it idle;
    /// closes it otherwise.
    /// </summary>
    /// <returns>A task that completes when the connection is kept or closed.</returns>
    public async ValueTask DisposeAsync()
    {
        if (isMarkedReusable)
        {
            isMarkedReusable = false;
            ReturnedReusableCount++;
            return;
        }

        await CloseAsync();
    }

    /// <summary>
    /// Closes the connection as the pool closes an idle one: shuts its session down, then
    /// disposes the scripted connection.
    /// </summary>
    /// <returns>A task that completes when the connection is closed.</returns>
    public async ValueTask CloseAsync()
    {
        if (Session is not null)
        {
            await Session.ShutDownAsync(CancellationToken.None);
        }

        await inner.DisposeAsync();
    }
}
