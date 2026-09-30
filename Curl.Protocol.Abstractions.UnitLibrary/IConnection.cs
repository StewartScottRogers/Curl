using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// A bidirectional byte stream to a remote peer: the seam that keeps protocol
/// implementations off the network during tests.
/// </summary>
/// <remarks>
/// Protocol handlers accept this interface and never construct a
/// <see cref="System.Net.Sockets.Socket" />, <c>SslStream</c> or <c>HttpClient</c>
/// themselves. A unit test supplies an implementation that replays recorded bytes,
/// so wire-level behaviour is asserted without a server; production supplies one
/// backed by a real socket, wrapped in TLS when the scheme is secure.
/// </remarks>
public interface IConnection : IAsyncDisposable
{
    /// <summary>
    /// Gets a value indicating whether traffic on this connection is encrypted.
    /// </summary>
    bool IsSecure { get; }

    /// <summary>
    /// Gets the remote endpoint, or <see langword="null" /> when the implementation
    /// has no meaningful address, as is the case for a fake used in a test.
    /// </summary>
    EndPoint? RemoteEndPoint { get; }

    /// <summary>
    /// Gets the local endpoint of the connection, or <see langword="null" />, the default,
    /// when the implementation has no meaningful address.
    /// </summary>
    /// <remarks>
    /// FTP's <c>-P -</c> announces this address for its active-mode data connection. Only
    /// the TCP connection overrides it, so no other implementation or test fake has to
    /// (ADR-0102).
    /// </remarks>
    EndPoint? LocalEndPoint => null;

    /// <summary>
    /// Reads up to <paramref name="buffer" /> bytes from the peer.
    /// </summary>
    /// <param name="buffer">The destination for the bytes read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The number of bytes read, or zero once the peer has closed its side.
    /// </returns>
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);

    /// <summary>
    /// Writes <paramref name="buffer" /> to the peer.
    /// </summary>
    /// <param name="buffer">The bytes to send.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the bytes have been handed to the transport.</returns>
    ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);

    /// <summary>
    /// Flushes any buffered outbound bytes.
    /// </summary>
    /// <param name="cancellationToken">Cancels the flush.</param>
    /// <returns>A task that completes when the buffer has drained.</returns>
    ValueTask FlushAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Marks the connection as able to carry another request once this one is disposed:
    /// the response has been read to its end and the protocol says the connection persists
    /// (ADR-0050).
    /// </summary>
    /// <remarks>
    /// The caller still disposes the connection. A pooled connection returns to its pool on
    /// dispose when it was marked, and closes otherwise; every other implementation ignores
    /// the mark, which is what the default does, so forgetting to call this closes the
    /// connection rather than wrongly reusing it.
    /// </remarks>
    void MarkReusable()
    {
    }

    /// <summary>
    /// Gets the protocol session a transfer that earlier had this connection left with it
    /// (<see cref="TryHoldSession" />), or <see langword="null" />, the default, when there is
    /// none.
    /// </summary>
    IConnectionSession? Session => null;

    /// <summary>
    /// Asks the connection to hold <paramref name="session" />: to keep it for the next transfer
    /// that has the connection, and to call its <see cref="IConnectionSession.ShutDownAsync" />
    /// before the connection closes (BL-817).
    /// </summary>
    /// <param name="session">The protocol's session on this connection.</param>
    /// <returns>
    /// <see langword="true" /> when the connection holds it; <see langword="false" />, the
    /// default, when it holds none, and the caller shuts the session down itself before
    /// disposing the connection. Only a pooled connection, which outlives a transfer, holds one.
    /// </returns>
    bool TryHoldSession(IConnectionSession session) => false;

    /// <summary>
    /// Shuts TLS down on this connection and hands back the plaintext connection it ran over,
    /// as FTP's <c>CCC</c> clears the control connection (BL-636, ADR-0280).
    /// </summary>
    /// <param name="sendCloseNotifyFirst">
    /// <see langword="true" /> to send <c>close_notify</c> and then read the server's, as
    /// <c>--ftp-ssl-ccc-mode active</c> does; <see langword="false" /> to read the server's
    /// <c>close_notify</c> and send none, as <c>passive</c> does.
    /// </param>
    /// <param name="cancellationToken">Cancels the shutdown.</param>
    /// <returns>
    /// The plaintext connection to carry on over, which is still disposed with this one; or
    /// <see langword="null" />, the default, when TLS could not be cleared: the connection is
    /// not TLS, the TLS build this one matches fails the shutdown as curl's Schannel build
    /// does, or the server ended the connection or sent data instead of <c>close_notify</c>.
    /// </returns>
    ValueTask<IConnection?> ClearTlsAsync(bool sendCloseNotifyFirst, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IConnection?>(null);
}
