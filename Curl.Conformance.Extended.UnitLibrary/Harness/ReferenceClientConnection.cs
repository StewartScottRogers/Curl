using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Extended.Harness;

/// <summary>curl's end of an in-memory connection to a reference server.</summary>
internal sealed class ReferenceClientConnection(Stream clientEnd, IPEndPoint remoteEndPoint) : IConnection
{
    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint => remoteEndPoint;

    public EndPoint? LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        clientEnd.ReadAsync(buffer, cancellationToken);

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        clientEnd.WriteAsync(buffer, cancellationToken);

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => clientEnd.DisposeAsync();
}
