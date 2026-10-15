using System.Net;
using Curl.Quic;
using Curl.Tls;

namespace Curl.Conformance.Http3Server;

[TestClass]
public sealed class Http3ServerConnectorTests
{
    [TestMethod]
    public async Task OpenAsync_ClientInitial_IsAnsweredFromTheOpenedEndPoint()
    {
        var connector = new Http3ServerConnector(new RecordingBackendConnector(null), Http3TestClient.ServerSettings, SystemTlsRandomSource.Instance);
        using var client = Http3TestClient.Create();

        var opened = await connector.OpenAsync("127.0.0.2", 9010, CancellationToken.None);
        await using var channel = opened.Channel!;
        foreach (var datagram in client.Start())
        {
            await channel.SendAsync(datagram, channel.ServerEndPoint, CancellationToken.None);
        }

        var buffer = new byte[65536];
        var received = await channel.ReceiveAsync(buffer, CancellationToken.None);
        client.Receive(buffer.AsMemory(0, received.Length).ToArray());

        Assert.AreEqual(new IPEndPoint(IPAddress.Parse("127.0.0.2"), 9010), channel.ServerEndPoint);
        Assert.AreEqual(channel.ServerEndPoint, received.RemoteEndPoint);
        Assert.IsNull(client.Failure);
    }

    [TestMethod]
    public async Task OpenAsync_HostName_OpensToLoopback()
    {
        var connector = UpstreamHttp3ServerFactory.Create(new RecordingBackendConnector(null), Http3TestClient.ServerSettings);

        var opened = await connector.OpenAsync("localhost", 9010, CancellationToken.None);
        await using var channel = opened.Channel!;

        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 9010), channel.ServerEndPoint);
    }

    [TestMethod]
    public void Create_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamHttp3ServerFactory.Create(null!, Http3TestClient.ServerSettings));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamHttp3ServerFactory.Create(new RecordingBackendConnector(null), null!));
    }
}
