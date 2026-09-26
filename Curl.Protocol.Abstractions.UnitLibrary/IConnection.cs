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
}
