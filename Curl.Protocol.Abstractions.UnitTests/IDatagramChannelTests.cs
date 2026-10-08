using System.Net;
using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the default member of <see cref="IDatagramChannel" />, which lets TFTP's channel
/// and its fakes leave it out.
/// </summary>
[TestClass]
public sealed class IDatagramChannelTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void LocalEndPoint_WhenNotOverridden_ReturnsNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IDatagramChannel channel = new MinimalChannel();
        diagnostics.Arrange("channel", nameof(MinimalChannel));

        var localEndPoint = channel.LocalEndPoint;

        diagnostics.Act("local end point", localEndPoint?.ToString() ?? "null");
        diagnostics.Assert("local end point is null", true, localEndPoint is null);
        Assert.IsNull(localEndPoint);
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
