using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// A plaintext <see cref="IConnection" /> over a <see cref="Stream" />. In production the
/// stream is the <see cref="System.Net.Sockets.NetworkStream" /> <see cref="TcpDialer" />
/// opens or <see cref="TcpPendingConnection" /> accepts; the connection owns the stream and
/// disposes it.
/// </summary>
/// <param name="stream">The stream to read from and write to.</param>
/// <param name="remoteEndPoint">The peer's address, or <see langword="null" /> when there is none.</param>
/// <param name="localEndPoint">
/// The local address of the socket under the stream, or <see langword="null" /> when there is none.
/// </param>
public sealed class StreamConnection(Stream stream, EndPoint? remoteEndPoint, EndPoint? localEndPoint = null) : IConnection
{
    private readonly Stream _stream = stream ?? throw new ArgumentNullException(nameof(stream));

    /// <summary>
    /// Gets <see langword="false" />: this connection is never encrypted. A secure
    /// connection comes from <see cref="ITlsProvider" />.
    /// </summary>
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint { get; } = remoteEndPoint;

    /// <inheritdoc />
    public EndPoint? LocalEndPoint { get; } = localEndPoint;

    /// <summary>
    /// Gets whether a read failing with <see cref="SocketError.ConnectionAborted" /> is reported as
    /// <see cref="SocketError.ConnectionReset" />: <see langword="true" /> on Windows, set by tests on any
    /// platform (ADR-0419, BL-1450).
    /// </summary>
    /// <remarks>
    /// Windows answers a receive issued more than a few milliseconds after a peer's RST with
    /// WSAECONNABORTED, and one issued sooner, or already waiting, with WSAECONNRESET (measured,
    /// BL-1450). curl's <c>recv</c> runs inside that window and reports the reset; Curl's first read
    /// after a request comes later, so it reports the RST as the reset it was.
    /// </remarks>
    internal bool ReportsAbortedReadAsReset { get; init; } = OperatingSystem.IsWindows();

    /// <summary>
    /// Gets the check run before each write: whether the peer had already sent bytes the stream has
    /// not read yet. Production asks the socket under a <see cref="NetworkStream" />; tests set it
    /// (ADR-0708, BL-2821).
    /// </summary>
    internal Func<Stream, bool> HasUnreadBytesBeforeWrite { get; init; } = HasSocketUnderUnreadBytes;

    private bool _hadUnreadBytesAtLastWrite;

    /// <inheritdoc />
    /// <remarks>
    /// Asks the socket under a <see cref="NetworkStream" />: readable with nothing to read means
    /// the peer's FIN, or a reset, has arrived. Any other stream has no socket to ask.
    /// </remarks>
    public bool HasPeerClosed => HasPeerClosedSocketUnder(_stream);

    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    private static bool HasPeerClosedSocketUnder(Stream stream) =>
        stream is NetworkStream network && network.Socket.Poll(0, SelectMode.SelectRead) && network.Socket.Available == 0;

    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    private static bool HasSocketUnderUnreadBytes(Stream stream) =>
        stream is NetworkStream network && network.Socket.Available > 0;

    /// <inheritdoc />
    /// <remarks>
    /// The aborted-to-reset rewrite (<see cref="ReportsAbortedReadAsReset" />) applies only when the
    /// last write found no unread bytes: a peer that resets in answer to the request, or right after
    /// accepting (ADR-0419). A peer whose bytes were still unread at the write, as a proxy leaving
    /// bytes after its CONNECT reply and closing the tunnel (upstream test1802), keeps the abort, as
    /// curl's Schannel build prints it (ADR-0708).
    /// </remarks>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            return await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException failure) when (ReportsAbortedReadAsReset && !_hadUnreadBytesAtLastWrite && IsConnectionAborted(failure))
        {
            throw new IOException(failure.Message, new SocketException((int)SocketError.ConnectionReset));
        }
    }

    private static bool IsConnectionAborted(IOException failure) =>
        failure.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionAborted };

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        _hadUnreadBytesAtLastWrite = HasUnreadBytesBeforeWrite(_stream);
        return _stream.WriteAsync(buffer, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        new(_stream.FlushAsync(cancellationToken));

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _stream.DisposeAsync();
}
