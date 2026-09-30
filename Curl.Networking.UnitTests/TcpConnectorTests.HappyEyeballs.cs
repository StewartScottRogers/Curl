using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="TcpConnector" />'s happy eyeballs (<see cref="AddressFamilyRace" />, ADR-0254)
/// against curl 8.21.0 (mingw, Schannel), measured on 2026-09-29 with <c>Record-CurlExchange.ps1</c>
/// listening on one family of <c>localhost</c> only (BL-644 Notes): with
/// <c>--happy-eyeballs-timeout-ms 50</c> and IPv4 listening, <c>Trying [::1]</c> is followed 50 ms
/// later by <c>Trying 127.0.0.1</c>, which wins, and the abandoned IPv6 attempt prints nothing; with
/// <c>5000</c>, <c>::1</c> is refused after about 2 seconds and IPv4 is tried at once; with <c>0</c>
/// both are tried together.
/// </summary>
public sealed partial class TcpConnectorTests
{
    private const int HappyEyeballsPort = 18644;

    private static readonly IPEndPoint IPv6Attempt = new(IPAddress.IPv6Loopback, HappyEyeballsPort);

    private static readonly IPEndPoint IPv4Attempt = new(Loopback, HappyEyeballsPort);

    private static readonly IPEndPoint SecondIPv6Attempt = new(IPAddress.Parse("::2"), HappyEyeballsPort);

