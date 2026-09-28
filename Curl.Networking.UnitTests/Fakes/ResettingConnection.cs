using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="IConnection" /> the server resets: every write is accepted and discarded,
/// and every read fails as <see cref="NetworkStream" /> reports a reset, an
/// <see cref="IOException" /> around a <see cref="SocketException" /> with the given error.
/// </summary>
/// <param name="socketError">The socket error each read fails with.</param>
public sealed class ResettingConnection(SocketError socketError) : IConnection
{
    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        ValueTask.FromException<int>(
            new IOException("Unable to read data from the transport connection.", new SocketException((int)socketError)));

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
