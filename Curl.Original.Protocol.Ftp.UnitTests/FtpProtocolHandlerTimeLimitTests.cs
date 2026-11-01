using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Ftp;

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
    public TestContext TestContext { get; set; } = null!;

    private const string Url = "ftp://127.0.0.1:47904/f.txt";

    private const string Greeting = "220 Recorder ready\r\n";

    private const string LoggedIn = Greeting + "331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    private readonly SteppingTimeProvider clock = new();

    [TestMethod]
    public async Task ExecuteAsync_NoGreetingBeforeTheConnectTimeout_EndsWithExit28AndCurlsOperationMessage()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_NoGreetingBeforeTheConnectTimeout_EndsWithExit28AndCurlsOperationMessage");

        // curl -m 1 / --connect-timeout 1 ftp://127.0.0.1:47903/f.txt, GREETING=STALL.
        var control = new StallingConnection();
        Task<TransferResult> run = StartAsync(control, OneSecond);
        await control.Stalled;

        clock.Advance(OneSecond);

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.OperationTimedOut, "Operation timed out after 1000 milliseconds with 0 bytes received"),
            (await run) with { Report = null });
        Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(control.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_UserUnansweredBeforeTheConnectTimeout_EndsWithExit28AndNoQuit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_UserUnansweredBeforeTheConnectTimeout_EndsWithExit28AndNoQuit");

        // curl --connect-timeout 1 ftp://127.0.0.1:47904/f.txt, USER=STALL.
        var control = new StallingConnection(Encoding.Latin1.GetBytes(Greeting));
        Task<TransferResult> run = StartAsync(control, OneSecond);
        await control.Stalled;

        clock.Advance(OneSecond);

        TransferResult result = await run;
        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.OperationTimedOut, "Operation timed out after 1000 milliseconds with 0 bytes received"),
            result with { Report = null });
        Assert.AreEqual(220, result.Report!.ResponseCode);
        Assert.AreEqual("USER anonymous\r\n", Encoding.Latin1.GetString(control.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_LoginStalledShortOfTheConnectTimeout_IsStillRunning()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_LoginStalledShortOfTheConnectTimeout_IsStillRunning");

        var control = new StallingConnection(Encoding.Latin1.GetBytes(Greeting));
        Task<TransferResult> run = StartAsync(control, OneSecond);
        await control.Stalled;

        clock.Advance(TimeSpan.FromMilliseconds(999));
        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.IsFalse(run.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.AreEqual(CurlExitCode.OperationTimedOut, (await run).ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoConnectTimeout_HoldsTheLoginToCurls300Seconds()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_NoConnectTimeout_HoldsTheLoginToCurls300Seconds");

        var control = new StallingConnection(Encoding.Latin1.GetBytes(Greeting));
        Task<TransferResult> run = StartAsync(control, connectTimeout: null, serverResponseTimeout: TenMinutes);
        await control.Stalled;

        clock.Advance(TimeSpan.FromSeconds(299));
        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.IsFalse(run.IsCompleted);
        clock.Advance(OneSecond);

        Assert.AreEqual("Operation timed out after 300000 milliseconds with 0 bytes received", (await run).ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ZeroConnectTimeout_HoldsTheLoginToCurls300Seconds()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_ZeroConnectTimeout_HoldsTheLoginToCurls300Seconds");

        var control = new StallingConnection(Encoding.Latin1.GetBytes(Greeting));
        Task<TransferResult> run = StartAsync(control, TimeSpan.Zero, serverResponseTimeout: TenMinutes);
        await control.Stalled;

        clock.Advance(TimeSpan.FromSeconds(300));

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.AreEqual("Operation timed out after 300000 milliseconds with 0 bytes received", (await run).ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_TimerFiresEarly_WaitsOutTheRestOfTheConnectTimeout()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_TimerFiresEarly_WaitsOutTheRestOfTheConnectTimeout");

        var control = new StallingConnection(Encoding.Latin1.GetBytes(Greeting));
        Task<TransferResult> run = StartAsync(control, OneSecond);
        await control.Stalled;

        clock.Advance(TimeSpan.FromMilliseconds(400));
        clock.FireEveryTimerNow();
        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.IsFalse(run.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(600));

        Assert.AreEqual("Operation timed out after 1000 milliseconds with 0 bytes received", (await run).ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ControlConnectOutlastsTheConnectTimeout_EndsAtTheFirstStall()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_ControlConnectOutlastsTheConnectTimeout_EndsAtTheFirstStall");

        // The limit counts from the start of the request, before the TCP connect.
        var control = new StallingConnection();
        var connector = new ClockAdvancingConnector(clock, TimeSpan.FromSeconds(2), control);
        Task<TransferResult> run = StartAsync(connector, OneSecond, NoTransferProgress.Instance, CancellationToken.None);
        await control.Stalled;

        clock.Advance(TimeSpan.Zero);

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.AreEqual("Operation timed out after 2000 milliseconds with 0 bytes received", (await run).ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_StalledAfterTheEntryPath_IsNotHeldToTheConnectTimeout()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_StalledAfterTheEntryPath_IsNotHeldToTheConnectTimeout");

        // Past PWD curl is in DO, which only -m limits: the stall ends with the transfer's
        // cancellation, which the runner turns into its message.
        using var cancellation = new CancellationTokenSource();
        var control = new StallingConnection(Encoding.Latin1.GetBytes(LoggedIn));
        Task<TransferResult> run = StartAsync(new QueuedConnector(ConnectResult.Connected(control)), OneSecond, NoTransferProgress.Instance, cancellation.Token);
        await control.Stalled;

        clock.Advance(TimeSpan.FromSeconds(10));
        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.IsFalse(run.IsCompleted);
        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => run);
        Assert.AreEqual("USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nEPSV\r\n", Encoding.Latin1.GetString(control.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledDuringTheLogin_LetsTheCancellationOut()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_CancelledDuringTheLogin_LetsTheCancellationOut");

        // -m running out first is the runner's to report.
        using var cancellation = new CancellationTokenSource();
        var control = new StallingConnection(Encoding.Latin1.GetBytes(Greeting));
        Task<TransferResult> run = StartAsync(new QueuedConnector(ConnectResult.Connected(control)), OneSecond, NoTransferProgress.Instance, cancellation.Token);
        await control.Stalled;

        await cancellation.CancelAsync();

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => run);
    }

    [TestMethod]
    public async Task ExecuteAsync_ControlConnected_ReportsTheTransferStartedBeforeTheGreeting()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_ControlConnected_ReportsTheTransferStartedBeforeTheGreeting");

        // curl draws its progress meter, and -m's message says "Operation", from the TCP connect on.
        var progress = new RecordingProgress();
        var control = new StallingConnection();
        Task<TransferResult> run = StartAsync(new QueuedConnector(ConnectResult.Connected(control)), OneSecond, progress, CancellationToken.None);
        await control.Stalled;

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.IsTrue(progress.Started);
        clock.Advance(OneSecond);
        await run;
    }

    [TestMethod]
    public async Task ExecuteAsync_StalledMidRetrieve_ReportsTheBytesReceivedAndLetsTheCancellationOut()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_StalledMidRetrieve_ReportsTheBytesReceivedAndLetsTheCancellationOut");

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

        diagnostics.Act("steps before the checks", "completed");
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => run);
        CollectionAssert.AreEqual(new[] { (5L, (long?)100) }, progress.Downloaded);
    }

    [TestMethod]
    public async Task ExecuteAsync_StopsBeforeEntryPath_SendsNoPwdAndWaitsUntilTheConnectTimeout()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_StopsBeforeEntryPath_SendsNoPwdAndWaitsUntilTheConnectTimeout");

        // Upstream's debug build under CURL_FTP_PWD_STOP: ftp_state_pwd sends nothing and leaves the state machine waiting (test753).
        var control = ScriptedConnection.FromReplies(LoggedIn);
        var context = new TransferContext { Url = CurlUrl.Parse(Url), Output = new MemoryStream(), ConnectTimeout = OneSecond, TimeProvider = clock };
        Task<TransferResult> run = new FtpProtocolHandler(new QueuedConnector(ConnectResult.Connected(control))) { StopsBeforeEntryPath = true }.ExecuteAsync(context).AsTask();
        bool stillWaiting = !run.IsCompleted;

        clock.Advance(OneSecond);
        TransferResult result = await run;
        diagnostics.Act("still waiting before the connect timeout", stillWaiting);
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.IsTrue(stillWaiting);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("USER anonymous\r\nPASS ftp@example.com\r\n", Encoding.Latin1.GetString(control.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_StorUnansweredFor60Seconds_EndsWithExit28ServerResponseTimeoutAndNoQuit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_StorUnansweredFor60Seconds_EndsWithExit28ServerResponseTimeoutAndNoQuit");

        // curl -T up.txt ftp://127.0.0.1:47924/x, STOR=STALL: curl 8.22.0 ends 60 s after STOR
        // with "curl: (28) server response timeout" and sends no QUIT (upstream case 594, BL-2724).
        var control = new StallingConnection(Encoding.Latin1.GetBytes(LoggedIn + "229 Entering Extended Passive Mode (|||64163|)\r\n200 Type set\r\n"));
        var connector = new QueuedConnector(ConnectResult.Connected(control), ConnectResult.Connected(new ScriptedConnection()));
        var context = new TransferContext { Url = CurlUrl.Parse(Url), Output = new MemoryStream(), Upload = new MemoryStream("hello"u8.ToArray()), TimeProvider = clock };
        Task<TransferResult> run = new FtpProtocolHandler(connector).ExecuteAsync(context).AsTask();
        await control.Stalled;

        clock.Advance(TimeSpan.FromMilliseconds(59999));
        bool stillWaiting = !run.IsCompleted;
        clock.Advance(TimeSpan.FromMilliseconds(1));
        TransferResult result = await run;

        diagnostics.Act("still waiting at 59.999 s", stillWaiting);
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.IsTrue(stillWaiting);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.OperationTimedOut, "server response timeout"), result with { Report = null });
        Assert.AreEqual("USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nEPSV\r\nTYPE I\r\nSTOR f.txt\r\n", Encoding.Latin1.GetString(control.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_ActiveStorUnansweredAndNeverConnected_EndsWithExit12AfterTheAcceptTimeoutAndAMinuteMoreWithNoQuit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_ActiveStorUnansweredAndNeverConnected_EndsWithExit12AfterTheAcceptTimeoutAndAMinuteMoreWithNoQuit");

        // curl -v -P - -T 594 ftp://127.0.0.1:47594/path/594, STOR=STALL: curl 8.22.0 ends
        // after 120128 ms with "* FTP response timeout", "* control connection looks dead" and
        // "curl: (12) Accept timeout occurred while waiting server connect", no QUIT (BL-2796).
        var control = new StallingConnection(Encoding.Latin1.GetBytes(LoggedIn + EprtOk + "200 Type set\r\n"));
        var events = new RecordingTransferEvents();
        Task<TransferResult> run = StartActiveUploadAsync(control, events, acceptTimeout: TransferContext.DefaultFtpAcceptTimeout);
        await control.Stalled;
        await clock.TimerArmedAt(TimeSpan.FromSeconds(60));

        clock.Advance(TimeSpan.FromMilliseconds(59999));
        bool waitingForTheAccept = !run.IsCompleted;
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await clock.TimerArmedAt(TimeSpan.FromSeconds(120));
        clock.Advance(TimeSpan.FromMilliseconds(59999));
        bool waitingForTheReply = !run.IsCompleted;
        clock.Advance(TimeSpan.FromMilliseconds(1));
        TransferResult result = await run;

        diagnostics.Act("result", result with { Report = null });
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.IsTrue(waitingForTheAccept);
        Assert.IsTrue(waitingForTheReply);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpAcceptTimeout, "Accept timeout occurred while waiting server connect"), result with { Report = null });
        Assert.AreEqual(ActiveUploadSent, Encoding.Latin1.GetString(control.Sent));
        CollectionAssert.IsSubsetOf(new[] { "FTP response timeout", "control connection looks dead" }, events.Info);
        Assert.AreEqual("control connection looks dead", events.Info[^1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_ActiveStorAnsweredAfterTheAcceptTimeout_EndsWithExit12AfterQuit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_ActiveStorAnsweredAfterTheAcceptTimeout_EndsWithExit12AfterQuit");

        // A reply within ftp_done's minute leaves curl's control connection valid, so QUIT is sent.
        var control = new ReleasableConnection(Encoding.Latin1.GetBytes(LoggedIn + EprtOk + "200 Type set\r\n"));
        var events = new RecordingTransferEvents();
        Task<TransferResult> run = StartActiveUploadAsync(control, events, acceptTimeout: TimeSpan.FromSeconds(8));
        await control.Stalled;
        await clock.TimerArmedAt(TimeSpan.FromSeconds(8));

        clock.Advance(TimeSpan.FromSeconds(8));
        await clock.TimerArmedAt(TimeSpan.FromSeconds(68));
        control.Release(Encoding.Latin1.GetBytes("150 Opening\r\n221 Bye\r\n"));
        TransferResult result = await run;

        diagnostics.Act("result", result with { Report = null });
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpAcceptTimeout, "Accept timeout occurred while waiting server connect"), result with { Report = null });
        Assert.AreEqual(ActiveUploadSent + "QUIT\r\n", Encoding.Latin1.GetString(control.Sent));
        CollectionAssert.DoesNotContain(events.Info, "control connection looks dead");
    }

    [TestMethod]
    public async Task ExecuteAsync_ActiveStorUnansweredWithALongerAcceptTimeout_EndsWithExit28ServerResponseTimeout()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_ActiveStorUnansweredWithALongerAcceptTimeout_EndsWithExit28ServerResponseTimeout");

        // The server response timeout running out before the accept timer is the passive ending.
        var control = new StallingConnection(Encoding.Latin1.GetBytes(LoggedIn + EprtOk + "200 Type set\r\n"));
        Task<TransferResult> run = StartActiveUploadAsync(control, new RecordingTransferEvents(), acceptTimeout: TimeSpan.FromSeconds(120));
        await control.Stalled;
        await clock.TimerArmedAt(TimeSpan.FromSeconds(60));

        clock.Advance(TimeSpan.FromSeconds(60));
        TransferResult result = await run;

        diagnostics.Act("result", result with { Report = null });
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.OperationTimedOut, "server response timeout"), result with { Report = null });
        Assert.AreEqual(ActiveUploadSent, Encoding.Latin1.GetString(control.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_UserUnansweredWithoutConnectTimeout_EndsWithExit28ServerResponseTimeoutAfter60Seconds()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_UserUnansweredWithoutConnectTimeout_EndsWithExit28ServerResponseTimeoutAfter60Seconds");

        // curl ftp://127.0.0.1:47925/f.txt, USER=STALL: curl 8.22.0 ends after 60630 ms with
        // "curl: (28) server response timeout", well before its 300 s connect timeout (BL-2724).
        var control = new StallingConnection(Encoding.Latin1.GetBytes(Greeting));
        Task<TransferResult> run = StartAsync(control, connectTimeout: null);
        await control.Stalled;

        clock.Advance(TimeSpan.FromSeconds(60));
        TransferResult result = await run;

        diagnostics.Act("result", result with { Report = null });
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.OperationTimedOut, "server response timeout"), result with { Report = null });
        Assert.AreEqual("USER anonymous\r\n", Encoding.Latin1.GetString(control.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerResponseTimeoutSet_EndsTheUnansweredUserAfterThatTimeout()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test", "ExecuteAsync_ServerResponseTimeoutSet_EndsTheUnansweredUserAfterThatTimeout");

        // CURLOPT_SERVER_RESPONSE_TIMEOUT 8 replaces curl's 60 seconds (BL-2724).
        var control = new StallingConnection(Encoding.Latin1.GetBytes(Greeting));
        Task<TransferResult> run = StartAsync(control, connectTimeout: null, serverResponseTimeout: TimeSpan.FromSeconds(8));
        await control.Stalled;

        clock.Advance(TimeSpan.FromSeconds(8));
        TransferResult result = await run;

        diagnostics.Act("result", result with { Report = null });
        diagnostics.Assert("outcome checked by the assertions below", "passes", "checking");
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.OperationTimedOut, "server response timeout"), result with { Report = null });
    }

    /// <summary>
    /// A server response timeout past curl's 300-second connect timeout, as
    /// <c>CURLOPT_SERVER_RESPONSE_TIMEOUT</c> 600 gives, so a stalled login reaches the connect
    /// timeout instead of curl's default 60-second response timeout (BL-2724).
    /// </summary>
    private static readonly TimeSpan TenMinutes = TimeSpan.FromMinutes(10);

    private const string EprtOk = "200 EPRT command successful\r\n";

    private const string ActiveUploadSent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nEPRT |1|127.0.0.1|52280|\r\nTYPE I\r\nSTOR f.txt\r\n";

    /// <summary>Starts <c>-P 127.0.0.1 -T</c> against a server that never connects to port 52280.</summary>
    private Task<TransferResult> StartActiveUploadAsync(IConnection control, RecordingTransferEvents events, TimeSpan acceptTimeout)
    {
        var pending = new ScriptedPendingConnection(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 52280), null);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = new MemoryStream(),
            Upload = new MemoryStream("hello"u8.ToArray()),
            FtpPort = "127.0.0.1",
            FtpAcceptTimeout = acceptTimeout,
            Events = events,
            TimeProvider = clock,
        };
        var handler = new FtpProtocolHandler(new QueuedConnector(ConnectResult.Connected(control)), new QueuedListener(ListenResult.Listening(pending)), new QueuedTlsProvider());
        return handler.ExecuteAsync(context).AsTask();
    }

    /// <summary>
    /// A control connection that, once its scripted reads run out, waits until the test
    /// releases the next bytes, then reads end of stream.
    /// </summary>
    private sealed class ReleasableConnection(byte[] firstRead) : IConnection
    {
        private readonly List<byte> sent = [];

        private readonly TaskCompletionSource stalled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<byte[]> released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int reads;

        public Task Stalled => stalled.Task;

        public bool IsSecure => false;

        public System.Net.EndPoint? RemoteEndPoint => null;

        public byte[] Sent => [.. sent];

        public void Release(byte[] bytes) => released.SetResult(bytes);

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            byte[] read = ++reads switch
            {
                1 => firstRead,
                2 => await WaitForReleaseAsync(cancellationToken),
                _ => [],
            };
            read.CopyTo(buffer);
            return read.Length;
        }

        private async Task<byte[]> WaitForReleaseAsync(CancellationToken cancellationToken)
        {
            stalled.TrySetResult();
            return await released.Task.WaitAsync(cancellationToken);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            sent.AddRange(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private Task<TransferResult> StartAsync(StallingConnection control, TimeSpan? connectTimeout, TimeSpan? serverResponseTimeout = null) =>
        StartAsync(new QueuedConnector(ConnectResult.Connected(control)), connectTimeout, NoTransferProgress.Instance, CancellationToken.None, serverResponseTimeout);

    private Task<TransferResult> StartAsync(IConnector connector, TimeSpan? connectTimeout, ITransferProgress progress, CancellationToken cancellationToken, TimeSpan? serverResponseTimeout = null)
    {
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = new MemoryStream(),
            ConnectTimeout = connectTimeout,
            TimeProvider = clock,
            Progress = progress,
            CancellationToken = cancellationToken,
            FtpServerResponseTimeout = serverResponseTimeout ?? TransferContext.DefaultFtpServerResponseTimeout,
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