    [TestMethod]
    public async Task ConnectAsync_WhenTheFirstFamilyIsSlow_DialsTheOtherOnceTheHappyEyeballsTimeoutPasses()
    {
        var time = new ManualTimeProvider();
        var dialer = new GatedTcpDialer(time);
        var events = new RecordingTransferEvents();
        var connector = HappyEyeballsConnector(dialer, time, TimeSpan.FromMilliseconds(50), IPAddress.IPv6Loopback, Loopback);

        var connecting = connector.ConnectAsync(DualTarget(events), CancellationToken.None).AsTask();
        time.Advance(49);
        Assert.HasCount(1, dialer.Dials);
        time.Advance(1);
        await dialer.WaitForDialsAsync(2);
        var winner = new StallingConnection();
        dialer.Connect(IPv4Attempt, winner);
        var result = await connecting;

        Assert.AreSame(winner, result.Connection);
        CollectionAssert.AreEqual(new[] { (IPv6Attempt, 0L), (IPv4Attempt, 50L) }, dialer.Dials);
        CollectionAssert.AreEqual(new[] { "  Trying [::1]:18644...", "  Trying 127.0.0.1:18644..." }, DialLines(events));
        Assert.AreEqual(0, time.PendingTimerCount);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheFirstFamilyConnectsInTime_NeverDialsTheOther()
    {
        var time = new ManualTimeProvider();
        var dialer = new GatedTcpDialer(time);
        var connector = HappyEyeballsConnector(dialer, time, null, IPAddress.IPv6Loopback, Loopback);

        var connecting = connector.ConnectAsync(DualTarget(new RecordingTransferEvents()), CancellationToken.None).AsTask();
        time.Advance(199);
        var winner = new StallingConnection();
        dialer.Connect(IPv6Attempt, winner);
        var result = await connecting;
        time.Advance(1);

        Assert.AreSame(winner, result.Connection);
        CollectionAssert.AreEqual(new[] { (IPv6Attempt, 0L) }, dialer.Dials);
        Assert.AreEqual(0, time.PendingTimerCount);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheFirstFamilyFailsBeforeTheTimeout_DialsTheOtherAtOnce()
    {
        var time = new ManualTimeProvider();
        var dialer = new GatedTcpDialer(time);
        var events = new RecordingTransferEvents();
        var connector = HappyEyeballsConnector(dialer, time, TimeSpan.FromMilliseconds(5000), IPAddress.IPv6Loopback, Loopback);

        var connecting = connector.ConnectAsync(DualTarget(events), CancellationToken.None).AsTask();
        time.Advance(2000);
        dialer.Refuse(IPv6Attempt);
        await dialer.WaitForDialsAsync(2);
        var winner = new StallingConnection();
        dialer.Connect(IPv4Attempt, winner);
        var result = await connecting;

        Assert.AreSame(winner, result.Connection);
        CollectionAssert.AreEqual(new[] { (IPv6Attempt, 0L), (IPv4Attempt, 2000L) }, dialer.Dials);
        CollectionAssert.AreEqual(
            new[] { "  Trying [::1]:18644...", $"connect to ::1 port 18644 from :: port 0 failed: {RefusedReason()}", "  Trying 127.0.0.1:18644..." },
            DialLines(events));
    }

    [TestMethod]
    public async Task ConnectAsync_WithAZeroTimeout_DialsBothFamiliesTogether()
    {
        var time = new ManualTimeProvider();
        var dialer = new GatedTcpDialer(time);
        var connector = HappyEyeballsConnector(dialer, time, TimeSpan.Zero, IPAddress.IPv6Loopback, Loopback);

        var connecting = connector.ConnectAsync(DualTarget(new RecordingTransferEvents()), CancellationToken.None).AsTask();
        await dialer.WaitForDialsAsync(2);
        var winner = new StallingConnection();
        dialer.Connect(IPv6Attempt, winner);
        var result = await connecting;

        Assert.AreSame(winner, result.Connection);
        CollectionAssert.AreEqual(new[] { (IPv6Attempt, 0L), (IPv4Attempt, 0L) }, dialer.Dials);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheFirstAddressIsIPv4_RacesIPv6Second()
    {
        var time = new ManualTimeProvider();
        var dialer = new GatedTcpDialer(time);
        var connector = HappyEyeballsConnector(dialer, time, TimeSpan.FromMilliseconds(50), Loopback, IPAddress.IPv6Loopback);

        var connecting = connector.ConnectAsync(DualTarget(new RecordingTransferEvents()), CancellationToken.None).AsTask();
        time.Advance(50);
        await dialer.WaitForDialsAsync(2);
        var winner = new StallingConnection();
        dialer.Connect(IPv6Attempt, winner);
        var result = await connecting;

        Assert.AreSame(winner, result.Connection);
        CollectionAssert.AreEqual(new[] { (IPv4Attempt, 0L), (IPv6Attempt, 50L) }, dialer.Dials);
    }

    [TestMethod]
    public async Task ConnectAsync_WithTwoAddressesOfTheFirstFamily_TriesThemInTurnBeforeTheOtherFamilyIsDue()
    {
        var time = new ManualTimeProvider();
        var dialer = new GatedTcpDialer(time);
        var connector = HappyEyeballsConnector(dialer, time, TimeSpan.FromMilliseconds(200), IPAddress.IPv6Loopback, Loopback, IPAddress.Parse("::2"));

        var connecting = connector.ConnectAsync(DualTarget(new RecordingTransferEvents()), CancellationToken.None).AsTask();
        time.Advance(10);
        dialer.Refuse(IPv6Attempt);
        await dialer.WaitForDialsAsync(2);
        time.Advance(190);
        await dialer.WaitForDialsAsync(3);
        var winner = new StallingConnection();
        dialer.Connect(SecondIPv6Attempt, winner);
        var result = await connecting;

        Assert.AreSame(winner, result.Connection);
        CollectionAssert.AreEqual(new[] { (IPv6Attempt, 0L), (SecondIPv6Attempt, 10L), (IPv4Attempt, 200L) }, dialer.Dials);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenAnAbandonedAttemptConnectsAnyway_ClosesIt()
    {
        var time = new ManualTimeProvider();
        var dialer = new GatedTcpDialer(time) { IgnoresCancellation = true };
        var connector = HappyEyeballsConnector(dialer, time, TimeSpan.Zero, IPAddress.IPv6Loopback, Loopback);

        var connecting = connector.ConnectAsync(DualTarget(new RecordingTransferEvents()), CancellationToken.None).AsTask();
        await dialer.WaitForDialsAsync(2);
        var winner = new StallingConnection();
        var loser = new StallingConnection();
        dialer.Connect(IPv4Attempt, winner);
        dialer.Connect(IPv6Attempt, loser);
        var result = await connecting;

        Assert.AreSame(winner, result.Connection);
        Assert.IsTrue(loser.IsDisposed);
        Assert.IsFalse(winner.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenEveryAddressOfBothFamiliesFails_FailsWithExit7AfterTheLastFailure()
    {
        var time = new ManualTimeProvider();
        var dialer = new GatedTcpDialer(time);
        var events = new RecordingTransferEvents();
        var connector = HappyEyeballsConnector(dialer, time, TimeSpan.FromMilliseconds(50), IPAddress.IPv6Loopback, Loopback);

        var connecting = connector.ConnectAsync(DualTarget(events), CancellationToken.None).AsTask();
        time.Advance(50);
        await dialer.WaitForDialsAsync(2);
        dialer.Refuse(IPv4Attempt);
        time.Advance(1950);
        dialer.Refuse(IPv6Attempt);
        var result = await connecting;

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
        Assert.AreEqual("Failed to connect to dual.example:18644 after 2000 ms: Could not connect to server", result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[]
            {
                "  Trying [::1]:18644...",
                "  Trying 127.0.0.1:18644...",
                $"connect to 127.0.0.1 port 18644 from 0.0.0.0 port 0 failed: {RefusedReason()}",
                $"connect to ::1 port 18644 from :: port 0 failed: {RefusedReason()}",
            },
            DialLines(events));
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheCallerCancelsWhileTheFirstFamilyRunsAlone_LetsTheCancellationEscape()
    {
        using var caller = new CancellationTokenSource();
        var time = new ManualTimeProvider();
        var dialer = new GatedTcpDialer(time);
        var connector = HappyEyeballsConnector(dialer, time, TimeSpan.FromMilliseconds(200), IPAddress.IPv6Loopback, Loopback);

        var connecting = connector.ConnectAsync(DualTarget(new RecordingTransferEvents()), caller.Token).AsTask();
        await caller.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await connecting);
        CollectionAssert.AreEqual(new[] { (IPv6Attempt, 0L) }, dialer.Dials);
        Assert.AreEqual(0, time.PendingTimerCount);
    }

    [TestMethod]
    public void HappyEyeballsTimeout_NotGiven_IsCurls200Milliseconds()
    {
        var connector = new TcpConnector(new FakeDnsResolver(), new FakeTcpDialer(), new FakeTlsProvider(), TimeProvider.System);

        Assert.AreEqual(TimeSpan.FromMilliseconds(200), TcpConnector.DefaultHappyEyeballsTimeout);
        Assert.AreEqual(TcpConnector.DefaultHappyEyeballsTimeout, connector.HappyEyeballsTimeout);
    }

    [TestMethod]
    [DataRow(-5L, 0L, DisplayName = "negative held to zero")]
    [DataRow(0L, 0L, DisplayName = "zero")]
    [DataRow(50L, 50L, DisplayName = "50")]
    [DataRow(4294967294L, 4294967294L, DisplayName = "the longest timer")]
    [DataRow(9_000_000_000L, 4294967294L, DisplayName = "past the longest timer")]
    public void HappyEyeballsTimeout_Given_IsHeldToWhatATimerTakes(long givenMilliseconds, long expectedMilliseconds)
    {
        var connector = new TcpConnector(
            new FakeDnsResolver(), new FakeTcpDialer(), new FakeTlsProvider(), TimeProvider.System, happyEyeballsTimeout: TimeSpan.FromMilliseconds(givenMilliseconds));

        Assert.AreEqual(TimeSpan.FromMilliseconds(expectedMilliseconds), connector.HappyEyeballsTimeout);
    }

    private static TcpConnector HappyEyeballsConnector(GatedTcpDialer dialer, TimeProvider time, TimeSpan? happyEyeballsTimeout, params IPAddress[] addresses) =>
        new(new FakeDnsResolver(addresses), dialer, new FakeTlsProvider(), time, happyEyeballsTimeout: happyEyeballsTimeout);

    private static ConnectTarget DualTarget(RecordingTransferEvents events) =>
        new("dual.example", HappyEyeballsPort, UseTls: false) { Events = events };

    private static string[] DialLines(RecordingTransferEvents events) =>
        [.. events.Info.Where(line => line.StartsWith("  Trying ", StringComparison.Ordinal) || line.StartsWith("connect to ", StringComparison.Ordinal))];

    private static string RefusedReason() =>
        ConnectFailureReason.Describe(new SocketException((int)SocketError.ConnectionRefused), OperatingSystem.IsWindows());
}
