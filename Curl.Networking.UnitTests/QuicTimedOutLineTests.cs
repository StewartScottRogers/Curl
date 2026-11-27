using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Pins that a QUIC dial, and a CONNECT-UDP proxy tunnel, that <c>-m</c> ends reports
/// <c>Connection timed out after &lt;n&gt; milliseconds</c> even when the cancellation arrives with
/// some of the connect's limit still to run, as it does on Linux (BL-6072, ADR-1457). curl 8.18.0
/// <c>--http3-only -sv -m 3</c> against a silent peer prints that line before
/// <c>closing connection #0</c> and exits 28 (measured, BL-6072 Notes). The caller's token stands
/// for <c>-m</c>, whose clock starts before the connect's.
/// </summary>
[TestClass]
public sealed class QuicTimedOutLineTests
{
    private static readonly IPAddress PeerAddress = IPAddress.Parse("192.0.2.20");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(2997, 3000, DisplayName = "-m 3, 3 ms early")]
    [DataRow(2500, 3000, DisplayName = "-m 3, 500 ms early")]
    [DataRow(3004, 3004, DisplayName = "-m 3, after the limit")]
    public async Task ConnectMultiplexedAsync_WhenMaxTimeCancelsTheQuicDialAtTheLimit_ReportsTheTimedOutLine(int elapsed, int reported)
    {
        using var maxTime = new CancellationTokenSource();
        var time = new ManualTimeProvider();
        var opener = new QuicServerChannelOpener();
        var events = new RecordingTransferEvents();
        var connector = Connector(new FakeTcpDialer(), opener, time);
        Diagnostics.Arrange("elapsed of a 3000 ms limit", elapsed);

        var connecting = connector.ConnectMultiplexedAsync(Target(events), maxTime.Token).AsTask();
        await opener.WaitingToReceive.WaitAsync();
        time.Advance(elapsed);
        await maxTime.CancelAsync();
        var result = await connecting;

        AssertTimedOutLine(result, events, $"Connection timed out after {reported} milliseconds");
        Assert.IsTrue(opener.Opened.Single().IsDisposed);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheHandshakeTimerFiresBeforeTheClockReadsTheLimit_ReportsTheLimit()
    {
        var time = new EarlyTimerTimeProvider();
        var opener = new QuicServerChannelOpener();
        var events = new RecordingTransferEvents();
        var connector = Connector(new FakeTcpDialer(), opener, time);
        Diagnostics.Arrange("handshake timer", "fires at 2998 of 3000 ms");

        var connecting = connector.ConnectMultiplexedAsync(Target(events), CancellationToken.None).AsTask();
        await opener.WaitingToReceive.WaitAsync();
        time.FireTimerAt(2998);
        var result = await connecting;

        AssertTimedOutLine(result, events, "Connection timed out after 3000 milliseconds");
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheCallerCancelsTheQuicDialWellBeforeTheLimit_LetsTheCancellationEscape()
    {
        using var caller = new CancellationTokenSource();
        var time = new ManualTimeProvider();
        var opener = new QuicServerChannelOpener();
        var events = new RecordingTransferEvents();
        var connector = Connector(new FakeTcpDialer(), opener, time);
        Diagnostics.Arrange("elapsed of a 3000 ms limit", 2499);

        var connecting = connector.ConnectMultiplexedAsync(Target(events), caller.Token).AsTask();
        await opener.WaitingToReceive.WaitAsync();
        time.Advance(2499);
        await caller.CancelAsync();
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => connecting);

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("timed out lines", 0, events.Info.Count(IsTimedOutLine));
        Assert.DoesNotContain(IsTimedOutLine, events.Info);
        Assert.IsTrue(opener.Opened.Single().IsDisposed);
    }

    [TestMethod]
    [DataRow(2997, 3000, DisplayName = "-m 3, 3 ms early")]
    [DataRow(2500, 3000, DisplayName = "-m 3, 500 ms early")]
    [DataRow(3004, 3004, DisplayName = "-m 3, after the limit")]
    public async Task ConnectMultiplexedAsync_WhenMaxTimeCancelsTheUdpProxyTunnelAtTheLimit_ReportsTheTimedOutLine(int elapsed, int reported)
    {
        using var maxTime = new CancellationTokenSource();
        var time = new ManualTimeProvider();
        var dialer = new StallingTcpDialer
        {
            OnStalled = () =>
            {
                time.Advance(elapsed);
                maxTime.Cancel();
            },
        };
        var events = new RecordingTransferEvents();
        var connector = Connector(dialer, new QuicServerChannelOpener(), time);
        var proxied = Target(events) with { Proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null) };
        Diagnostics.Arrange("elapsed of a 3000 ms limit", elapsed);

        var result = await connector.ConnectMultiplexedAsync(proxied, maxTime.Token);

        AssertTimedOutLine(result, events, $"Connection timed out after {reported} milliseconds");
    }

    private static bool IsTimedOutLine(string line) => line.StartsWith("Connection timed out", StringComparison.Ordinal);

    private static ConnectTarget Target(ITransferEvents events) =>
        new("quic.test", 443, UseTls: true) { PoolScheme = "https", Events = events };

    private static TcpConnector Connector(ITcpDialer dialer, QuicServerChannelOpener opener, TimeProvider time) =>
        new(
            new FakeDnsResolver(PeerAddress),
            dialer,
            new FakeTlsProvider(),
            time,
            connectTimeout: TimeSpan.FromSeconds(3),
            quicDialer: new QuicDialer(opener, new TlsClientOptions(Insecure: true), true, time, SystemTlsRandomSource.Instance));

    private void AssertTimedOutLine(MultiplexedConnectResult result, RecordingTransferEvents events, string line)
    {
        Diagnostics.Act("error message", result.ErrorMessage);
        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(line, result.ErrorMessage);
        Assert.AreEqual(line, events.Info[^1]);
    }

    /// <summary>
    /// A clock in milliseconds whose 3 s timer the test fires while the clock still reads less than
    /// its due time, as a Linux timer on the coarse monotonic clock can. No other timer fires.
    /// </summary>
    private sealed class EarlyTimerTimeProvider : TimeProvider
    {
        private long now;

        private Action fire = () => { };

        public override long TimestampFrequency => 1000;

        public override long GetTimestamp() => now;

        public void FireTimerAt(long timestamp)
        {
            now = timestamp;
            fire();
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime == TimeSpan.FromSeconds(3))
            {
                fire = () => callback(state);
            }

            return new IdleTimer();
        }

        private sealed class IdleTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
