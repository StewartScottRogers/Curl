using System.Net;

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

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        _stream.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        _stream.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        new(_stream.FlushAsync(cancellationToken));

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _stream.DisposeAsync();
}
