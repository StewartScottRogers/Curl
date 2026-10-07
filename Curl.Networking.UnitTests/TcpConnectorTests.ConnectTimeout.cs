using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="TcpConnector" />'s connect timeout (ADR-0117) against curl 8.21.0 (mingw,
/// Schannel), measured on 2026-09-28 with <c>Record-CurlExchange.ps1</c> (BL-510 Notes):
/// <c>-v --connect-timeout 1 http://10.255.255.1/</c> and the same against a loopback listener
/// that never answers the ClientHello both end with exit 28 and
/// <c>Connection timed out after &lt;n&gt; milliseconds</c>, also printed as a <c>-v</c> line.
/// </summary>
public sealed partial class TcpConnectorTests
{
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    [TestMethod]
    public async Task ConnectAsync_WhenTheDialStallsPastTheConnectTimeout_FailsWithExit28AndCurlsMessage()
    {
        var events = new RecordingTransferEvents();
        var time = new ManualTimeProvider();
        var dialer = new StallingTcpDialer { OnStalled = () => time.Advance(1001) };
        var connector = new TcpConnector(new FakeDnsResolver(IPAddress.Parse("10.255.255.1")), dialer, new FakeTlsProvider(), time, connectTimeout: OneSecond);

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("10.255.255.1", 80, UseTls: false) { Events = events });

        Diagnostics.Assert("result.ExitCode", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Connection timed out after 1001 milliseconds", result.ErrorMessage);
        Assert.IsNull(result.Connection);
        Assert.AreEqual(0, result.ConnectionNumber);
        CollectionAssert.AreEqual(
            new[] { "  Trying 10.255.255.1:80...", "Connection timed out after 1001 milliseconds" },
            events.Info);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheHandshakeStallsPastTheConnectTimeout_FailsWithExit28AndCurlsMessage()
    {
        var time = new ManualTimeProvider();
        var tlsProvider = new StallingTlsProvider { OnStalled = () => time.Advance(1006) };
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, tlsProvider, time, connectTimeout: OneSecond);

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 18510, UseTls: true));

        Diagnostics.Assert("result.ExitCode", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Connection timed out after 1006 milliseconds", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughAProxyWhoseTunnelStalls_FailsWithExit28()
    {
        var time = new ManualTimeProvider();
        var connection = new StallingConnection { OnStalled = () => time.Advance(2000) };
        var dialer = new FakeTcpDialer { DialOutcome = _ => connection };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), time, connectTimeout: OneSecond);
        var target = new ConnectTarget("example.com", 80, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null) };

        var result = await ConnectLoggedAsync(connector, target);

        Diagnostics.Assert("result.ExitCode", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Connection timed out after 2000 milliseconds", result.ErrorMessage);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheConnectFinishesInTime_IsUnaffectedByTheConnectTimeout()
    {
        var time = new ManualTimeProvider();
        var dialer = new FakeTcpDialer { DialOutcome = _ => { time.Advance(999); return new FakeConnection(); } };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), time, connectTimeout: OneSecond);

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 80, UseTls: false));

        Diagnostics.Assert("result.ExitCode", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNotNull(result.Connection);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "not given")]
    [DataRow(0, DisplayName = "0")]
    public async Task ConnectAsync_WithoutAConnectTimeout_WaitsCurlsDefault300Seconds(int? seconds)
    {
        var time = new ManualTimeProvider();
        var dialer = new FakeTcpDialer { DialOutcome = _ => { time.Advance(299_999); return new FakeConnection(); } };
        var tlsProvider = new StallingTlsProvider { OnStalled = () => time.Advance(1) };
        TimeSpan? connectTimeout = seconds is { } given ? TimeSpan.FromSeconds(given) : null;
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, tlsProvider, time, connectTimeout: connectTimeout);

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("localhost", 443, UseTls: true));

        Diagnostics.Assert("result.ExitCode", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Connection timed out after 300000 milliseconds", result.ErrorMessage);
        Assert.AreEqual(TimeSpan.FromSeconds(300), TcpConnector.DefaultConnectTimeout);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheCallerCancelsBeforeTheConnectTimeout_LetsTheCancellationEscape()
    {
        using var caller = new CancellationTokenSource();
        var dialer = new StallingTcpDialer { OnStalled = caller.Cancel };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new ManualTimeProvider(), connectTimeout: OneSecond);
        Diagnostics.Arrange("connect timeout", OneSecond);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 80, UseTls: false), caller.Token));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("caller cancelled", true, caller.IsCancellationRequested);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheCallerCancelsWithTheConnectTimeoutPassed_ReportsTheTimeout()
    {
        // A simultaneous -m cannot turn the connect timeout into an exception (ADR-0117).
        using var caller = new CancellationTokenSource();
        var time = new SteppingTimeProvider(0) { Step = 1000 };
        var dialer = new StallingTcpDialer { OnStalled = caller.Cancel };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), time, connectTimeout: OneSecond);

        Diagnostics.Arrange("connect timeout", OneSecond);
        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 80, UseTls: false), caller.Token);
        Diagnostics.Act("exit code", result.ExitCode);

        Diagnostics.Assert("result.ExitCode", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
    }
}
