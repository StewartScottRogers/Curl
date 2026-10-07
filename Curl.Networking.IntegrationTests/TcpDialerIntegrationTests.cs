using System.Net;
using System.Net.Sockets;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

[TestClass]
public sealed class TcpDialerIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task DialUnixSocketAsync_ToAListeningSocket_ConnectsAndCarriesBytes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bl507-{Guid.NewGuid():N}.sock");
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path));
        listener.Listen(1);
        try
        {
            var accepting = listener.AcceptAsync();

            await using var connection = await new TcpDialer().DialUnixSocketAsync(new UnixSocketAddress(path, IsAbstract: false), CancellationToken.None);
            using var accepted = await accepting;
            await connection.WriteAsync("hi"u8.ToArray(), CancellationToken.None);
            await connection.FlushAsync(CancellationToken.None);
            var received = new byte[2];
            var count = await accepted.ReceiveAsync(received, SocketFlags.None);

            Assert.AreEqual(2, count);
            CollectionAssert.AreEqual("hi"u8.ToArray(), received);
            Assert.AreEqual(path, connection.RemoteEndPoint!.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task DialUnixSocketAsync_ToAMissingSocket_ThrowsSocketException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bl507-missing-{Guid.NewGuid():N}.sock");

        await Assert.ThrowsExactlyAsync<SocketException>(
            async () => await new TcpDialer().DialUnixSocketAsync(new UnixSocketAddress(path, IsAbstract: false), CancellationToken.None));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task DialAsync_OverLoopback_CarriesBytesBothWaysThenFailsOnceTheListenerStops()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endPoint = (IPEndPoint)listener.LocalEndpoint;
        var dialer = new TcpDialer();

        var dialed = await dialer.DialAsync(endPoint, cancellation.Token);
        await using (var connection = dialed.Connection)
        {
            using var accepted = await listener.AcceptTcpClientAsync(cancellation.Token);
            var serverStream = accepted.GetStream();

            await connection.WriteAsync(new byte[] { 1, 2, 3 }, cancellation.Token);
            await connection.FlushAsync(cancellation.Token);
            var received = new byte[3];
            await serverStream.ReadExactlyAsync(received, cancellation.Token);
            await serverStream.WriteAsync(new byte[] { 9 }, cancellation.Token);
            var reply = new byte[1];
            var replyLength = await connection.ReadAsync(reply, cancellation.Token);

            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, received);
            Assert.AreEqual(1, replyLength);
            Assert.AreEqual(9, reply[0]);
            Assert.IsFalse(connection.IsSecure);
            Assert.AreEqual(endPoint, connection.RemoteEndPoint);
            Assert.AreEqual(accepted.Client.RemoteEndPoint, dialed.LocalEndPoint);
        }

        listener.Stop();

        await Assert.ThrowsAsync<SocketException>(
            async () => await dialer.DialAsync(endPoint, cancellation.Token));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [OSCondition(OperatingSystems.Linux)]
    [DataRow(false, "socket successfully bound to interface 'lo'", DisplayName = "lo")]
    [DataRow(true, "Local port: 0", DisplayName = "ifhost!lo!127.0.0.1")]
    public async Task DialFromDeviceAsync_OnLinuxWithTheLoopbackDevice_ReportsCurlsLine(bool bindsAddressAfterDevice, string expected)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var events = new RecordingTransferEvents();

        try
        {
            var dialed = await new TcpDialer().DialFromDeviceAsync(
                (IPEndPoint)listener.LocalEndpoint,
                "lo",
                bindsAddressAfterDevice,
                _ => ValueTask.FromResult(new IPEndPoint(IPAddress.Loopback, 0)),
                1,
                events,
                cancellation.Token);
            await dialed.Connection.DisposeAsync();
        }
        finally
        {
            listener.Stop();
        }

        CollectionAssert.AreEqual(new[] { expected }, events.Info);
    }
}
