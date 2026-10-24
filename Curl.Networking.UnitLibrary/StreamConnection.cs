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
    /// Gets the check run before each write: whether the peer had already sent bytes, or its FIN,
    /// that the stream has not read yet. Production asks the socket under a
    /// <see cref="NetworkStream" />; tests set it (ADR-0708, BL-2821, BL-3304).
    /// </summary>
    internal Func<Stream, bool> HasUnreadBytesOrEndBeforeWrite { get; init; } = HasSocketUnderUnreadBytesOrEnd;

    private bool _hadUnreadBytesOrEndAtLastWrite;

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
    private static bool HasSocketUnderUnreadBytesOrEnd(Stream stream) =>
        stream is NetworkStream network && (network.Socket.Available > 0 || HasUnreadEnd(network.Socket));

    /// <summary>
    /// Peeks one byte from a readable socket with nothing to read: zero means the peer's FIN; a
    /// reset fails the peek and is not an end (ADR-0419 keeps rewording it).
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    private static bool HasUnreadEnd(Socket socket)
    {
        try
        {
            return socket.Poll(0, SelectMode.SelectRead) && socket.Receive(new byte[1], SocketFlags.Peek) == 0;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The aborted-to-reset rewrite (<see cref="ReportsAbortedReadAsReset" />) applies only when the
    /// last write found no unread bytes and no unread FIN: a peer that resets in answer to the
    /// request, or right after accepting (ADR-0419). A peer whose bytes were still unread at the
    /// write, as a proxy leaving bytes after its CONNECT reply and closing the tunnel (upstream
    /// test1802), keeps the abort, as curl's Schannel build prints it (ADR-0708); so does a peer
    /// whose FIN was unread at the write, as a server that closed a kept connection curl then reuses
    /// (BL-3304, ADR-0796): Windows answers the reset of a connection it had a FIN on with
    /// WSAECONNABORTED however soon the receive runs.
    /// </remarks>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            return await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException failure) when (ReportsAbortedReadAsReset && !_hadUnreadBytesOrEndAtLastWrite && IsConnectionAborted(failure))
        {
            throw new IOException(failure.Message, new SocketException((int)SocketError.ConnectionReset));
        }
    }

    private static bool IsConnectionAborted(IOException failure) =>
        failure.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionAborted };

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        _hadUnreadBytesOrEndAtLastWrite = HasUnreadBytesOrEndBeforeWrite(_stream);
        return _stream.WriteAsync(buffer, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        new(_stream.FlushAsync(cancellationToken));

    /// <summary>
    /// Gets the send-side shutdown <see cref="ShutDownSendingAsync" /> runs on the stream.
    /// Production discards the bytes the socket under a <see cref="NetworkStream" /> has received
    /// but not read, then shuts its send side down; tests set it (BL-3179).
    /// </summary>
    internal Action<Stream> ShutDownSendSide { get; init; } = DiscardUnreadAndShutDownSocketUnder;

    /// <inheritdoc />
    /// <remarks>
    /// Discarding what is already received before the FIN keeps the close that follows from
    /// sending a reset over unread bytes (ADR-0770).
    /// </remarks>
    public ValueTask ShutDownSendingAsync(CancellationToken cancellationToken)
    {
        ShutDownSendSide(_stream);
        return ValueTask.CompletedTask;
    }

    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    private static void DiscardUnreadAndShutDownSocketUnder(Stream stream)
    {
        if (stream is not NetworkStream network)
        {
            return;
        }

        try
        {
            byte[] discard = new byte[4096];
            while (network.Socket.Available > 0 && network.Socket.Receive(discard) > 0)
            {
            }

            network.Socket.Shutdown(SocketShutdown.Send);
        }
        catch (SocketException)
        {
            // The peer has already gone: there is no send side left to end.
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _stream.DisposeAsync();
}
