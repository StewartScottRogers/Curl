using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="UdpDatagramChannel" />. Opening, disposing and cancelling touch only a
/// local loopback socket and send nothing; the round trip sends datagrams and is tagged
/// <c>Integration</c>.
/// </summary>
[TestClass]
public sealed class UdpDatagramChannelTests
{
    private static readonly IPEndPoint TftpOnLoopback = new(IPAddress.Loopback, 69);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Constructor_WithNullServerEndPoint_ThrowsArgumentNullException()
    {
        Diagnostics.Arrange("server end point", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new UdpDatagramChannel(null!));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("parameter name", "serverEndPoint", exception.ParamName);
        Assert.AreEqual("serverEndPoint", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_WhenTheSocketCannotBeBound_DisposesItAndRethrows()
    {
        Socket? boundSocket = null;
        Diagnostics.Arrange("server end point", TftpOnLoopback);
        Diagnostics.Arrange("bind", "throws SocketException AddressAlreadyInUse");

        var exception = Assert.ThrowsExactly<SocketException>(
            () => new UdpDatagramChannel(
                TftpOnLoopback,
                (socket, _) =>
                {
                    boundSocket = socket;
                    throw new SocketException((int)SocketError.AddressAlreadyInUse);
                }));

        Diagnostics.Act("socket error code", exception.SocketErrorCode);
        Diagnostics.Act("bind called with a socket", boundSocket is not null);
        Diagnostics.Assert("socket error code", SocketError.AddressAlreadyInUse, exception.SocketErrorCode);
        Assert.AreEqual(SocketError.AddressAlreadyInUse, exception.SocketErrorCode);
        Assert.IsNotNull(boundSocket);
        var rebind = Assert.ThrowsExactly<ObjectDisposedException>(() => boundSocket.Bind(new IPEndPoint(IPAddress.Any, 0)));
        Diagnostics.Assert("bind after the failure", nameof(ObjectDisposedException), rebind.GetType().Name);
    }

    [TestMethod]
    public async Task Constructor_ForAnIPv4ServerEndPoint_BindsAnIPv4SocketToAnEphemeralPort()
    {
        Diagnostics.Arrange("server end point", TftpOnLoopback);

        await using var channel = new UdpDatagramChannel(TftpOnLoopback);

        var local = (IPEndPoint)channel.LocalEndPoint;
        Diagnostics.Act("local address family", local.AddressFamily);
        Diagnostics.Act("local port is ephemeral", local.Port != 0);
        Diagnostics.Assert("local address family", AddressFamily.InterNetwork, local.AddressFamily);
        Diagnostics.Assert("server end point", TftpOnLoopback, channel.ServerEndPoint);
        Assert.AreEqual(AddressFamily.InterNetwork, local.AddressFamily);
        Assert.AreNotEqual(0, local.Port);
        Assert.AreEqual(TftpOnLoopback, channel.ServerEndPoint);
    }

    [TestMethod]
    public async Task LocalEndPoint_WhenBoundToAnyAddress_ReportsTheAddressTheRouteToTheServerSendsFrom()
    {
        Diagnostics.Arrange("server end point", TftpOnLoopback);
        Diagnostics.Arrange("bound to", "any address");
        await using var channel = new UdpDatagramChannel(TftpOnLoopback);

        var local = (IPEndPoint)channel.LocalEndPoint;

        Diagnostics.Act("local address", local.Address);
        Diagnostics.Act("local port is ephemeral", local.Port != 0);
        Diagnostics.Assert("local address", IPAddress.Loopback, local.Address);
        Assert.AreEqual(IPAddress.Loopback, local.Address);
        Assert.AreNotEqual(0, local.Port);
    }

    [TestMethod]
    public async Task LocalEndPoint_WhenTheKernelHasNoRouteToTheServer_ReportsTheAnyAddress()
    {
        Diagnostics.Arrange("server end point", TftpOnLoopback);
        Diagnostics.Arrange("route probe", "throws SocketException NetworkUnreachable");
        await using var channel = new UdpDatagramChannel(
            TftpOnLoopback,
            static (socket, localEndPoint) => socket.Bind(localEndPoint),
            connectRouteProbe: static (_, _) => throw new SocketException((int)SocketError.NetworkUnreachable));

        var local = (IPEndPoint)channel.LocalEndPoint;

        Diagnostics.Act("local address", local.Address);
        Diagnostics.Act("local port is ephemeral", local.Port != 0);
        Diagnostics.Assert("local address", IPAddress.Any, local.Address);
        Assert.AreEqual(IPAddress.Any, local.Address);
        Assert.AreNotEqual(0, local.Port);
    }

    [TestMethod]
    public async Task LocalEndPoint_WhenBoundToALocalAddress_ReportsItWithoutAskingTheRoute()
    {
        var routeAsked = false;
        Diagnostics.Arrange("server end point", TftpOnLoopback);
        Diagnostics.Arrange("local address", IPAddress.Loopback);
        await using var channel = new UdpDatagramChannel(
            TftpOnLoopback,
            static (socket, localEndPoint) => socket.Bind(localEndPoint),
            IPAddress.Loopback,
            connectRouteProbe: (_, _) => routeAsked = true);

        var localAddress = ((IPEndPoint)channel.LocalEndPoint).Address;

        Diagnostics.Act("local address", localAddress);
        Diagnostics.Act("route asked", routeAsked);
        Diagnostics.Assert("local address", IPAddress.Loopback, localAddress);
        Diagnostics.Assert("route asked", false, routeAsked);
        Assert.AreEqual(IPAddress.Loopback, ((IPEndPoint)channel.LocalEndPoint).Address);
        Assert.IsFalse(routeAsked);
    }

    [TestMethod]
    public async Task Constructor_ForAnIPv6ServerEndPoint_BindsAnIPv6Socket()
    {
        var serverEndPoint = new IPEndPoint(IPAddress.IPv6Loopback, 69);
        Diagnostics.Arrange("server end point", serverEndPoint);

        await using var channel = new UdpDatagramChannel(serverEndPoint);

        Diagnostics.Act("local address family", channel.LocalEndPoint.AddressFamily);
        Diagnostics.Assert("local address family", AddressFamily.InterNetworkV6, channel.LocalEndPoint.AddressFamily);
        Assert.AreEqual(AddressFamily.InterNetworkV6, channel.LocalEndPoint.AddressFamily);
    }

    [TestMethod]
    public async Task SendAsync_WithNullDestination_ThrowsArgumentNullException()
    {
        Diagnostics.Arrange("destination", "null");
        Diagnostics.Bytes("datagram", [1]);
        await using var channel = new UdpDatagramChannel(TftpOnLoopback);

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await channel.SendAsync(new byte[] { 1 }, null!, CancellationToken.None));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("parameter name", "destination", exception.ParamName);
        Assert.AreEqual("destination", exception.ParamName);
    }

    [TestMethod]
    public async Task ReceiveAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        Diagnostics.Arrange("receive buffer length", 16);
        Diagnostics.Arrange("cancellation", "cancelled after the receive starts");
        await using var channel = new UdpDatagramChannel(TftpOnLoopback);
        using var cancellation = new CancellationTokenSource();

        var receive = channel.ReceiveAsync(new byte[16], cancellation.Token);
        await cancellation.CancelAsync();

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(async () => await receive);
        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception is an OperationCanceledException", true, exception is OperationCanceledException);
    }

    [TestMethod]
    public async Task DisposeAsync_ClosesTheSocket()
    {
        Diagnostics.Arrange("server end point", TftpOnLoopback);
        var channel = new UdpDatagramChannel(TftpOnLoopback);

        await channel.DisposeAsync();

        var exception = await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
            async () => await channel.ReceiveAsync(new byte[16], CancellationToken.None));
        Diagnostics.Act("receive after dispose", exception.GetType().Name);
        Diagnostics.Assert("receive after dispose", nameof(ObjectDisposedException), exception.GetType().Name);
    }

}
