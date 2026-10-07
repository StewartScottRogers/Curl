using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Adversarial black-box tests (BL-1508, by <c>Documentation/Wiki/Adversarial-Testing.md</c>):
/// <see cref="FtpProtocolHandler" /> attacked through its public surface and the scripted
/// control and data connections, with reply codes and passive ports at their limits,
/// malformed and unterminated replies, replies valid only in another state, URL paths
/// carrying control characters, and replies split byte by byte and transfers run twice.
/// The oracle is curl 8.21.0, measured with <c>Record-CurlExchange.ps1 -Ftp</c>, and the
/// handler's documented contract where curl shows no direct answer.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerAdversarialTests
{
    private const string Url = "ftp://127.0.0.1:18321/file.txt";

    private const string Greeting = "220 Recorder ready\r\n";

    private const string LoggedIn = Greeting + "331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string Epsv = "229 Entering Extended Passive Mode (|||61744|)\r\n";

    private const string TypeSet = "200 Type set\r\n";

    private const string Opening = "150 Opening BINARY mode data connection\r\n";

    private const string Complete = "226 Transfer complete\r\n";

    private const string Bye = "221 Bye\r\n";

    private const string LoginSent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\n";

    private const string DownloadSent = LoginSent + "EPSV\r\nTYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\nQUIT\r\n";

    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // Boundaries

    [TestMethod]
    [DataRow("199 Odd\r\n", 199)]
    [DataRow("300 Odd\r\n", 300)]
    [DataRow("600 Odd\r\n", 600)]
    [DataRow("999 Odd\r\n", 999)]
    public async Task ExecuteAsync_GreetingCodeOutsideTwoHundreds_FailsWithExit8AndSendsNothing(string greeting, int code)
    {
        FtpRun run = await RunAsync(greeting);

        AssertFailure(run, CurlExitCode.WeirdServerReply, $"Got a {code} ftp-server response when 220 was expected");
        Assert.AreEqual(string.Empty, run.Sent);
    }

    [TestMethod]
    [DataRow(65535)]
    [DataRow(1)]
    public async Task ExecuteAsync_EpsvPortAtTheEdgesOfItsRange_DialsThatPort(int port)
    {
        string replies = LoggedIn + $"229 Entering Extended Passive Mode (|||{port}|)\r\n" + TypeSet + "213 1\r\n" + Opening + Complete + Bye;

        FtpRun run = await RunAsync(replies, "x");

        Diagnostics.Act("data target", run.Connector.Targets[1]);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
        Assert.AreEqual(port, run.Connector.Targets[1].Port);
    }

    [TestMethod]
    [DataRow("65536")]
    [DataRow("99999")]
    [DataRow("2147483648")]
    [DataRow("99999999999999999999999999")]
    public async Task ExecuteAsync_EpsvPortPast65535_FailsWithExit13IllegalPortAndDialsNothing(string port)
    {
        string replies = LoggedIn + $"229 Entering Extended Passive Mode (|||{port}|)\r\n" + Bye;

        FtpRun run = await RunAsync(replies);

        AssertFailure(run, CurlExitCode.FtpWeirdPasvReply, "Illegal port number in EPSV reply");
        Assert.HasCount(1, run.Connector.Targets);
    }

    [TestMethod]
    [DataRow("0,1", 1)]
    [DataRow("255,255", 65535)]
    public async Task ExecuteAsync_PasvPortAtTheEdgesOfItsRange_DialsThatPortOnTheControlHost(string portNumbers, int port)
    {
        string replies = LoggedIn + "500 no EPSV\r\n" + $"227 Entering Passive Mode (127,0,0,1,{portNumbers})\r\n" + TypeSet + "213 1\r\n" + Opening + Complete + Bye;

        FtpRun run = await RunAsync(replies, "x");

        Assert.AreEqual(TransferResult.Success(1), run.Result);
        Assert.AreEqual(port, run.Connector.Targets[1].Port);
    }

    [TestMethod]
    [DataRow("127,0,0,1,256,0")]
    [DataRow("127,0,0,1,0,256")]
    [DataRow("256,0,0,1,4,1")]
    [DataRow("127,0,0,1,4")]
    [DataRow("127,0,0,1,4,")]
    [DataRow("")]
    public async Task ExecuteAsync_PasvNumbersOutOfRangeOrMissing_FailsWithExit14(string numbers)
    {
        string replies = LoggedIn + "500 no EPSV\r\n" + $"227 Entering Passive Mode ({numbers})\r\n" + Bye;

        FtpRun run = await RunAsync(replies);

        AssertFailure(run, CurlExitCode.FtpWeird227Format, "Could not interpret the 227-response");
        Assert.HasCount(1, run.Connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyLineOf65534BytesBeforeItsLineFeed_IsRead()
    {
        string greeting = "220 " + new string('a', 65530) + "\n";

        FtpRun run = await RunAsync(greeting + LoggedIn[Greeting.Length..] + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + Bye, "x");

        Diagnostics.ActRun(run);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
        Assert.AreEqual(DownloadSent, run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyLineOf65535BytesBeforeItsLineFeed_FailsWithExit100()
    {
        string greeting = "220 " + new string('a', 65531) + "\n";

        FtpRun run = await RunAsync(greeting + Bye);

        AssertFailure(run, CurlExitCode.TooLarge, "A value or data field grew larger than allowed");
        Assert.AreEqual(string.Empty, run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeFromOneByteBeforeTheEnd_SendsRestAndWritesTheLastByte()
    {
        string replies = LoggedIn + Epsv + TypeSet + "213 10\r\n350 Restarting\r\n" + Opening + Complete + Bye;

        FtpRun run = await RunAsync(replies, "9", context => context.ResumeFrom = 9);

        Diagnostics.ActRun(run);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.Contains("REST 9\r\nRETR file.txt\r\n", run.Sent);
        Assert.AreEqual("9", run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeFromLongMaxValue_FailsWithExit36BeyondTheSize()
    {
        string replies = LoggedIn + Epsv + TypeSet + "213 10\r\n" + Bye;

        FtpRun run = await RunAsync(replies, context => context.ResumeFrom = long.MaxValue);

        Diagnostics.ActRun(run);
        Assert.AreEqual(CurlExitCode.BadDownloadResume, run.Result.ExitCode);
        Assert.DoesNotContain("RETR", run.Sent);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    // Malformed input

    [TestMethod]
    public async Task ExecuteAsync_MultiLineGreetingWithFakeLastLinesInside_WaitsForTheCodeAndSpace()
    {
        string greeting = "220-Welcome\r\n220\r\n22 short\r\n220-still going\r\n 220 indented\r\nabc def\r\n220 Ready\r\n";

        FtpRun run = await RunAsync(greeting + LoggedIn[Greeting.Length..] + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + Bye, "x");

        Diagnostics.ActRun(run);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
        Assert.AreEqual(DownloadSent, run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_LastLineEndedByBareLineFeeds_IsReadLikeCrLf()
    {
        string replies = (LoggedIn + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + Bye).Replace("\r\n", "\n", StringComparison.Ordinal);

        FtpRun run = await RunAsync(replies, "x");

        Diagnostics.ActRun(run);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
        Assert.AreEqual(DownloadSent, run.Sent);
    }

    [TestMethod]
    [DataRow("220 Ready")]
    [DataRow("220-Ready\r\n")]
    [DataRow("2")]
    [DataRow("")]
    public async Task ExecuteAsync_GreetingCutOffBeforeItsLastLineEnds_FailsWithExit56AndSendsNothing(string greeting)
    {
        FtpRun run = await RunAsync(greeting);

        AssertFailure(run, CurlExitCode.RecvError, "response reading failed (errno: 0)");
        Assert.AreEqual(string.Empty, run.Sent);
    }

    [TestMethod]
    [DataRow("2x0 Ready\r\n")]
    [DataRow("+20 Ready\r\n")]
    [DataRow(" 220 Ready\r\n")]
    [DataRow("220\tReady\r\n")]
    public async Task ExecuteAsync_GreetingWhoseCodeIsNotThreeDigitsAndASpace_IsNeverALastLine(string greeting)
    {
        FtpRun run = await RunAsync(greeting);

        AssertFailure(run, CurlExitCode.RecvError, "response reading failed (errno: 0)");
        Assert.AreEqual(string.Empty, run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_NulByteInsideAMultiLineReply_FailsWithExit8()
    {
        FtpRun run = await RunAsync("220-Wel\0come\r\n220 Ready\r\n");

        AssertFailure(run, CurlExitCode.WeirdServerReply, "Nul byte in server response line");
        Assert.AreEqual(string.Empty, run.Sent);
    }

    [TestMethod]
    [DataRow("229 Entering Extended Passive Mode |||61744|\r\n", "Weirdly formatted EPSV reply")]
    [DataRow("229 Entering Extended Passive Mode (|||)\r\n", "Weirdly formatted EPSV reply")]
    [DataRow("229 Entering Extended Passive Mode (||61744|)\r\n", "Weirdly formatted EPSV reply")]
    [DataRow("229 Entering Extended Passive Mode (|!|61744|)\r\n", "Weirdly formatted EPSV reply")]
    [DataRow("229 Entering Extended Passive Mode (|||-1|)\r\n", "Weirdly formatted EPSV reply")]
    [DataRow("229 Entering Extended Passive Mode (|||61744)\r\n", "Illegal port number in EPSV reply")]
    [DataRow("229 Entering Extended Passive Mode (|||61744!)\r\n", "Illegal port number in EPSV reply")]
    [DataRow("229 Entering Extended Passive Mode (|||61744", "Illegal port number in EPSV reply")]
    public async Task ExecuteAsync_EpsvReplyMalformed_FailsWithExit13AndDialsNothing(string epsv, string message)
    {
        string replies = LoggedIn + epsv.Replace("\r\n", string.Empty, StringComparison.Ordinal) + "\r\n" + Bye;

        FtpRun run = await RunAsync(replies);

        AssertFailure(run, CurlExitCode.FtpWeirdPasvReply, message);
        Assert.HasCount(1, run.Connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_PasvNamingAForeignAddress_DialsTheControlHostByDefault()
    {
        string replies = LoggedIn + "500 no EPSV\r\n227 Entering Passive Mode (203,0,113,9,4,1)\r\n" + TypeSet + "213 1\r\n" + Opening + Complete + Bye;

        FtpRun run = await RunAsync(replies, "x");

        Diagnostics.Act("data target", run.Connector.Targets[1]);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
        Assert.AreEqual("127.0.0.1", run.Connector.Targets[1].Host);
        Assert.AreEqual(1025, run.Connector.Targets[1].Port);
    }

    [TestMethod]
    [DataRow("213 abc\r\n")]
    [DataRow("213 -5\r\n")]
    [DataRow("213 99999999999999999999999\r\n")]
    [DataRow("213 \r\n")]
    public async Task ExecuteAsync_SizeReplyThatIsNotANumber_DownloadsEveryByteSent(string size)
    {
        string replies = LoggedIn + Epsv + TypeSet + size + Opening + Complete + Bye;

        FtpRun run = await RunAsync(replies, "hello");

        Diagnostics.ActRun(run);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual("hello", run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_SizeReplyLargerThanTheData_FailsWithExit18NamingTheBytesMissing()
    {
        string replies = LoggedIn + Epsv + TypeSet + "213 9\r\n" + Opening + Complete + Bye;

        FtpRun run = await RunAsync(replies, "hello");

        // curl 8.21.0 measured: exit 18 "transfer closed with 4 bytes remaining to read", hello written.
        AssertFailure(run, CurlExitCode.PartialFile, "transfer closed with 4 bytes remaining to read");
        Assert.AreEqual("hello", run.OutputText);
    }

    // Invalid partitions

    [TestMethod]
    [DataRow("/a%0D%0AQUIT")]
    [DataRow("/a%0ADELE%20x")]
    [DataRow("/a%0Db")]
    [DataRow("/a%00b")]
    [DataRow("/dir%0A/file.txt")]
    public async Task ExecuteAsync_PathCarryingAControlCharacter_FailsWithExit3BeforeAnyCommandCarriesIt(string path)
    {
        FtpRun run = await RunAsync(LoggedIn + Bye, url: "ftp://127.0.0.1:18321" + path);

        Diagnostics.ActRun(run);
        Assert.AreEqual(CurlExitCode.UrlMalformat, run.Result.ExitCode);
        Assert.DoesNotContain("DELE", run.Sent);
        Assert.DoesNotContain("RETR", run.Sent);
        Assert.DoesNotContain("CWD", run.Sent);
    }

    [TestMethod]
    [DataRow(FtpFileMethod.MultiCwd)]
    [DataRow(FtpFileMethod.SingleCwd)]
    [DataRow(FtpFileMethod.NoCwd)]
    public async Task ExecuteAsync_EveryFileMethodWithAnInjectedLineEnd_FailsWithExit3(FtpFileMethod method)
    {
        FtpRun run = await RunAsync(
            LoggedIn + Bye,
            adjust: context => context.FtpFileMethod = method,
            url: "ftp://127.0.0.1:18321/d%0D%0ADELE%20x/f");

        Diagnostics.ActRun(run);
        Assert.AreEqual(CurlExitCode.UrlMalformat, run.Result.ExitCode);
        Assert.DoesNotContain("DELE", run.Sent);
    }

    [TestMethod]
    [DataRow("226 Transfer complete\r\n", 226)]
    [DataRow("250 Done\r\n", 250)]
    [DataRow("200 OK\r\n", 200)]
    public async Task ExecuteAsync_RetrAnsweredWithACompletionBefore150_FailsWithExit19(string reply, int code)
    {
        string replies = LoggedIn + Epsv + TypeSet + "213 1\r\n" + reply + Bye;

        FtpRun run = await RunAsync(replies, "x");

        Diagnostics.ActRun(run);
        AssertFailure(run, CurlExitCode.FtpCouldntRetrFile, $"RETR response: {code}");
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    [DataRow("150 Opening\r\n")]
    [DataRow("331 Password required\r\n")]
    public async Task ExecuteAsync_GreetingThatIsAnotherStatesReply_FailsWithExit8(string greeting)
    {
        FtpRun run = await RunAsync(greeting);

        Diagnostics.ActRun(run);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        Assert.AreEqual(string.Empty, run.Sent);
    }

    // State and concurrency

    [TestMethod]
    public async Task ExecuteAsync_EveryReplyDeliveredOneBytePerRead_SendsTheSameCommandsAndBody()
    {
        byte[] replies = Encoding.Latin1.GetBytes(LoggedIn + Epsv + TypeSet + "213 5\r\n" + Opening + Complete + Bye);
        byte[][] reads = [.. replies.Select(single => new[] { single })];

        FtpRun run = await FtpRun.ExecuteAsync(Url, new ScriptedConnection(reads), new ScriptedConnection("he"u8.ToArray(), "l"u8.ToArray(), "lo"u8.ToArray()));

        Diagnostics.ActRun(run);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
        Assert.AreEqual(DownloadSent, run.Sent);
        Assert.AreEqual("hello", run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_ControlClosedBetweenTheTwoLinesOfAMultiLineReply_FailsWithExit56()
    {
        FtpRun run = await RunAsync(LoggedIn + "229-Entering\r\n");

        Diagnostics.ActRun(run);
        Assert.AreEqual(CurlExitCode.RecvError, run.Result.ExitCode);
        Assert.HasCount(1, run.Connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_SameHandlerRunTwice_GivesTheSameCommandsAndBodyBothTimes()
    {
        string replies = LoggedIn + Epsv + TypeSet + "213 5\r\n" + Opening + Complete + Bye;
        ScriptedConnection firstControl = Scripted(replies);
        ScriptedConnection secondControl = Scripted(replies);
        var connector = new ControlAndDataConnector(
            [ConnectResult.Connected(firstControl), ConnectResult.Connected(secondControl)],
            [ConnectResult.Connected(Scripted("hello")), ConnectResult.Connected(Scripted("hello"))]);
        var handler = new FtpProtocolHandler(connector);
        var firstOutput = new MemoryStream();
        var secondOutput = new MemoryStream();

        TransferResult first = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(Url), Output = firstOutput });
        TransferResult second = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(Url), Output = secondOutput });

        Assert.AreEqual(first with { Report = null }, second with { Report = null });
        Assert.AreEqual(CurlExitCode.Ok, second.ExitCode);
        Assert.AreEqual(DownloadSent, Encoding.Latin1.GetString(secondControl.Sent));
        CollectionAssert.AreEqual(firstOutput.ToArray(), secondOutput.ToArray());
        CollectionAssert.AreEqual("hello"u8.ToArray(), secondOutput.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_SixteenTransfersAtOnceOnOneHandler_EachWritesItsOwnBody()
    {
        const int count = 16;
        string replies = LoggedIn + Epsv + TypeSet + "213 5\r\n" + Opening + Complete + Bye;
        ConnectResult[] controls = [.. Enumerable.Range(0, count).Select(_ => ConnectResult.Connected(Scripted(replies)))];
        ConnectResult[] data = [.. Enumerable.Range(0, count).Select(_ => ConnectResult.Connected(Scripted("hello")))];
        var handler = new FtpProtocolHandler(new ControlAndDataConnector(controls, data));
        MemoryStream[] outputs = [.. Enumerable.Range(0, count).Select(_ => new MemoryStream())];

        TransferResult[] finished = await Task.WhenAll(outputs.Select(output =>
            Task.Run(async () => await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(Url), Output = output }))));

        Assert.IsTrue(finished.All(result => result.ExitCode == CurlExitCode.Ok), string.Join(",", finished.Select(result => result.ExitCode)));
        Assert.IsTrue(outputs.All(output => output.ToArray().SequenceEqual("hello"u8.ToArray())));
    }

    [TestMethod]
    public async Task ExecuteAsync_TokenCancelledBeforeTheCall_ThrowsOperationCanceledAndWritesNothing()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        string replies = LoggedIn + Epsv + TypeSet + "213 5\r\n" + Opening + Complete + Bye;
        var output = new MemoryStream();
        var connector = new ControlAndDataConnector([ConnectResult.Connected(Scripted(replies))], [ConnectResult.Connected(Scripted("hello"))]);
        var context = new TransferContext { Url = CurlUrl.Parse(Url), Output = output, CancellationToken = cancellation.Token };

        // The handler's contract: cancellation leaves as an OperationCanceledException (or a type derived from it).
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await new FtpProtocolHandler(connector).ExecuteAsync(context));

        Assert.AreEqual(0L, output.Length);
    }

    private static ScriptedConnection Scripted(string text) => new(Encoding.Latin1.GetBytes(text));

    private static void AssertFailure(FtpRun run, CurlExitCode exitCode, string message)
    {
        Assert.AreEqual(exitCode, run.Result.ExitCode, run.Result.ErrorMessage);
        Assert.AreEqual(message, run.Result.ErrorMessage);
    }

    private Task<FtpRun> RunAsync(string replies, Action<MutableContext> adjust) => RunAsync(replies, string.Empty, adjust);

    private async Task<FtpRun> RunAsync(string replies, string data = "", Action<MutableContext>? adjust = null, string url = Url)
    {
        Diagnostics.ArrangeFtp(url, replies, data);
        FtpRun run = await FtpRun.ExecuteAsync(url, replies, data, context => MutableContext.Build(context, adjust ?? (_ => { })));
        Diagnostics.ActRun(run);
        return run;
    }

    /// <summary>
    /// An <see cref="IConnector" /> that answers, from any thread, each connect to the URL's
    /// port with the next of <paramref name="controls" /> and every other connect with the
    /// next of <paramref name="data" />.
    /// </summary>
    private sealed class ControlAndDataConnector(ConnectResult[] controls, ConnectResult[] data) : IConnector
    {
        private int nextControl = -1;

        private int nextData = -1;

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(target.Port == 18321
                ? controls[Interlocked.Increment(ref nextControl)]
                : data[Interlocked.Increment(ref nextData)]);
    }
}
