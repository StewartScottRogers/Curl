using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins the IPv4-mapped IPv6 literal (<c>[::ffff:127.0.0.1]</c>): curl 8.22.0 connects to an IPv4 listener
/// through it from a dual-stack IPv6 socket (<c>Established connection to [::ffff:127.0.0.1] ... from
/// ::ffff:127.0.0.1</c>, measured 2026-10-22, BL-3174 Notes), so <see cref="TcpDialer" /> makes every IPv6
/// socket dual-stack and <see cref="TcpConnector" /> dials the literal unchanged (ADR-0775). The loopback
/// test connects and closes without sending a byte, so it is a unit test (ADR-0421).
/// </summary>
[TestClass]
public sealed class TcpDialerMappedIPv4Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ApplySocketOptions_OnAnIpV6Socket_MakesItDualStack()
    {
        using var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
        Diagnostics.Arrange("family, dual mode before", $"InterNetworkV6, {socket.DualMode}");

        new TcpDialer().ApplySocketOptions(socket);

        Diagnostics.Act("dual mode", socket.DualMode);
        Diagnostics.Assert("dual mode", true, socket.DualMode);
        Assert.IsTrue(socket.DualMode);
    }

    [TestMethod]
    public async Task DialAsync_ToAMappedLiteralOfAnIpV4Listener_Connects()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var mapped = new IPEndPoint(IPAddress.Parse("::ffff:127.0.0.1"), port);
        Diagnostics.Arrange("listener, dialled", $"127.0.0.1:{port}, {mapped}");
        var accepting = listener.AcceptSocketAsync(TestContext.CancellationToken).AsTask();

        DialedTcpConnection dialed;
        using (Diagnostics.Phase("dial"))
        {
            dialed = await new TcpDialer().DialAsync(mapped, TestContext.CancellationToken);
        }

        using var accepted = await accepting;
        await using var connection = dialed.Connection;
        var acceptedRemote = (IPEndPoint)accepted.RemoteEndPoint!;
        Diagnostics.Act("local end, accepted peer", $"{dialed.LocalEndPoint}, {acceptedRemote}");
        Diagnostics.Assert("accepted peer port", dialed.LocalEndPoint.Port, acceptedRemote.Port);
        Assert.AreEqual(dialed.LocalEndPoint.Port, acceptedRemote.Port);
        Assert.AreEqual(IPAddress.Parse("::ffff:127.0.0.1"), dialed.LocalEndPoint.Address);
        Assert.AreEqual(IPAddress.Loopback, acceptedRemote.Address);
    }

    [TestMethod]
    [DataRow("::ffff:127.0.0.1", DisplayName = "IPv4-mapped literal")]
    [DataRow("::1", DisplayName = "IPv6 loopback")]
    [DataRow("127.0.0.1", DisplayName = "IPv4 literal")]
    public async Task ConnectAsync_ToAnAddressLiteral_DialsItUnchanged(string host)
    {
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(new FakeDnsResolver(IPAddress.Parse(host)), dialer, new FakeTlsProvider(), new ManualTimeProvider());
        Diagnostics.Arrange("host", host);

        var result = await connector.ConnectAsync(new ConnectTarget(host, 18174, UseTls: false), CancellationToken.None);

        Diagnostics.Act("exit code, dialled", $"{result.ExitCode}, {string.Join(", ", dialer.DialedEndPoints)}");
        Diagnostics.Assert("dialled", new IPEndPoint(IPAddress.Parse(host), 18174).ToString(), string.Join(", ", dialer.DialedEndPoints));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(IPAddress.Parse(host), 18174) }, dialer.DialedEndPoints);
    }
}
