using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="TcpConnector.WithoutConnectTimeout" />, FTP's passive data connection's
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
    public async Task WithoutConnectTimeout_WhenTheDialStallsPastTheConnectTimeout_KeepsWaiting()
    {
        var time = new ManualTimeProvider();
        var stalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialer = new StallingTcpDialer { OnStalled = () => stalled.TrySetResult() };
        var connector = new TcpConnector(new FakeDnsResolver(IPAddress.Parse("10.255.255.1")), dialer, new FakeTlsProvider(), time, connectTimeout: OneSecond);
        using var cancellation = new CancellationTokenSource();

        var connect = connector.WithoutConnectTimeout().ConnectAsync(new ConnectTarget("10.255.255.1", 1025, UseTls: false), cancellation.Token).AsTask();
        await stalled.Task;
        time.Advance(60_000);

        Assert.IsFalse(connect.IsCompleted);
        await cancellation.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => connect);
    }

    [TestMethod]
    public async Task WithoutConnectTimeout_NumbersItsConnectionsInTheOwnersSequence()
    {
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = CreateConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider());

        var first = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 21, UseTls: false), CancellationToken.None);
        var second = await connector.WithoutConnectTimeout().ConnectAsync(new ConnectTarget("127.0.0.1", 1025, UseTls: false), CancellationToken.None);

        Assert.AreEqual(0L, first.ConnectionNumber);
        Assert.AreEqual(1L, second.ConnectionNumber);
    }

    [TestMethod]
    public async Task WithoutConnectTimeout_WithANullTarget_ThrowsArgumentNullException()
    {
        var connector = CreateConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer(), new FakeTlsProvider());

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await connector.WithoutConnectTimeout().ConnectAsync(null!, CancellationToken.None));

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

        var result = await connector.ConnectAsync(new ConnectTarget("10.255.255.1", 1025, UseTls: false) { Events = events }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Failed to connect to 10.255.255.1:1025 after 21047 ms: Could not connect to server", result.ErrorMessage);
        Assert.IsFalse(result.IsConnectionRefused);
        Assert.IsNull(result.Connection);
        Assert.AreEqual("Failed to connect to 10.255.255.1:1025 after 21047 ms: Could not connect to server", events.Info[^1]);
    }
}
