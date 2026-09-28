using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins that the connection <see cref="TcpConnector" /> returns over the production
/// <see cref="TcpDialer" /> reports its socket's local end point, which FTP's <c>-P -</c>
/// announces (ADR-0102, BL-456). It dials a loopback <see cref="TcpConnectionListener" />,
/// so it is an <c>Integration</c> test, as <see cref="TcpDialerTests" /> is.
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task ConnectAsync_OverTheTcpDialer_ReturnsAConnectionThatReportsItsLocalEndPoint()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var listened = await new TcpConnectionListener().ListenAsync(
            new ListenTarget(IPAddress.Loopback, 0, 0), cancellation.Token);
        await using var pending = listened.PendingConnection!;
        var port = ((IPEndPoint)pending.LocalEndPoint).Port;
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new TcpDialer(), new FakeTlsProvider(), TimeProvider.System);

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", port, UseTls: false), cancellation.Token);
        await using var connection = result.Connection!;
        var accepted = await pending.AcceptAsync(cancellation.Token);
        await using var serverSide = accepted.Connection!;

        Assert.IsNotNull(connection.LocalEndPoint);
        Assert.AreEqual(result.LocalEndPoint, connection.LocalEndPoint);
        Assert.AreEqual(serverSide.RemoteEndPoint, connection.LocalEndPoint);
    }
}
