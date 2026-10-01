using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// One connection a <see cref="PoolingConnector" /> opened, with what it must report again
/// when the connection is reused: its key, curl's number for it, its local end point and the
/// server's certificates, the Unix domain socket it was dialled through and the <c>--connect-to</c>
/// destination it was dialled to.
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
/// <param name="MappedHost">
/// The host a <c>--connect-to</c> mapping or alt-svc alternative sent it to, or
/// <see langword="null" /> when it went to the target's own host (BL-975).
/// </param>
/// <param name="MappedPort">The port beside <paramref name="MappedHost" />; <c>0</c> when there is none.</param>
internal sealed record PoolEntry(
    ConnectionPoolKey? Key,
    IConnection Connection,
    long ConnectionNumber,
    IPEndPoint? LocalEndPoint,
    IReadOnlyList<ReadOnlyMemory<byte>> PeerCertificates,
    string? UnixSocketPath,
    string? MappedHost,
    int MappedPort)
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
    /// Gets or sets how many transfers hold a lease of the connection: one, or more while its
    /// <see cref="Session" /> multiplexes them (BL-717). Read and written under the pool's lock.
    /// </summary>
    public int LeaseCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a lease ended without marking the connection
    /// reusable: no new transfer shares it, and it closes when its last lease ends.
    /// </summary>
    public bool IsShareBarred { get; set; }

    /// <summary>
    /// Gets or sets what transfers waiting to learn whether the connection multiplexes wait on,
    /// or <see langword="null" /> once that is known (BL-717).
    /// </summary>
    public MultiplexingNegotiation? Negotiation { get; set; }

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
