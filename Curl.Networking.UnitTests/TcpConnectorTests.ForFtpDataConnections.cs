using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="TcpConnector.ForFtpDataConnections" />, FTP's passive data connection's
/// connector, and exit 28 for a dial the system timed out, against curl 8.21.0 (mingw, Schannel),
/// measured on 2026-09-30 with <c>Record-CurlExchange.ps1</c> (BL-797 Notes): with
/// <c>--connect-timeout 1</c> a data connect to <c>10.255.255.1:1025</c> ran on for 21 s, until
/// Windows gave up, and ended with exit 28 and <c>Failed to connect to 127.0.0.1:47911 via
/// 10.255.255.1:1025 after 21125 ms: Could not connect to server</c>; <c>curl
/// http://10.255.255.1:1025/</c> ended the same way, exit 28, after 21047 ms, as the Linux
/// (OpenSSL) build did after 134182 ms.
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ForFtpDataConnections_WhenTheDialStallsPastTheConnectTimeout_KeepsWaiting()
    {
        var time = new ManualTimeProvider();
        var stalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialer = new StallingTcpDialer { OnStalled = () => stalled.TrySetResult() };
        var connector = new TcpConnector(new FakeDnsResolver(IPAddress.Parse("10.255.255.1")), dialer, new FakeTlsProvider(), time, connectTimeout: OneSecond);
        using var cancellation = new CancellationTokenSource();

        var connect = connector.ForFtpDataConnections().ConnectAsync(new ConnectTarget("10.255.255.1", 1025, UseTls: false), cancellation.Token).AsTask();
        await stalled.Task;
        time.Advance(60_000);

        Diagnostics.Arrange("target", "10.255.255.1:1025 with a 1 s connect timeout, the dial stalled, 60 s later");
        Diagnostics.Act("connect completed", connect.IsCompleted);
        Diagnostics.Assert("connect completed", false, connect.IsCompleted);
        Assert.IsFalse(connect.IsCompleted);
        await cancellation.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => connect);
    }

    [TestMethod]
    public async Task ForFtpDataConnections_NumbersItsConnectionsInTheOwnersSequence()
    {
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = CreateConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider());

        var first = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 21, UseTls: false));
        var second = await connector.ForFtpDataConnections().ConnectAsync(new ConnectTarget("127.0.0.1", 1025, UseTls: false), CancellationToken.None);

        Diagnostics.Assert("connection numbers", (0L, 1L), (first.ConnectionNumber, second.ConnectionNumber));
        Assert.AreEqual(0L, first.ConnectionNumber);
        Assert.AreEqual(1L, second.ConnectionNumber);
    }

    [TestMethod]
    public async Task ForFtpDataConnections_WithAConnectToMappingMatchingEveryHost_DialsTheTargetAsGiven()
    {
        // upstream test713: --connect-to ::127.0.0.1:8993 maps the control connection only; the
        // passive data connection still goes to the EPSV reply's port (BL-1976).
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new ManualTimeProvider(),
            connectToMappings: new ConnectToMappings(["::127.0.0.1:8993"]));

        await ConnectLoggedAsync(connector, new ConnectTarget("ftp.example.com", 21, UseTls: false));
        var data = await connector.ForFtpDataConnections().ConnectAsync(new ConnectTarget("127.0.0.1", 9005, UseTls: false), CancellationToken.None);

        Diagnostics.Assert("dialled end points", "127.0.0.1:8993 127.0.0.1:9005", string.Join(' ', dialer.DialedEndPoints));
        Assert.IsNotNull(data.Connection);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(Loopback, 8993), new IPEndPoint(Loopback, 9005) }, dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ForFtpDataConnections_WithANullTarget_ThrowsArgumentNullException()
    {
        var connector = CreateConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer(), new FakeTlsProvider());

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await connector.ForFtpDataConnections().ConnectAsync(null!, CancellationToken.None));

        Diagnostics.Arrange("target", null);
        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("parameter name", "target", exception.ParamName);
        Assert.AreEqual("target", exception.ParamName);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheSystemTimesTheLastDialOut_FailsWithExit28AndCurlsConnectMessage()
    {
        var time = new ManualTimeProvider();
        var events = new RecordingTransferEvents();
        var dialer = new FakeTcpDialer
        {
            DialOutcome = _ =>
            {
                time.Advance(21047);
                throw new SocketException((int)SocketError.TimedOut);
            },
        };
        var connector = new TcpConnector(new FakeDnsResolver(IPAddress.Parse("10.255.255.1")), dialer, new FakeTlsProvider(), time);

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("10.255.255.1", 1025, UseTls: false) { Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, result.ExitCode);

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Failed to connect to 10.255.255.1:1025 after 21047 ms: Could not connect to server", result.ErrorMessage);
        Assert.IsFalse(result.IsConnectionRefused);
        Assert.IsNull(result.Connection);
        Assert.AreEqual("Failed to connect to 10.255.255.1:1025 after 21047 ms: Could not connect to server", events.Info[^1]);
    }
}
