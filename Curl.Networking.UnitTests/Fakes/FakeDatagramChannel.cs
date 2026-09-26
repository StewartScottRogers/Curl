using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="IDatagramChannel" /> that only remembers the endpoint it was opened for.
/// </summary>
/// <param name="serverEndPoint">The endpoint the channel was opened for.</param>
public sealed class FakeDatagramChannel(IPEndPoint serverEndPoint) : IDatagramChannel
{
    /// <inheritdoc />
    public EndPoint ServerEndPoint { get; } = serverEndPoint;

    /// <inheritdoc />
    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
