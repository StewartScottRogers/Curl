using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the default member of <see cref="IDatagramChannel" />, which lets TFTP's channel
/// and its fakes leave it out.
/// </summary>
[TestClass]
public sealed class IDatagramChannelTests
{
    [TestMethod]
    public void LocalEndPoint_WhenNotOverridden_ReturnsNull()
    {
        IDatagramChannel channel = new MinimalChannel();

        Assert.IsNull(channel.LocalEndPoint);
    }

    private sealed class MinimalChannel : IDatagramChannel
    {
        public EndPoint ServerEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 69);

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
