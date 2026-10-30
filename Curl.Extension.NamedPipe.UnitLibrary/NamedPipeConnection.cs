using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.NamedPipe;

/// <summary>A transfer's connection over a connected named pipe: the bytes pass through unchanged, as a byte-mode pipe carries them.</summary>
/// <param name="pipe">The connected pipe, which the connection owns.</param>
internal sealed class NamedPipeConnection(Stream pipe) : IConnection
{
    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        pipe.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        pipe.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        new(pipe.FlushAsync(cancellationToken));

    /// <inheritdoc />
    public ValueTask DisposeAsync() => pipe.DisposeAsync();
}
