using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins that <see cref="TcpConnector" /> reports <c>Connection timed out after &lt;n&gt; milliseconds</c>
/// for a connect <c>-m</c> ends, whether the caller's <c>-m</c> cancellation or the connector's own
/// limit fires first, as curl 8.18.0 prints it on Linux (BL-6039, ADR-1457). The caller's token
/// stands for <c>-m</c>, whose clock starts before the connect's.
/// </summary>
[TestClass]
public sealed class TcpConnectorTimedOutLineTests
{
    private static readonly ConnectTarget Unreachable = new("192.0.2.20", 1, UseTls: false);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(3000, 2997, 3000, DisplayName = "-m 3, 3 ms early")]
    [DataRow(3000, 2500, 3000, DisplayName = "-m 3, 500 ms early")]
    [DataRow(1000, 750, 1000, DisplayName = "-m 1, a quarter early")]
    [DataRow(3000, 3000, 3000, DisplayName = "-m 3, on the limit")]
    [DataRow(3000, 3004, 3004, DisplayName = "-m 3, after the limit")]
    public async Task ConnectAsync_WhenMaxTimeCancelsAtTheLimit_ReportsTheTimedOutLine(int limit, int elapsed, int reported)
    {
        using var maxTime = new CancellationTokenSource();
        var time = new ManualTimeProvider();
        var dialer = new StallingTcpDialer { OnStalled = () => AdvanceThenCancel(time, elapsed, maxTime) };

        await AssertTimedOutLineAsync(Connector(dialer, time, limit), maxTime.Token, $"Connection timed out after {reported} milliseconds");
    }

    [TestMethod]
    public async Task ConnectAsync_WhenItsOwnTimerFiresBeforeTheClockReadsTheLimit_ReportsTheTimedOutLine()
    {
        var time = new EarlyTimerTimeProvider();
        var dialer = new StallingTcpDialer { OnStalled = () => time.FireTimerAt(2000) };

        await AssertTimedOutLineAsync(Connector(dialer, time, 3000), CancellationToken.None, "Connection timed out after 3000 milliseconds");
    }

    [TestMethod]
    public async Task ConnectAsync_WhenMaxTimeCancelsAResolveJustBeforeTheLimit_ReportsResolvingTimedOut()
    {
        using var maxTime = new CancellationTokenSource();
        var time = new ManualTimeProvider();
        var resolver = new StallingDnsResolver(() => AdvanceThenCancel(time, 2997, maxTime));
        var connector = new TcpConnector(resolver, new StallingTcpDialer(), new FakeTlsProvider(), time, connectTimeout: TimeSpan.FromSeconds(3));

        await AssertTimedOutLineAsync(connector, maxTime.Token, "Resolving timed out after 3000 milliseconds", new ConnectTarget("slow.example", 80, UseTls: false));
    }

    [TestMethod]
    [DataRow(3000, 2499, DisplayName = "more than 500 ms early")]
    [DataRow(1000, 749, DisplayName = "more than a quarter early")]
    public async Task ConnectAsync_WhenTheCallerCancelsWellBeforeTheLimit_LetsTheCancellationEscape(int limit, int elapsed)
    {
        using var caller = new CancellationTokenSource();
        var events = new RecordingTransferEvents();
        var time = new ManualTimeProvider();
        var dialer = new StallingTcpDialer { OnStalled = () => AdvanceThenCancel(time, elapsed, caller) };
        var connector = Connector(dialer, time, limit);
        Diagnostics.Arrange("elapsed of limit", $"{elapsed} of {limit} ms");

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await connector.ConnectAsync(Unreachable with { Events = events }, caller.Token));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("last line", "  Trying 192.0.2.20:1...", events.Info[^1]);
        CollectionAssert.AreEqual(new[] { "  Trying 192.0.2.20:1..." }, events.Info);
    }

    private static TcpConnector Connector(ITcpDialer dialer, TimeProvider time, int limit) =>
        new(new FakeDnsResolver(IPAddress.Parse("192.0.2.20")), dialer, new FakeTlsProvider(), time, connectTimeout: TimeSpan.FromMilliseconds(limit));

    private static void AdvanceThenCancel(ManualTimeProvider time, int milliseconds, CancellationTokenSource caller)
    {
        time.Advance(milliseconds);
        caller.Cancel();
    }

    private async Task AssertTimedOutLineAsync(TcpConnector connector, CancellationToken maxTime, string line, ConnectTarget? target = null)
    {
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("expected line", line);

        var result = await connector.ConnectAsync((target ?? Unreachable) with { Events = events }, maxTime);

        Diagnostics.Act("error message", result.ErrorMessage);
        Diagnostics.Assert("result.ExitCode", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(line, result.ErrorMessage);
        Assert.AreEqual(line, events.Info[^1]);
    }

    /// <summary>A resolver that never answers, until its token is cancelled.</summary>
    private sealed class StallingDnsResolver(Action onStalled) : IDnsResolver
    {
        public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            onStalled();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("An infinite delay ended without being cancelled.");
        }
    }

    /// <summary>
    /// A clock in milliseconds whose one timer the test fires while the clock still reads less than
    /// its due time, as a Linux timer on the coarse monotonic clock can.
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
            // The connector's limit is the first timer a connect creates; any later one never fires.
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
