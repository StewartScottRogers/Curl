using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="DarwinFastOpenConnect" /> (BL-1101): <c>--tcp-fastopen</c> connects through <c>connectx</c> on
/// macOS, and nowhere else.
/// </summary>
[TestClass]
public sealed class DarwinFastOpenConnectTests
{
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public void TryConnect_OffMacOS_LeavesTheSocketToConnectAsUsual()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        Assert.IsNull(DarwinFastOpenConnect.TryConnect(socket, new IPEndPoint(IPAddress.Loopback, 9)));
        Assert.IsFalse(socket.Connected);
    }

    [TestMethod]
    // Loopback only and so in the fast run: CI's macOS job is the only place this route runs (BL-1101 Notes).
    [OSCondition(OperatingSystems.OSX)]
    public async Task TryConnect_OnMacOS_ConnectsThroughConnectxAndCarriesTheFirstBytes()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen();
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        new TcpDialer(new TcpSocketOptions(FastOpen: true)).ApplySocketOptions(socket);

        var connected = DarwinFastOpenConnect.TryConnect(socket, (IPEndPoint)listener.LocalEndPoint!);

        Assert.IsNotNull(connected);
        Assert.IsNotNull(connected.LocalEndPoint);
        // No SYN has left yet, so the socket is not connected until the first write (BL-1158, ADR-0358).
        await using var stream = new DeferredConnectSocketStream(connected);
        await stream.WriteAsync("GET"u8.ToArray(), cancellation.Token);
        using var accepted = await listener.AcceptAsync(cancellation.Token);
        var received = new byte[3];
        await new NetworkStream(accepted).ReadExactlyAsync(received, cancellation.Token);
        CollectionAssert.AreEqual("GET"u8.ToArray(), received);
    }
}
