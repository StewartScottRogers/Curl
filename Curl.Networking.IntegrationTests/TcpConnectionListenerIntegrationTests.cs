using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

[TestClass]
public sealed class TcpConnectionListenerIntegrationTests
{
    private static readonly ListenTarget AnyLoopbackPort = new(IPAddress.Loopback, 0, 0);

    [TestMethod]
    [TestCategory("Integration")]
    public async Task ListenAsync_OnLoopbackPortZero_AcceptsTheClientThatConnectsAndKeepsItOpenAfterDispose()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var listened = await new TcpConnectionListener().ListenAsync(AnyLoopbackPort, cancellation.Token);
        var pending = listened.PendingConnection!;
        var listening = (IPEndPoint)pending.LocalEndPoint;
        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        await client.ConnectAsync(listening, cancellation.Token);
        var accepted = await pending.AcceptAsync(cancellation.Token);
        await pending.DisposeAsync();

        Assert.AreEqual(CurlExitCode.Ok, accepted.ExitCode);
        await using var connection = accepted.Connection!;
        Assert.IsFalse(connection.IsSecure);
        Assert.AreEqual(listening, connection.LocalEndPoint);
        Assert.AreEqual(listening, accepted.LocalEndPoint);
        Assert.AreEqual(client.LocalEndPoint, connection.RemoteEndPoint);
        await connection.WriteAsync(new byte[] { 42 }, cancellation.Token);
        await connection.FlushAsync(cancellation.Token);
        var received = new byte[1];
        await client.ReceiveAsync(received, cancellation.Token);
        Assert.AreEqual(42, received[0]);
    }
}
