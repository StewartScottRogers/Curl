using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Imap;

/// <summary>
/// Holds a connect-only session's connection: disposing the lease closes the session and the
/// connection unless <see cref="Release" /> handed the connection to the caller first (BL-2706).
/// </summary>
/// <param name="connection">The connection the session started on.</param>
/// <param name="session">The session talking on it.</param>
internal sealed class ImapConnectOnlyLease(IConnection connection, ImapSession session) : IAsyncDisposable
{
    private bool released;

    /// <summary>Hands over the connection the session now talks on, which disposing the lease then leaves open.</summary>
    /// <returns>The connection, which the caller now owns.</returns>
    public IConnection Release()
    {
        released = true;
        return session.TakeConnection();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!released)
        {
            await session.DisposeAsync().ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
