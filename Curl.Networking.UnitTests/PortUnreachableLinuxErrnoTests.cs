using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins BL-5315: QUIC's UDP socket is connected to the server where the platform needs it (Linux and macOS),
/// so the kernel's ICMP port unreachable reaches the receive as <see cref="SocketError.ConnectionRefused" />
/// (errno 111 on Linux), which <see cref="QuicDialer" /> words as exit 56 (<c>TcpConnectorQuicTests</c>'
/// <c>ConnectMultiplexedAsync_WhenAReceiveFailsWithAnotherErrorInTheSchannelBuild_UsesCurlsWordsForIt</c>).
/// Each test opens and closes a loopback UDP socket and sends nothing.
/// </summary>
[TestClass]
public sealed class PortUnreachableLinuxErrnoTests
{
    private static readonly IPEndPoint NothingListening = new(IPAddress.Loopback, 9);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task Constructor_ConnectingToTheServer_LeavesTheSocketOnTheRouteToIt()
    {
        Diagnostics.Arrange("connectsToServer", true);

        await using var channel = new UdpDatagramChannel(NothingListening, static (socket, end) => socket.Bind(end), connectsToServer: true);

        var local = (IPEndPoint)channel.LocalEndPoint;
        Diagnostics.Assert("local address", IPAddress.Loopback, local.Address);
        Assert.AreEqual(IPAddress.Loopback, local.Address);
    }

    [TestMethod]
    public async Task Constructor_WithLocalAddressAndPort_BindsThatAddressOnAnEphemeralPort()
    {
        Diagnostics.Arrange("local address", IPAddress.Loopback);

        await using var channel = new UdpDatagramChannel(NothingListening, IPAddress.Loopback, 0);

        Assert.AreEqual(IPAddress.Loopback, ((IPEndPoint)channel.LocalEndPoint!).Address);
    }

    [TestMethod]
    public void Constructor_WhenTheBindFails_RethrowsWithoutConnecting()
    {
        Diagnostics.Arrange("bind", "throws AddressAlreadyInUse");

        var failure = Assert.ThrowsExactly<SocketException>(
            () => new UdpDatagramChannel(NothingListening, static (_, _) => throw new SocketException((int)SocketError.AddressAlreadyInUse), connectsToServer: true));
        Assert.AreEqual(SocketError.AddressAlreadyInUse, failure.SocketErrorCode);
    }

    [TestMethod]
    public async Task UdpChannelOpener_ConnectingOpener_OpensConnectedChannelsThroughEveryOpenMethod()
    {
        var opener = new UdpChannelOpener((_, _) => true, connectsToServer: true);
        Diagnostics.Arrange("opener", "connects to server");

        await using var opened = opener.Open(NothingListening);
        await using var openedFrom = opener.OpenFrom(NothingListening, new IPEndPoint(IPAddress.Loopback, 0), 1, NoTransferEvents.Instance);
        await using var openedFromDevice = await opener.OpenFromDeviceAsync(
            NothingListening,
            "eth0",
            bindsAddressAfterDevice: false,
            _ => ValueTask.FromResult(new IPEndPoint(IPAddress.Loopback, 0)),
            1,
            NoTransferEvents.Instance,
            CancellationToken.None);

        foreach (var channel in new[] { opened, openedFrom, openedFromDevice })
        {
            Assert.AreEqual(IPAddress.Loopback, ((IPEndPoint)channel.LocalEndPoint!).Address);
        }
    }
}
