using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins how an <c>ftp://</c> transfer ends when <c>--connect-timeout</c> or <c>-m</c> runs out
/// (BL-512), on a <see cref="SteppingTimeProvider" /> so no second is real. Measured on
/// 2026-09-28 with curl 8.21.0 (mingw, Schannel) through <c>Record-CurlExchange.ps1 -Ftp</c>
/// (BL-512 Notes): <c>--connect-timeout 1</c> against a server that sent no greeting, or no
/// reply to <c>USER</c>, ended with exit 28 and <c>Operation timed out after 1011 milliseconds
/// with 0 bytes received</c>, since curl holds every state before <c>DO</c> to the connect
/// timeout; a stall after <c>PWD</c> is not held to it. <c>-m</c> belongs to the runner
/// (ADR-0117), so the handler reports the transfer started once the control connection is up,
/// which gives the runner's message its <c>Operation timed out</c> wording, and lets the
/// transfer's cancellation out.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerTimeLimitTests
{
    private const string Url = "ftp://127.0.0.1:47904/f.txt";

    private const string Greeting = "220 Recorder ready\r\n";

    private const string LoggedIn = Greeting + "331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    private readonly SteppingTimeProvider clock = new();

    [TestMethod]
    public async Task ExecuteAsync_NoGreetingBeforeTheConnectTimeout_EndsWithExit28AndCurlsOperationMessage()
    {
        // curl -m 1 / --connect-timeout 1 ftp://127.0.0.1:47903/f.txt, GREETING=STALL.
        var control = new StallingConnection();
        Task<TransferResult> run = StartAsync(control, OneSecond);
        await control.Stalled;

        clock.Advance(OneSecond);

        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.OperationTimedOut, "Operation timed out after 1000 milliseconds with 0 bytes received"),
            (await run) with { Report = null });
        Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(control.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_UserUnansweredBeforeTheConnectTimeout_EndsWithExit28AndNoQuit()
    {
        // curl --connect-timeout 1 ftp://127.0.0.1:47904/f.txt, USER=STALL.
        var control = new StallingConnection(Encoding.Latin1.GetBytes(Greeting));
        Task<TransferResult> run = StartAsync(control, OneSecond);
        await control.Stalled;

        clock.Advance(OneSecond);

        TransferResult result = await run;
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.OperationTimedOut, "Operation timed out after 1000 milliseconds with 0 bytes received"),
            result with { Report = null });
        Assert.AreEqual(220, result.Report!.ResponseCode);
        Assert.AreEqual("USER anonymous\r\n", Encoding.Latin1.GetString(control.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_LoginStalledShortOfTheConnectTimeout_IsStillRunning()
    {
        var control = new StallingConnection(Encoding.Latin1.GetBytes(Greeting));
        Task<TransferResult> run = StartAsync(control, OneSecond);
        await control.Stalled;

        clock.Advance(TimeSpan.FromMilliseconds(999));
        Assert.IsFalse(run.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.AreEqual(CurlExitCode.OperationTimedOut, (await run).ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoConnectTimeout_HoldsTheLoginToCurls300Seconds()
    {
        var control = new StallingConnection(Encoding.Latin1.GetBytes(Greeting));
        Task<TransferResult> run = StartAsync(control, connectTimeout: null);
        await control.Stalled;

        clock.Advance(TimeSpan.FromSeconds(299));
        Assert.IsFalse(run.IsCompleted);
        clock.Advance(OneSecond);

        Assert.AreEqual("Operation timed out after 300000 milliseconds with 0 bytes received", (await run).ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ZeroConnectTimeout_HoldsTheLoginToCurls300Seconds()
    {
        var control = new StallingConnection(Encoding.Latin1.GetBytes(Greeting));
        Task<TransferResult> run = StartAsync(control, TimeSpan.Zero);
        await control.Stalled;

        clock.Advance(TimeSpan.FromSeconds(300));

        Assert.AreEqual("Operation timed out after 300000 milliseconds with 0 bytes received", (await run).ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_TimerFiresEarly_WaitsOutTheRestOfTheConnectTimeout()
    {
        var control = new StallingConnection(Encoding.Latin1.GetBytes(Greeting));
        Task<TransferResult> run = StartAsync(control, OneSecond);
        await control.Stalled;

        clock.Advance(TimeSpan.FromMilliseconds(400));
        clock.FireEveryTimerNow();
        Assert.IsFalse(run.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(600));

        Assert.AreEqual("Operation timed out after 1000 milliseconds with 0 bytes received", (await run).ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ControlConnectOutlastsTheConnectTimeout_EndsAtTheFirstStall()
    {
        // The limit counts from the start of the request, before the TCP connect.
        var control = new StallingConnection();
        var connector = new ClockAdvancingConnector(clock, TimeSpan.FromSeconds(2), control);
        Task<TransferResult> run = StartAsync(connector, OneSecond, NoTransferProgress.Instance, CancellationToken.None);
        await control.Stalled;

        clock.Advance(TimeSpan.Zero);

        Assert.AreEqual("Operation timed out after 2000 milliseconds with 0 bytes received", (await run).ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_StalledAfterTheEntryPath_IsNotHeldToTheConnectTimeout()
    {
        // Past PWD curl is in DO, which only -m limits: the stall ends with the transfer's
        // cancellation, which the runner turns into its message.
        using var cancellation = new CancellationTokenSource();
        var control = new StallingConnection(Encoding.Latin1.GetBytes(LoggedIn));
        Task<TransferResult> run = StartAsync(new QueuedConnector(ConnectResult.Connected(control)), OneSecond, NoTransferProgress.Instance, cancellation.Token);
        await control.Stalled;

        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.IsFalse(run.IsCompleted);
        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => run);
        Assert.AreEqual("USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nEPSV\r\n", Encoding.Latin1.GetString(control.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledDuringTheLogin_LetsTheCancellationOut()
    {
        // -m running out first is the runner's to report.
        using var cancellation = new CancellationTokenSource();
        var control = new StallingConnection(Encoding.Latin1.GetBytes(Greeting));
        Task<TransferResult> run = StartAsync(new QueuedConnector(ConnectResult.Connected(control)), OneSecond, NoTransferProgress.Instance, cancellation.Token);
        await control.Stalled;

        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => run);
    }

    [TestMethod]
    public async Task ExecuteAsync_ControlConnected_ReportsTheTransferStartedBeforeTheGreeting()
    {
        // curl draws its progress meter, and -m's message says "Operation", from the TCP connect on.
        var progress = new RecordingProgress();
        var control = new StallingConnection();
        Task<TransferResult> run = StartAsync(new QueuedConnector(ConnectResult.Connected(control)), OneSecond, progress, CancellationToken.None);
        await control.Stalled;

        Assert.IsTrue(progress.Started);
        clock.Advance(OneSecond);
        await run;
    }

    [TestMethod]
    public async Task ExecuteAsync_StalledMidRetrieve_ReportsTheBytesReceivedAndLetsTheCancellationOut()
    {
        // curl -m 1 ftp://127.0.0.1:47907/f.txt, SIZE=213 100 and 5 bytes sent before the
        // data connection stalled: "with 5 out of 100 bytes received", from these reports.
        using var cancellation = new CancellationTokenSource();
        var progress = new RecordingProgress();
        var control = new StallingConnection(Encoding.Latin1.GetBytes(
            LoggedIn + "229 Entering Extended Passive Mode (|||63829|)\r\n200 Type set\r\n213 100\r\n150 Opening BINARY mode data connection\r\n"));
        var data = new StallingConnection("hello"u8.ToArray());
        var connector = new QueuedConnector(ConnectResult.Connected(control), ConnectResult.Connected(data));
        Task<TransferResult> run = StartAsync(connector, OneSecond, progress, cancellation.Token);
        await data.Stalled;

        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => run);
        CollectionAssert.AreEqual(new[] { (5L, (long?)100) }, progress.Downloaded);
    }

    private Task<TransferResult> StartAsync(StallingConnection control, TimeSpan? connectTimeout) =>
        StartAsync(new QueuedConnector(ConnectResult.Connected(control)), connectTimeout, NoTransferProgress.Instance, CancellationToken.None);

    private Task<TransferResult> StartAsync(IConnector connector, TimeSpan? connectTimeout, ITransferProgress progress, CancellationToken cancellationToken)
    {
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = new MemoryStream(),
            ConnectTimeout = connectTimeout,
            TimeProvider = clock,
            Progress = progress,
            CancellationToken = cancellationToken,
        };
        return new FtpProtocolHandler(connector).ExecuteAsync(context).AsTask();
    }

    /// <summary>A connector whose connect takes <paramref name="connectTime" /> on the clock.</summary>
    private sealed class ClockAdvancingConnector(SteppingTimeProvider clock, TimeSpan connectTime, IConnection connection) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            clock.Advance(connectTime);
            return ValueTask.FromResult(ConnectResult.Connected(connection));
        }
    }
}
