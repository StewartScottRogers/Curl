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

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            return await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException failure) when (ReportsAbortedReadAsReset && IsConnectionAborted(failure))
        {
            throw new IOException(failure.Message, new SocketException((int)SocketError.ConnectionReset));
        }
    }

    private static bool IsConnectionAborted(IOException failure) =>
        failure.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionAborted };

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        _stream.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        new(_stream.FlushAsync(cancellationToken));

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _stream.DisposeAsync();
}
