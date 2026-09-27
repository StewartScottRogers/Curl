using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="IConnection" /> that carries no bytes; tests compare it by reference.
/// </summary>
public sealed class FakeConnection : IConnection
{
    /// <summary>Gets or sets the value <see cref="IsSecure" /> reports.</summary>
    public bool IsSecure { get; init; }

    /// <summary>Gets or sets the value <see cref="RemoteEndPoint" /> reports; none by default.</summary>
    public EndPoint? RemoteEndPoint { get; init; }

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
