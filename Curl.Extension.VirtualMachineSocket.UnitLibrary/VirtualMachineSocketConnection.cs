using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.VirtualMachineSocket;

/// <summary>A transfer's connection over a connected vsock or Hyper-V stream socket: the bytes pass through unchanged.</summary>
/// <param name="socket">The connected socket's stream, which the connection owns.</param>
internal sealed class VirtualMachineSocketConnection(Stream socket) : IConnection
{
    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        socket.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        socket.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        new(socket.FlushAsync(cancellationToken));

    /// <inheritdoc />
    public ValueTask DisposeAsync() => socket.DisposeAsync();
}
