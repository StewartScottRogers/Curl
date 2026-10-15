using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Http2Server;

/// <summary>
/// The client's end of an in-memory connection to the h2c server: reads and writes go to the
/// client's half of an <see cref="InMemoryDuplexStream"/> pair, and disposing it ends the
/// stream the server reads.
/// </summary>
/// <param name="stream">The client's half of the pair.</param>
internal sealed class Http2ServerClientConnection(Stream stream) : IConnection
{
    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        stream.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        stream.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => stream.DisposeAsync();
}
