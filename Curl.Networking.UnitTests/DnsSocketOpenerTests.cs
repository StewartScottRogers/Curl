using System.Net;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="DnsSocketOpener" />'s UDP side, which opens a loopback socket and sends nothing, and
/// that its TCP stream owns its socket (a loopback connect that sends nothing, so a unit test).
/// </summary>
[TestClass]
public sealed class DnsSocketOpenerTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task OpenDatagramChannel_BindsTheLocalAddressItIsGiven()
    {
        var server = new IPEndPoint(IPAddress.Loopback, 53);
        Diagnostics.Arrange("server", server);
        Diagnostics.Arrange("local address", IPAddress.Loopback);

        var channel = (UdpDatagramChannel)new DnsSocketOpener().OpenDatagramChannel(server, IPAddress.Loopback);
        await using (channel)
        {
            var localAddress = ((IPEndPoint)channel.LocalEndPoint).Address;
            Diagnostics.Act("bound local address", localAddress);
            Diagnostics.Act("server end point", channel.ServerEndPoint);
            Diagnostics.Assert("bound local address", IPAddress.Loopback, localAddress);
            Diagnostics.Assert("server end point", server, channel.ServerEndPoint);
            Assert.AreEqual(IPAddress.Loopback, ((IPEndPoint)channel.LocalEndPoint).Address);
            Assert.AreEqual(server, channel.ServerEndPoint);
        }
    }

    [TestMethod]
    public async Task ConnectStreamAsync_StreamDisposed_ClosesTheSocketSoTheServerSeesEndOfStream()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var server = (IPEndPoint)listener.LocalEndpoint;
            Diagnostics.Arrange("server", server);

            var stream = await new DnsSocketOpener().ConnectStreamAsync(server, IPAddress.Loopback, CancellationToken.None);
            using var accepted = await listener.AcceptTcpClientAsync(TestContext.CancellationToken);
            await stream.DisposeAsync();

            var buffer = new byte[1];
            using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
            readTimeout.CancelAfter(TimeSpan.FromSeconds(10));
            var read = await accepted.GetStream().ReadAsync(buffer, readTimeout.Token);
            Diagnostics.Act("bytes read after the client disposed its stream", read);
            Diagnostics.Assert("bytes read (0 = FIN received)", 0, read);
            Assert.AreEqual(0, read);
        }
        finally
        {
            listener.Stop();
        }
    }
}
