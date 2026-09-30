using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// One connection a <see cref="PoolingConnector" /> opened, with what it must report again
/// when the connection is reused: its key, curl's number for it, its local end point and the
/// server's certificates and the Unix domain socket it was dialled through.
/// </summary>
/// <param name="Key">The key it is pooled under, or <see langword="null" /> when it is never pooled.</param>
/// <param name="Connection">The connection the inner connector opened.</param>
/// <param name="ConnectionNumber">curl's number for it, counted from <c>0</c>.</param>
/// <param name="LocalEndPoint">The local address and port it was opened from, or <see langword="null" />.</param>
/// <param name="PeerCertificates">The DER of every certificate the server sent when it was opened.</param>
/// <param name="UnixSocketPath">
/// The path of the Unix domain socket it was dialled through, or <see langword="null" /> for a TCP
/// connection (BL-884).
/// </param>
internal sealed record PoolEntry(
    ConnectionPoolKey? Key,
    IConnection Connection,
    long ConnectionNumber,
    IPEndPoint? LocalEndPoint,
    IReadOnlyList<ReadOnlyMemory<byte>> PeerCertificates,
    string? UnixSocketPath)
{
    /// <summary>
    /// Gets or sets the timestamp, on the pool's <see cref="TimeProvider" />, at which the
    /// connection last went idle.
    /// </summary>
    public long IdleSince { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a read on the connection found the end of the
    /// server's stream: the server has closed it, so curl 8.21.0 reports it dead instead of
    /// reusing it (ADR-0112).
    /// </summary>
    public bool HasReadPeerClose { get; set; }

    /// <summary>
    /// Gets or sets the protocol session a transfer left with the connection, such as its
    /// HTTP/2 session, or <see langword="null" /> when none did (BL-817).
    /// </summary>
    public IConnectionSession? Session { get; set; }

    /// <summary>
    /// Closes the connection, first letting its <see cref="Session" />, if any, write what
    /// its protocol sends before a connection closes, such as HTTP/2's GOAWAY.
    /// </summary>
    /// <returns>A task that completes when the connection is closed.</returns>
    public async ValueTask CloseAsync()
    {
        if (Session is not null)
        {
            await Session.ShutDownAsync(CancellationToken.None);
        }

        await Connection.DisposeAsync();
    }
}
