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
    public async Task ConnectAsync_PooledAndCancelledByTheCallerBeforeTheLimit_OffersTheNextConnectionNumber()
    {
        // The handler numbers a connect -m ended on a redirect hop with this (BL-6074).
        using var caller = new CancellationTokenSource();
        var dialer = new StallingTcpDialer { OnStalled = caller.Cancel };
        await using var pool = new PoolingConnector(
            new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new ManualTimeProvider(), connectTimeout: OneSecond),
            new ManualTimeProvider());
        var earlier = pool.ConnectionNumbers.NumberNextConnection();

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await pool.ConnectAsync(new ConnectTarget("127.0.0.1", 80, UseTls: false), caller.Token));
        var take = (Func<long>)exception.Data[ConnectCancellationKeys.ConnectionNumberTaker]!;

        Diagnostics.Assert("earlier number", 0L, earlier);
        Assert.AreEqual(1L, take());
        Assert.AreEqual(2L, pool.ConnectionNumbers.NumberNextConnection());
        Assert.IsNull(exception.Data[ConnectCancellationKeys.ArrivedWhileResolving]);
    }

    [TestMethod]
    public async Task ConnectAsync_CancelledByTheCallerWhileResolving_SaysSoOnTheCancellation()
    {
        var resolver = new FakeDnsResolver(Loopback) { ExceptionToThrow = new OperationCanceledException() };
        await using var pool = new PoolingConnector(
            new TcpConnector(resolver, new StallingTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider(), connectTimeout: OneSecond),
            new ManualTimeProvider());

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await pool.ConnectAsync(new ConnectTarget("slow.example.test", 1, UseTls: false), CancellationToken.None));

        Diagnostics.Assert("arrived while resolving", true, exception.Data[ConnectCancellationKeys.ArrivedWhileResolving]);
        Assert.AreEqual(true, exception.Data[ConnectCancellationKeys.ArrivedWhileResolving]);
        Assert.IsNotNull(exception.Data[ConnectCancellationKeys.ConnectionNumberTaker]);
    }

    [TestMethod]
    [DataRow(true, true, DisplayName = "a cancelled QUIC session connect")]
    [DataRow(false, false, DisplayName = "a TCP connect that threw something else")]
    public async Task ConnectAsync_PooledFailureThatCurlDoesNotNumber_OffersNoConnectionNumber(bool isQuic, bool isCancellation)
    {
        Exception failure = isCancellation ? new OperationCanceledException() : new InvalidOperationException();
        await using var pool = new PoolingConnector(new ThrowingConnector(failure), new ManualTimeProvider());
        var target = new ConnectTarget("127.0.0.1", 443, UseTls: true);

        var exception = await Assert.ThrowsAsync<Exception>(async () => _ = isQuic
            ? await pool.ConnectMultiplexedSessionAsync(target, _ => throw new NotSupportedException(), CancellationToken.None)
            : await pool.ConnectAsync(target, CancellationToken.None));

        Diagnostics.Assert("data entries", 0, exception.Data.Count);
        Assert.AreSame(failure, exception);
        Assert.IsEmpty(exception.Data);
    }

    private sealed class ThrowingConnector(Exception failure) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw failure;

        public ValueTask<ConnectResult> ConnectMultiplexedSessionAsync(
            ConnectTarget target, Func<IMultiplexedConnection, IConnection> openSession, CancellationToken cancellationToken) =>
            throw failure;
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
