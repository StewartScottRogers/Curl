using System.Net;
using System.Net.Sockets;

using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="DarwinFastOpenConnect" /> (BL-1101): <c>--tcp-fastopen</c> connects through <c>connectx</c> on
/// macOS, and nowhere else.
/// </summary>
[TestClass]
public sealed class DarwinFastOpenConnectTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public void TryConnect_OffMacOS_LeavesTheSocketToConnectAsUsual()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Diagnostics.Arrange("endpoint", "loopback:9");
        Diagnostics.Arrange("socket connected before", socket.Connected);

        var connected = DarwinFastOpenConnect.TryConnect(socket, new IPEndPoint(IPAddress.Loopback, 9));

        Diagnostics.Act("result is null", connected is null);
        Diagnostics.Act("socket connected after", socket.Connected);
        Diagnostics.Assert("result is null", true, connected is null);
        Assert.IsNull(connected);
        Diagnostics.Assert("socket connected", false, socket.Connected);
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
        Diagnostics.Arrange("listener", "loopback, ephemeral port");
        Diagnostics.Arrange("fast open", true);
        Diagnostics.Arrange("first bytes", "GET");

        Socket? connected;
        using (Diagnostics.Phase("connect"))
        {
            connected = DarwinFastOpenConnect.TryConnect(socket, (IPEndPoint)listener.LocalEndPoint!);
        }

        Diagnostics.Act("result is null", connected is null);
        Diagnostics.Assert("result is null", false, connected is null);
        Assert.IsNotNull(connected);
        Diagnostics.Assert("local endpoint is set", true, connected.LocalEndPoint is not null);
        Assert.IsNotNull(connected.LocalEndPoint);
        // No SYN has left yet, so the socket is not connected until the first write (BL-1158, ADR-0358).
        await using var stream = new DeferredConnectSocketStream(connected);
        var received = new byte[3];
        using (Diagnostics.Phase("first write and accept"))
        {
            await stream.WriteAsync("GET"u8.ToArray(), cancellation.Token);
            using var accepted = await listener.AcceptAsync(cancellation.Token);
            await new NetworkStream(accepted).ReadExactlyAsync(received, cancellation.Token);
        }

        Diagnostics.Bytes("received by the listener", received);
        Diagnostics.Act("received length", received.Length);
        Diagnostics.Diff("received bytes", "GET"u8, received);
        CollectionAssert.AreEqual("GET"u8.ToArray(), received);
    }
}
