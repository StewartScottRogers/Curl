using System.Text;
using Curl.Conformance.LibTest;
using Curl.Conformance.UnitProgram;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamCaseRunner"/> with a hand-written curl: the arguments it builds as
/// <c>runtests.pl</c> does, the files and standard input it prepares, the outcomes it reports, and
/// the failures it turns a hang or an exception into.
/// </summary>
[TestClass]
public sealed partial class UpstreamCaseRunnerTests
{
    public TestContext TestContext { get; set; } = null!;

    // A case on the real clock needs a busy CI runner's thread pool to fire its waits, and a
    // runner that stalls the whole process for over ten seconds (BL-1717) would otherwise fail
    // a case that needs well under one.
    private static readonly TimeSpan RealClockTimeLimit = TimeSpan.FromMinutes(2);

    private const string HttpCase =
        "<testcase>\n<reply>\n<data crlf=\"headers\">\nHTTP/1.1 200 OK\n\nbody\n</data>\n</reply>\n"
        + "<client>\n<server>\nhttp\n</server>\n<command>\nhttp://%HOSTIP:%HTTPPORT/%TESTNUMBER\n</command>\n</client>\n"
        + "<verify>\n<protocol crlf=\"yes\">\nGET /%TESTNUMBER HTTP/1.1\nHost: %HOSTIP:%HTTPPORT\nUser-Agent: curl/%VERSION\n\n</protocol>\n</verify>\n</testcase>\n";

    [TestMethod]
    public async Task RunAsync_CaseThatMatches_Passes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        List<string>? arguments = null;
        UpstreamCaseRunner runner = Runner(async invocation =>
        {
            arguments = [.. invocation.Arguments];
            await ExchangeAsync(invocation.Connector, "GET /5 HTTP/1.1\r\nHost: 127.0.0.1:8990\r\nUser-Agent: curl/8.22.0\r\n\r\n", invocation.Arguments[1]);
            return 0;
        });

        UpstreamCaseOutcome outcome = await RunAsync(runner, HttpCase);

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Passed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        diagnostics.Assert("first argument", "--output", arguments![0]);
        Assert.AreEqual("--output", arguments![0]);
        diagnostics.Assert("arguments after the output file", "--include --trace-ascii --trace-config all --trace-time http://127.0.0.1:8990/5", string.Join(' ', arguments.Skip(2).Where((_, index) => index != 2)));
        CollectionAssert.AreEqual(new[] { "--include", "--trace-ascii", "--trace-config", "all", "--trace-time", "http://127.0.0.1:8990/5" }, arguments.Skip(2).Where((_, index) => index != 2).ToArray());
    }

    [TestMethod]
    public async Task RunAsync_CaseThatDiffers_FailsWithTheFirstDifference()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(3));

        UpstreamCaseOutcome outcome = await RunAsync(runner, HttpCase);

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Failed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Failed, outcome.Kind);
        diagnostics.Assert("detail starts with the expected text", true, outcome.Detail is not null && outcome.Detail.StartsWith("<verify><protocol> differs at byte 0", StringComparison.Ordinal));
        StringAssert.StartsWith(outcome.Detail, "<verify><protocol> differs at byte 0");
    }

    [TestMethod]
    [DataRow("", "", new[] { "--output", "*", "--include", "--trace-ascii", "#", "--trace-config", "all", "--trace-time", "a" })]
    [DataRow(" option=\"no-include\"", "", new[] { "--output", "*", "--trace-ascii", "#", "--trace-config", "all", "--trace-time", "a" })]
    [DataRow(" option=\"no-output,no-include\"", "", new[] { "--trace-ascii", "#", "--trace-config", "all", "--trace-time", "a" })]
    [DataRow("", "<verify>\n<stdout>\n</stdout>\n</verify>\n", new[] { "--include", "--trace-ascii", "#", "--trace-config", "all", "--trace-time", "a" })]
    [DataRow(" option=\"force-output\"", "<verify>\n<stdout>\n</stdout>\n</verify>\n", new[] { "--output", "*", "--include", "--trace-ascii", "#", "--trace-config", "all", "--trace-time", "a" })]
    [DataRow(" option=\"binary-trace\"", "", new[] { "--output", "*", "--include", "--trace", "#", "--trace-config", "all", "--trace-time", "a" })]
    public async Task RunAsync_PutsRuntestsArgumentsBeforeTheCommand(string commandAttributes, string verify, string[] expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string[]? arguments = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            arguments = [.. invocation.Arguments];
            return Task.FromResult(0);
        });

        await RunAsync(runner, $"<testcase>\n<client>\n<command{commandAttributes}>\na\n</command>\n</client>\n{verify}</testcase>\n");

        diagnostics.Arrange("expected arguments", string.Join(' ', expected));
        diagnostics.Assert("arguments", string.Join(' ', expected), string.Join(' ', arguments!.Select(argument => argument.EndsWith("/curl5.out", StringComparison.Ordinal) ? "*" : argument.EndsWith("/trace5", StringComparison.Ordinal) ? "#" : argument).ToArray()));
        CollectionAssert.AreEqual(expected, arguments!.Select(argument => argument.EndsWith("/curl5.out", StringComparison.Ordinal) ? "*" : argument.EndsWith("/trace5", StringComparison.Ordinal) ? "#" : argument).ToArray());
    }

    [TestMethod]
    public async Task RunAsync_WritesClientFilesAndGivesStdinAndSavesTheStandardStreams()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string logDirectory = CreateLogDirectory();
        string? stdin = null;
        UpstreamCaseRunner runner = Runner(async invocation =>
        {
            stdin = await new StreamReader(invocation.StandardInput).ReadToEndAsync();
            await invocation.StandardOutput.WriteAsync(Encoding.Latin1.GetBytes(File.ReadAllText($"{logDirectory}/sub/in.txt")));
            await invocation.StandardError.WriteAsync("warned\n"u8.ToArray());
            return 0;
        });
        string testFile = "<testcase>\n<client>\n<command>\na\n</command>\n"
            + "<file name=\"%LOGDIR/sub/in.txt\" nonewline=\"yes\">\nfile body\n</file>\n<stdin>\ntyped\n</stdin>\n</client>\n"
            + "<verify>\n<stdout nonewline=\"yes\">\nfile body\n</stdout>\n<file2 name=\"%LOGDIR/stderr%TESTNUMBER\">\nwarned\n</file2>\n</verify>\n</testcase>\n";

        UpstreamCaseOutcome outcome = await RunWithLogDirectoryAsync(runner, testFile, logDirectory);

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Passed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        Assert.AreEqual("typed\n", stdin);
        diagnostics.Diff("stdout file", "file body", File.ReadAllText($"{logDirectory}/stdout5"));
        Assert.AreEqual("file body", File.ReadAllText($"{logDirectory}/stdout5"));
    }

    [TestMethod]
    [DataRow(" option=\"no-stdout\"", ">>", "initial\nsaid\n", "")]
    [DataRow(" option=\"no-stdout\"", ">", "said\n", "")]
    [DataRow("", ">>", "initial\n", "said\n")]
    public async Task RunAsync_CommandRedirectsStandardOutput_WritesTheFileAsTheShellDoes(string option, string redirect, string expectedFile, string expectedStdout)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string logDirectory = CreateLogDirectory();
        UpstreamCaseRunner runner = Runner(async invocation =>
        {
            await invocation.StandardOutput.WriteAsync("said\n"u8.ToArray());
            return 0;
        });
        string testFile = $"<testcase>\n<client>\n<command{option}>\na {redirect} %LOGDIR/out%TESTNUMBER\n</command>\n"
            + "<file name=\"%LOGDIR/out%TESTNUMBER\">\ninitial\n</file>\n</client>\n</testcase>\n";

        UpstreamCaseOutcome outcome = await RunWithLogDirectoryAsync(runner, testFile, logDirectory);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        diagnostics.Diff("redirect file", expectedFile, File.ReadAllText($"{logDirectory}/out5"));
        Assert.AreEqual(expectedFile, File.ReadAllText($"{logDirectory}/out5"));
        Assert.AreEqual(expectedStdout, File.ReadAllText($"{logDirectory}/stdout5"));
    }

    [TestMethod]
    public async Task RunAsync_ClientFileAndCommandNamedLogSlashName_AreTheLogDirectorysName()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string logDirectory = CreateLogDirectory();
        IReadOnlyList<string>? arguments = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            arguments = invocation.Arguments;
            return Task.FromResult(0);
        });
        string testFile = "<testcase>\n<client>\n<command>\n-T log/upload%TESTNUMBER\n</command>\n<file name=\"log/upload%TESTNUMBER\">\nputdata\n</file>\n</client>\n</testcase>\n";

        UpstreamCaseOutcome outcome = await RunWithLogDirectoryAsync(runner, testFile, logDirectory);

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Passed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        Assert.AreEqual("putdata\n", File.ReadAllText($"{logDirectory}/upload5"));
        Assert.IsTrue(arguments!.Contains($"{logDirectory.Replace('\\', '/')}/upload5"), string.Join(" ", arguments!));
    }

    [TestMethod]
    [DataRow("headers", "GET / HTTP/1.0\r\n\r\nbody\n")]
    [DataRow("yes", "GET / HTTP/1.0\r\n\r\nbody\r\n")]
    public async Task RunAsync_StdinAndFileWithCrlf_AreGivenWithCrlfLineEndings(string crlf, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string logDirectory = CreateLogDirectory();
        string? stdin = null;
        UpstreamCaseRunner runner = Runner(async invocation =>
        {
            stdin = await new StreamReader(invocation.StandardInput).ReadToEndAsync();
            return 0;
        });
        string body = "GET / HTTP/1.0\n\nbody\n";
        string testFile = $"<testcase>\n<client>\n<command>\na\n</command>\n<file name=\"%LOGDIR/in.txt\" crlf=\"{crlf}\">\n{body}</file>\n"
            + $"<stdin crlf=\"{crlf}\">\n{body}</stdin>\n</client>\n</testcase>\n";

        await RunWithLogDirectoryAsync(runner, testFile, logDirectory);

        diagnostics.Arrange("crlf attribute", crlf);
        diagnostics.Diff("stdin", expected, stdin ?? string.Empty);
        diagnostics.Diff("file", expected, File.ReadAllText($"{logDirectory}/in.txt"));
        Assert.AreEqual(expected, stdin);
        Assert.AreEqual(expected, File.ReadAllText($"{logDirectory}/in.txt"));
    }

    [TestMethod]
    public async Task RunAsync_OutputFileCurlWrote_IsComparedWithTheReplyData()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            File.WriteAllText(invocation.Arguments[1], "served\n");
            return Task.FromResult(0);
        });

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<reply>\n<data>\nserved\n</data>\n</reply>\n<client>\n<command>\na\n</command>\n</client>\n</testcase>\n");

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Passed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_CaseTheHarnessCannotRun_IsSkippedWithTheReason()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = Runner(_ => throw new AssertFailedException("curl must not run"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<command>\nhttp://%HTTPTLS6PORT/\n</command>\n</client>\n</testcase>\n");

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        diagnostics.Assert("detail", "the harness has no value for %HTTPTLS6PORT", outcome.Detail);
        Assert.AreEqual("the harness has no value for %HTTPTLS6PORT", outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_FileThatDoesNotParse_IsSkippedWithTheParseFailure()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = Runner(_ => throw new AssertFailedException("curl must not run"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<reply>\n<data>\n</testcase>\n");

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        diagnostics.Assert("detail is present", true, !string.IsNullOrEmpty(outcome.Detail));
        Assert.IsFalse(string.IsNullOrEmpty(outcome.Detail));
    }

    [TestMethod]
    public async Task RunAsync_FeaturesDecideIfBlocks()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = new(_ => Task.FromResult(0), UpstreamCurlPlatform.Windows, TimeProvider.System, TimeSpan.FromSeconds(10));
        string testFile = "<testcase>\n<client>\n<command>\na\n</command>\n</client>\n<verify>\n<errorcode>\n%if win32\n0\n%else\n1\n%endif\n</errorcode>\n</verify>\n</testcase>\n";

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Passed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
    }

    // ADR-0488: off Windows a Schannel case is skipped as one only Windows' Curl runs; a case
    // needing a feature no platform's Curl has is an ordinary skip.
    [TestMethod]
    [DataRow("SSL\nSchannel", true, "Curl lacks the feature Schannel")]
    [DataRow("http\nDebug", false, "Curl lacks the feature Debug")]
    public async Task RunAsync_CaseNeedingAFeatureThisPlatformLacks_SaysWhetherAnotherPlatformRunsIt(string caseFeatures, bool runsOnlyOnAnotherPlatform, string reason)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = Runner(_ => throw new AssertFailedException("curl must not run"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, $"<testcase>\n<client>\n<features>\n{caseFeatures}\n</features>\n<command>\na\n</command>\n</client>\n</testcase>\n");

        diagnostics.Assert("runs only on another platform", runsOnlyOnAnotherPlatform, outcome.RunsOnlyOnAnotherPlatform);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        Assert.AreEqual(reason, outcome.Detail);
        Assert.AreEqual(runsOnlyOnAnotherPlatform, outcome.RunsOnlyOnAnotherPlatform);
    }

    [TestMethod]
    public async Task RunAsync_CurlThatNeverFinishes_FailsAfterTheTimeLimit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ExpiringTimeProvider time = new();
        UpstreamCaseRunner runner = new(_ => new TaskCompletionSource<int>().Task, UpstreamCurlPlatform.Unix, time, TimeSpan.FromMilliseconds(1));

        Task<UpstreamCaseOutcome> running = RunAsync(runner, "<testcase>\n<client>\n<command>\na\n</command>\n</client>\n</testcase>\n");
        await time.ExpireTheTimeLimitAsync();
        UpstreamCaseOutcome outcome = await running;

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Failed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Failed, outcome.Kind);
        diagnostics.Assert("detail", "curl did not finish within 0.001 seconds", outcome.Detail);
        Assert.AreEqual("curl did not finish within 0.001 seconds", outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_CurlThatLoopsWithoutYielding_FailsAfterTheTimeLimitAndIsStoppedByTheAbandonedServer()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ExpiringTimeProvider time = new();
        TaskCompletionSource<Exception> loopEnded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        UpstreamCaseRunner runner = new(invocation => LoopUntilTheServerFails(invocation.Connector, loopEnded), UpstreamCurlPlatform.Unix, time, TimeSpan.FromMilliseconds(1));

        Task<UpstreamCaseOutcome> running = RunAsync(runner, HttpCase);
        await time.ExpireTheTimeLimitAsync();
        UpstreamCaseOutcome outcome = await running;

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Failed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Failed, outcome.Kind);
        diagnostics.Assert("detail", "curl did not finish within 0.001 seconds", outcome.Detail);
        Assert.AreEqual("curl did not finish within 0.001 seconds", outcome.Detail);
        Exception loopException = await loopEnded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        diagnostics.Assert("loop ended with", nameof(IOException), loopException.GetType().Name);
        Assert.IsInstanceOfType<IOException>(loopException);
    }

    [TestMethod]
    public async Task RunAsync_WriteDelaysWithNoCurlTimer_AreSkippedSoTheCaseTakesNoRealTime()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // Two writes a minute apart would take two minutes on the real clock; the limit is ten seconds.
        string testFile = HttpCase.Replace("</reply>", "<servercmd>\nwritedelay: 60000\n</servercmd>\n</reply>", StringComparison.Ordinal);
        UpstreamCaseRunner runner = Runner(async invocation =>
        {
            await ExchangeAsync(invocation.Connector, "GET /5 HTTP/1.1\r\nHost: 127.0.0.1:8990\r\nUser-Agent: curl/8.22.0\r\n\r\n", invocation.Arguments[1]);
            return 0;
        });

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Passed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_WriteDelaysWithACurlTimer_TakeRealTime()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string testFile = HttpCase
            .Replace("</reply>", "<servercmd>\nwritedelay: 300\n</servercmd>\n</reply>", StringComparison.Ordinal)
            .Replace("%TESTNUMBER\n</command>", "%TESTNUMBER -m 5\n</command>", StringComparison.Ordinal);
        UpstreamCaseRunner runner = Runner(async invocation =>
        {
            await ExchangeAsync(invocation.Connector, "GET /5 HTTP/1.1\r\nHost: 127.0.0.1:8990\r\nUser-Agent: curl/8.22.0\r\n\r\n", invocation.Arguments[1]);
            return 0;
        }, RealClockTimeLimit);
        long startedAt = TimeProvider.System.GetTimestamp();

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Passed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        TimeSpan elapsed = TimeProvider.System.GetElapsedTime(startedAt);
        diagnostics.Assert("waited at least 300 ms", true, elapsed >= TimeSpan.FromMilliseconds(300));
        Assert.IsGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(300), elapsed);
    }

    [TestMethod]
    public async Task RunAsync_CurlThatThrows_FailsNamingTheException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = Runner(_ => throw new InvalidOperationException("boom"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<command>\na\n</command>\n</client>\n</testcase>\n");

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Failed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Failed, outcome.Kind);
        diagnostics.Assert("detail", "curl threw InvalidOperationException: boom", outcome.Detail);
        Assert.AreEqual("curl threw InvalidOperationException: boom", outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_CurlReachingForUdp_GetsTheDnsdEmulationInFrontOfTheTftpOne()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IDatagramConnector? datagramConnector = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            datagramConnector = invocation.DatagramConnector;
            return Task.FromResult(0);
        });

        await RunAsync(runner, "<testcase>\n<client>\n<command>\na\n</command>\n</client>\n</testcase>\n");

        diagnostics.Assert("datagram connector type", nameof(DnsServerConnector), datagramConnector?.GetType().Name ?? "(none)");
        Assert.IsInstanceOfType<DnsServerConnector>(datagramConnector);
    }

    [TestMethod]
    public async Task RunAsync_QueryOnDnsPort_IsComparedWithVerifyDns()
    {
        const string testCase = "<testcase>\n<reply>\n<dns>\nA: %HOSTIP\n</dns>\n</reply>\n<client>\n<server>\ndns\n</server>\n<command>\na\n</command>\n</client>\n" +
            "<verify>\n<dns>\nQNAME b QTYPE A\n</dns>\n</verify>\n</testcase>\n";
        UpstreamCaseRunner runner = Runner(async invocation =>
        {
            await using IDatagramChannel channel = (await invocation.DatagramConnector.OpenAsync("127.0.0.1", int.Parse(UpstreamCaseRunner.DnsPort, System.Globalization.CultureInfo.InvariantCulture), CancellationToken.None)).Channel!;
            await channel.SendAsync((byte[])[0, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, (byte)'a', 0, 0, 1, 0, 1], channel.ServerEndPoint, CancellationToken.None);
            return 0;
        });

        UpstreamCaseOutcome outcome = await RunAsync(runner, testCase);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Failed, outcome.Kind);
        Assert.Contains("QNAME a QTYPE A", outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_Test271_ReachesTheTftpEmulationOnTftpPort()
    {
        UpstreamCaseRunner runner = Runner(async invocation =>
        {
            await using IDatagramChannel channel = (await invocation.DatagramConnector.OpenAsync("127.0.0.1", int.Parse(UpstreamCaseRunner.TftpPort, System.Globalization.CultureInfo.InvariantCulture), CancellationToken.None)).Channel!;
            await channel.SendAsync(Encoding.Latin1.GetBytes("\0\u0001/5\0octet\0tsize\00\0blksize\0512\0timeout\06\0"), channel.ServerEndPoint, CancellationToken.None);
            DatagramReceived received = await channel.ReceiveAsync(new byte[600], CancellationToken.None);
            await channel.SendAsync(new byte[] { 0, 4, 0, 1 }, received.RemoteEndPoint, CancellationToken.None);
            return 0;
        });
        string testFile = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "UpstreamTestData", "test271.curltest"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        // The protocol dump matches; only the --output file the fake never writes differs.
        StringAssert.StartsWith(outcome.Detail, "the --output file");
    }

    [TestMethod]
    public async Task RunAsync_LogDirectoryWithABlank_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(0));

        diagnostics.Arrange("log directory", "/a b");
        ArgumentException exception = await Assert.ThrowsExactlyAsync<ArgumentException>(() => runner.RunAsync(1, ReadOnlyMemory<byte>.Empty, "/a b"));
        diagnostics.Act("exception type", exception.GetType().Name);
        diagnostics.Assert("exception type", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    public async Task RunAsync_TestsDirectoryWithABlank_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(0));

        diagnostics.Arrange("tests directory", "C:/Users/Stewart Rogers/tests");
        ArgumentException exception = await Assert.ThrowsExactlyAsync<ArgumentException>(() => runner.RunAsync(1, ReadOnlyMemory<byte>.Empty, CreateLogDirectory(), "C:/Users/Stewart Rogers/tests"));
        diagnostics.Act("exception parameter", exception.ParamName);
        diagnostics.Assert("exception parameter", "testsDirectory", exception.ParamName);
        Assert.AreEqual("testsDirectory", exception.ParamName);
    }

    [TestMethod]
    public async Task RunAsync_TestsDirectory_IsPwdWithForwardSlashes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IReadOnlyList<string>? arguments = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            arguments = CommandArguments(invocation);
            return Task.FromResult(0);
        });
        string testFile = "<testcase>\n<client>\n<command option=\"no-output,no-include\">\n--output-dir %PWD/not-there\n</command>\n</client>\n</testcase>\n";

        diagnostics.Arrange("tests directory", "Z:\\curl\\tests");
        UpstreamCaseOutcome outcome = await runner.RunAsync(5, Encoding.Latin1.GetBytes(testFile), CreateLogDirectory(), "Z:\\curl\\tests");
        diagnostics.Act("arguments", string.Join(" | ", arguments ?? []));

        diagnostics.Assert("arguments", "--output-dir | Z:/curl/tests/not-there", string.Join(" | ", arguments ?? []));
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        CollectionAssert.AreEqual(new[] { "--output-dir", "Z:/curl/tests/not-there" }, arguments!.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_SourceDirectoryWithABlank_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(0));

        diagnostics.Arrange("source directory", "/a b/tests");
        ArgumentException exception = await Assert.ThrowsExactlyAsync<ArgumentException>(() => runner.RunAsync(1, ReadOnlyMemory<byte>.Empty, CreateLogDirectory(), null, null, "/a b/tests"));
        diagnostics.Act("exception parameter", exception.ParamName);
        diagnostics.Assert("exception parameter", "sourceDirectory", exception.ParamName);
        Assert.AreEqual("sourceDirectory", exception.ParamName);
    }

    [TestMethod]
    public async Task RunAsync_SourceDirectory_IsSrcdirWithForwardSlashesAndKeepsTheEmulatedScripts()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IReadOnlyList<string>? arguments = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            arguments = CommandArguments(invocation);
            return Task.FromResult(0);
        });
        string testFile = "<testcase>\n<client>\n<command option=\"no-output,no-include\">\n-K %SRCDIR/data/x --output-dir %SRCDIR/libtest/test610.pl\n</command>\n</client>\n</testcase>\n";

        diagnostics.Arrange("source directory", "Z:\\curl\\tests");
        UpstreamCaseOutcome outcome = await runner.RunAsync(5, Encoding.Latin1.GetBytes(testFile), CreateLogDirectory(), null, null, "Z:\\curl\\tests");
        diagnostics.Act("arguments", string.Join(" | ", arguments ?? []));

        diagnostics.Assert("arguments", "-K | Z:/curl/tests/data/x | --output-dir | ./libtest/test610.pl", string.Join(" | ", arguments ?? []));
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        CollectionAssert.AreEqual(new[] { "-K", "Z:/curl/tests/data/x", "--output-dir", "./libtest/test610.pl" }, arguments!.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_CertificateDirectoryWithABlank_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(0));

        diagnostics.Arrange("certificate directory", "/a b/tests");
        ArgumentException exception = await Assert.ThrowsExactlyAsync<ArgumentException>(() => runner.RunAsync(1, ReadOnlyMemory<byte>.Empty, CreateLogDirectory(), null, "/a b/tests"));
        diagnostics.Act("exception parameter", exception.ParamName);
        diagnostics.Assert("exception parameter", "certificateDirectory", exception.ParamName);
        Assert.AreEqual("certificateDirectory", exception.ParamName);
    }

    [TestMethod]
    public async Task RunAsync_CertificateDirectory_IsCertdirWithForwardSlashes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IReadOnlyList<string>? arguments = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            arguments = CommandArguments(invocation);
            return Task.FromResult(0);
        });
        string testFile = "<testcase>\n<client>\n<server>\nnone\n</server>\n<command option=\"no-output,no-include\">\n--cacert %CERTDIR/certs/test-ca.crt\n</command>\n</client>\n</testcase>\n";

        diagnostics.Arrange("certificate directory", "Z:\\curl\\tests");
        UpstreamCaseOutcome outcome = await runner.RunAsync(5, Encoding.Latin1.GetBytes(testFile), CreateLogDirectory(), null, "Z:\\curl\\tests");
        diagnostics.Act("arguments", string.Join(" | ", arguments ?? []));

        diagnostics.Assert("arguments", "--cacert | Z:/curl/tests/certs/test-ca.crt", string.Join(" | ", arguments ?? []));
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        CollectionAssert.AreEqual(new[] { "--cacert", "Z:/curl/tests/certs/test-ca.crt" }, arguments!.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_CaseUsingProxyport_RunsItWithTheProxyPort()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string[]? arguments = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            arguments = [.. CommandArguments(invocation)];
            return Task.FromResult(0);
        });

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<server>\nhttp\n</server>\n<command option=\"no-output,no-include\">\n-x %HOSTIP:%PROXYPORT http://example.com/\n</command>\n</client>\n</testcase>\n");

        diagnostics.Assert("arguments", "-x 127.0.0.1:8992 http://example.com/", string.Join(' ', arguments ?? []));
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        CollectionAssert.AreEqual(new[] { "-x", "127.0.0.1:" + UpstreamCaseRunner.ProxyPort, "http://example.com/" }, arguments);
    }

    [TestMethod]
    public async Task RunAsync_NoCertificateDirectory_SkipsACaseUsingCertdir()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(0));

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<command>\n--cacert %CERTDIR/certs/test-ca.crt\n</command>\n</client>\n</testcase>\n");

        diagnostics.Assert("skip reason", "the harness has no value for %CERTDIR", outcome.Detail);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
        Assert.AreEqual("the harness has no value for %CERTDIR", outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_NoTestsDirectory_SkipsACaseUsingPwd()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(0));

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<command>\n--output-dir %PWD/not-there\n</command>\n</client>\n</testcase>\n");

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_SourceDirectoryAndNoTestsDirectory_GivesPwdTheSourceDirectory()
    {
        // runtests.pl runs in the release's tests folder, which is also its %SRCDIR (test726's HOME=%PWD, BL-2224).
        var diagnostics = TestDiagnostics.For(TestContext);
        string logDirectory = CreateLogDirectory();
        IReadOnlyList<string>? arguments = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            arguments = CommandArguments(invocation);
            return Task.FromResult(0);
        });

        UpstreamCaseOutcome outcome = await runner.RunAsync(5, Encoding.Latin1.GetBytes("<testcase>\n<client>\n<command option=\"no-output,no-include\">\n%PWD/not-there\n</command>\n</client>\n</testcase>\n"), logDirectory, sourceDirectory: logDirectory);

        string expected = logDirectory.Replace('\\', '/') + "/not-there";
        diagnostics.Assert("argument", expected, arguments?.SingleOrDefault());
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        Assert.AreEqual(expected, arguments!.Single());
    }

    [TestMethod]
    public async Task RunAsync_LibTests_IsTheLibtestRunnerPath()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IReadOnlyList<string>? arguments = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            arguments = CommandArguments(invocation);
            return Task.FromResult(0);
        });

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<command option=\"no-output,no-include\">\n%LIBTESTS\n</command>\n</client>\n</testcase>\n");

        diagnostics.Assert("argument", "./libtest/libtests", arguments?.SingleOrDefault());
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        Assert.AreEqual("./libtest/libtests", arguments!.Single());
    }

    [TestMethod]
    public async Task RunAsync_PwdBeforeLogDir_NamesTheFileInTheLogDirectoryWithNoTestsDirectory()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string logDirectory = CreateLogDirectory();
        IReadOnlyList<string>? arguments = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            arguments = CommandArguments(invocation);
            return Task.FromResult(0);
        });

        UpstreamCaseOutcome outcome = await RunWithLogDirectoryAsync(runner, "<testcase>\n<client>\n<command option=\"no-output,no-include\">\n%PWD/%LOGDIR/x\n</command>\n</client>\n</testcase>\n", logDirectory);

        string expected = logDirectory.Replace('\\', '/') + "/x";
        diagnostics.Assert("argument", expected, arguments?.SingleOrDefault());
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        Assert.AreEqual(expected, arguments!.Single());
    }

    [TestMethod]
    public async Task RunAsync_OutputFile_IsCurlTestNumberDotOutInTheLogDirectory()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string logDirectory = CreateLogDirectory();
        IReadOnlyList<string>? arguments = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            arguments = invocation.Arguments;
            return Task.FromResult(0);
        });

        await RunWithLogDirectoryAsync(runner, "<testcase>\n<client>\n<command>\na\n</command>\n</client>\n</testcase>\n", logDirectory);

        string expected = logDirectory.Replace('\\', '/') + "/curl5.out";
        diagnostics.Assert("output file", expected, arguments?[1]);
        Assert.AreEqual(expected, arguments![1]);
    }

    [TestMethod]
    public async Task RunAsync_SrcdirOutsideTheEmulatedScripts_SkipsForTheVariable()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(0));

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<command>\n-K %SRCDIR/data/x\n</command>\n</client>\n</testcase>\n");

        diagnostics.Assert("outcome detail", "the harness has no value for %SRCDIR", outcome.Detail);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
        Assert.AreEqual("the harness has no value for %SRCDIR", outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_NullLogDirectory_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(0));

        diagnostics.Arrange("log directory", "(null)");
        ArgumentNullException exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => runner.RunAsync(1, ReadOnlyMemory<byte>.Empty, null!));
        diagnostics.Act("exception type", exception.GetType().Name);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public async Task RunAsync_IncludesReadFilesAndAMissingOneIsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string logDirectory = CreateLogDirectory();
        File.WriteAllText(Path.Combine(logDirectory, "code.txt"), "7\r\n");
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(7));
        string testFile = "<testcase>\n<client>\n<command>\na\n</command>\n</client>\n<verify>\n<errorcode>\n"
            + "%include %LOGDIR/missing.txt%\n%includetext %LOGDIR/code.txt%\n</errorcode>\n</verify>\n</testcase>\n";

        UpstreamCaseOutcome outcome = await RunWithLogDirectoryAsync(runner, testFile, logDirectory);

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Passed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_Setenv_SetsTheVariablesForThatRunOnlyAndTheNextRunHasNone()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        List<IReadOnlyDictionary<string, string>> environments = [];
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            environments.Add(invocation.EnvironmentVariables);
            return Task.FromResult(0);
        });
        string withSetenv = "<testcase>\n<client>\n<setenv>\nno_proxy=%HOSTIP\nEMPTY=\nUNSET\n=nameless\nA=b=c\n</setenv>\n"
            + "<command>\na\n</command>\n</client>\n</testcase>\n";

        await RunAsync(runner, withSetenv);
        await RunAsync(runner, "<testcase>\n<client>\n<command>\na\n</command>\n</client>\n</testcase>\n");

        string first = string.Join(';', environments[0].OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));
        diagnostics.Assert("first run's environment", "A=b=c;EMPTY=;no_proxy=127.0.0.1", first);
        Assert.AreEqual("A=b=c;EMPTY=;no_proxy=127.0.0.1", first);
        diagnostics.Assert("second run's environment size", 0, environments[1].Count);
        Assert.IsEmpty(environments[1]);
    }

    [TestMethod]
    public async Task RunAsync_TwoSetenvParts_SetsTheVariablesOfBoth()
    {
        // getpart joins every part of one name in a section; test433 sets COLUMNS in a second <setenv> (BL-2017).
        var diagnostics = TestDiagnostics.For(TestContext);
        IReadOnlyDictionary<string, string>? environment = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            environment = invocation.EnvironmentVariables;
            return Task.FromResult(0);
        });

        await RunAsync(runner, "<testcase>\n<client>\n<setenv>\nA=1\n</setenv>\n<setenv>\nCOLUMNS=300\n</setenv>\n<command>\na\n</command>\n</client>\n</testcase>\n");

        string actual = string.Join(';', environment!.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));
        diagnostics.Assert("environment", "A=1;COLUMNS=300", actual);
        Assert.AreEqual("A=1;COLUMNS=300", actual);
    }

    [TestMethod]
    public async Task RunAsync_NoListenPort_IsAPortWhoseConnectionIsRefused()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = Runner(async invocation =>
        {
            string port = invocation.Arguments[^1].Split(':')[1];
            ConnectResult result = await invocation.Connector.ConnectAsync(new ConnectTarget("127.0.0.1", int.Parse(port, System.Globalization.CultureInfo.InvariantCulture), false), CancellationToken.None);
            return (int)result.ExitCode;
        });

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<server>\nnone\n</server>\n<command>\n%HOSTIP:%NOLISTENPORT\n</command>\n</client>\n<verify>\n<errorcode>\n7\n</errorcode>\n</verify>\n</testcase>\n");

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Passed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_WithSshServer_GivesTheSshVariablesWritesTheKeyFilesAndRoutesItsPort()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string logDirectory = CreateLogDirectory();
        string[]? arguments = null;
        string? sshServerReached = null;
        UpstreamSshServer sshServer = new(
            () => new RefusingConnector("ssh server"),
            "curltest",
            Encoding.ASCII.GetBytes("private"),
            Encoding.ASCII.GetBytes("public"),
            "0011",
            "abc/def")
        {
            HostKeyAlgorithm = "ssh-rsa",
        };
        UpstreamCaseRunner runner = new(
            async invocation =>
            {
                arguments = [.. CommandArguments(invocation)];
                ConnectResult result = await invocation.Connector.ConnectAsync(new ConnectTarget("127.0.0.1", UpstreamSshServer.SshPort, false), CancellationToken.None);
                sshServerReached = result.ErrorMessage;
                return 0;
            },
            UpstreamCurlPlatform.Unix,
            TimeProvider.System,
            TimeSpan.FromSeconds(10),
            sshServer);

        await RunWithLogDirectoryAsync(runner, "<testcase>\n<client>\n<server>\nnone\n</server>\n<command option=\"no-output,no-include\">\n%SSHPORT %USER [%SFTP_PWD] [%SCP_PWD] %SSHSRVMD5 %SSHSRVSHA256 %SSHKEYALGO\n</command>\n</client>\n</testcase>\n", logDirectory);

        diagnostics.Assert("arguments", "9003 curltest [] [] 0011 abc/def ssh-rsa", string.Join(' ', arguments!));
        CollectionAssert.AreEqual(new[] { "9003", "curltest", "[]", "[]", "0011", "abc/def", "ssh-rsa" }, arguments);
        diagnostics.Assert("connector reached on %SSHPORT", "ssh server", sshServerReached);
        Assert.AreEqual("ssh server", sshServerReached);
        Assert.AreEqual("private", await File.ReadAllTextAsync(Path.Combine(logDirectory, "server", "curl_client_key")));
        Assert.AreEqual("public", await File.ReadAllTextAsync(Path.Combine(logDirectory, "server", "curl_client_key.pub")));
    }

    [TestMethod]
    public async Task RunAsync_WithSshServer_ReadsTheUploadAfterThePostcheckMovesItIntoPlace()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamSshServer sshServer = new(() => new RefusingConnector("ssh server"), "curltest", Encoding.ASCII.GetBytes("private"), Encoding.ASCII.GetBytes("public"), "0011", "abc/def");
        UpstreamCaseRunner runner = new(
            invocation =>
            {
                string uploaded = CommandArguments(invocation)[0];
                Directory.CreateDirectory(Path.GetDirectoryName(uploaded)!);
                File.WriteAllText(uploaded, "payload\n");
                return Task.FromResult(0);
            },
            UpstreamCurlPlatform.Unix,
            TimeProvider.System,
            TimeSpan.FromSeconds(10),
            sshServer);
        string testFile = "<testcase>\n<client>\n<server>\nsftp\n</server>\n<command option=\"no-output,no-include\">\n%LOGDIR/test%TESTNUMBER.dir/file\n</command>\n</client>\n"
            + "<verify>\n<upload>\npayload\n</upload>\n<postcheck>\n%PERL %SRCDIR/libtest/test610.pl move %LOGDIR/test%TESTNUMBER.dir/file %LOGDIR/upload.%TESTNUMBER\n</postcheck>\n</verify>\n</testcase>\n";

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Passed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_PostcheckRewritesTheOutputFile_ComparesReplyDataWithTheRewrite()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            File.WriteAllText(CommandArguments(invocation)[0], "rewritten\n");
            List<string> arguments = [.. invocation.Arguments];
            File.WriteAllText(arguments[arguments.IndexOf("--output") + 1], "raw listing\n");
            return Task.FromResult(0);
        });
        string testFile = "<testcase>\n<reply>\n<data>\nrewritten\n</data>\n</reply>\n<client>\n<command>\n%LOGDIR/rewrite%TESTNUMBER\n</command>\n</client>\n"
            + "<verify>\n<postcheck>\n%PERL %SRCDIR/libtest/test610.pl rm %LOGDIR/curl%TESTNUMBER.out move %LOGDIR/rewrite%TESTNUMBER %LOGDIR/curl%TESTNUMBER.out\n</postcheck>\n</verify>\n</testcase>\n";

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Passed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_Http2CaseWithoutHttp2Server_IsSkippedForTheServer()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(0));

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<server>\nhttp/2\n</server>\n<command>\nhttp://%HOSTIP:%HTTP2PORT/%TESTNUMBER\n</command>\n</client>\n<verify>\n</verify>\n</testcase>\n");

        diagnostics.Assert("skip reason", "the harness does not emulate the http/2 server", outcome.Detail);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        Assert.AreEqual("the harness does not emulate the http/2 server", outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_WithHttp2Server_ReachesItOnHttp2PortInFrontOfTheSwsEmulation()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string? http2ServerReached = null;
        string? httpPortReached = null;
        IConnector? backend = null;
        UpstreamHttp2Server http2Server = new(sws =>
        {
            backend = sws;
            return new RefusingConnector("http2 server");
        });
        UpstreamCaseRunner runner = new(
            async invocation =>
            {
                http2ServerReached = (await invocation.Connector.ConnectAsync(new ConnectTarget("127.0.0.1", UpstreamHttp2Server.Http2Port, false), CancellationToken.None)).ErrorMessage;
                ConnectResult http = await invocation.Connector.ConnectAsync(new ConnectTarget("127.0.0.1", 8990, false), CancellationToken.None);
                httpPortReached = http.Connection is not null ? "connected" : http.ErrorMessage;
                return 0;
            },
            UpstreamCurlPlatform.Unix,
            TimeProvider.System,
            TimeSpan.FromSeconds(10),
            http2Server: http2Server);

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<server>\nhttp/2\n</server>\n<command>\nhttp://%HOSTIP:%HTTP2PORT/%TESTNUMBER\n</command>\n</client>\n<verify>\n</verify>\n</testcase>\n");

        diagnostics.Assert("connector reached on %HTTP2PORT", "http2 server", http2ServerReached);
        Assert.AreNotEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
        Assert.AreEqual("http2 server", http2ServerReached);
        Assert.AreEqual("connected", httpPortReached);
        Assert.IsInstanceOfType<SwsHttpServerConnector>(backend);
    }

    // nghttpx's TLS front stands on %HTTP2TLSPORT with test-localhost.pem only for a case naming the http/2 server (BL-2245).
    [TestMethod]
    [DataRow("http/2", "http2 tls server", true)]
    [DataRow("http", "connected", false)]
    public async Task RunAsync_WithHttp2ServerAndACertificateDirectory_PutsTheTlsServerOnHttp2TlsPortForAnHttp2Case(string serverLine, string expectedReached, bool expectsCertificate)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string certificateDirectory = CreateLogDirectory();
        Directory.CreateDirectory(Path.Combine(certificateDirectory, "certs"));
        using (System.Security.Cryptography.ECDsa key = System.Security.Cryptography.ECDsa.Create())
        {
            System.Security.Cryptography.X509Certificates.CertificateRequest request = new("CN=localhost", key, System.Security.Cryptography.HashAlgorithmName.SHA256);
            using System.Security.Cryptography.X509Certificates.X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            await File.WriteAllTextAsync(Path.Combine(certificateDirectory, "certs", "test-localhost.pem"), certificate.ExportCertificatePem() + "\n" + key.ExportPkcs8PrivateKeyPem() + "\n");
        }

        System.Security.Cryptography.X509Certificates.X509Certificate2? presented = null;
        string[]? arguments = null;
        string? reached = null;
        UpstreamHttp2Server http2Server = new(
            _ => new RefusingConnector("http2 server"),
            (_, certificate) =>
            {
                presented = certificate;
                return new RefusingConnector("http2 tls server");
            });
        UpstreamCaseRunner runner = new(
            async invocation =>
            {
                arguments = [.. CommandArguments(invocation)];
                ConnectResult connected = await invocation.Connector.ConnectAsync(new ConnectTarget("127.0.0.1", UpstreamHttp2Server.Http2TlsPort, false), CancellationToken.None);
                reached = connected.Connection is not null ? "connected" : connected.ErrorMessage;
                return 0;
            },
            UpstreamCurlPlatform.Unix,
            TimeProvider.System,
            TimeSpan.FromSeconds(10),
            http2Server: http2Server);
        string testFile = $"<testcase>\n<client>\n<server>\n{serverLine}\n</server>\n<command option=\"no-output,no-include\">\nhttps://localhost:%HTTP2TLSPORT/\n</command>\n</client>\n</testcase>\n";

        UpstreamCaseOutcome outcome = await runner.RunAsync(5, Encoding.Latin1.GetBytes(testFile), CreateLogDirectory(), null, certificateDirectory);
        diagnostics.Act("connector reached on %HTTP2TLSPORT", reached);

        Assert.AreNotEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
        CollectionAssert.AreEqual(new[] { "https://localhost:" + UpstreamCaseRunner.Http2TlsPort + "/" }, arguments);
        Assert.AreEqual(expectedReached, reached);
        Assert.AreEqual(expectsCertificate, presented is not null);
    }

    [TestMethod]
    public async Task RunAsync_H2cVersion_IsH2c()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string[]? arguments = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            arguments = [.. CommandArguments(invocation)];
            return Task.FromResult(0);
        });

        await RunAsync(runner, "<testcase>\n<client>\n<server>\nnone\n</server>\n<command option=\"no-output,no-include\">\n%H2CVER\n</command>\n</client>\n</testcase>\n");

        diagnostics.Assert("arguments", "h2c", string.Join(' ', arguments!));
        CollectionAssert.AreEqual(new[] { "h2c" }, arguments);
    }

    [TestMethod]
    public async Task RunAsync_Http3CaseWithoutHttp3Server_IsSkippedForTheServer()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(0));

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<server>\nhttp/3\n</server>\n<command>\nhttp://%HOSTIP:%HTTPPORT/%TESTNUMBER\n</command>\n</client>\n<verify>\n</verify>\n</testcase>\n");

        diagnostics.Assert("skip reason", "the harness does not emulate the http/3 server", outcome.Detail);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        Assert.AreEqual("the harness does not emulate the http/3 server", outcome.Detail);
    }

    // runtests.pl reports nghttpx-h3 when its nghttpx serves HTTP/3; the harness reports it when it has the stand-in (BL-2261).
    [TestMethod]
    [DataRow(true, UpstreamCaseOutcomeKind.Passed)]
    [DataRow(false, UpstreamCaseOutcomeKind.Skipped)]
    public async Task RunAsync_NghttpxH3FeatureCase_RunsOnlyWithAnHttp3Server(bool givesHttp3Server, UpstreamCaseOutcomeKind expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("gives an http/3 server", givesHttp3Server);
        UpstreamCaseRunner runner = new(
            _ => Task.FromResult(0),
            UpstreamCurlPlatform.Unix,
            TimeProvider.System,
            TimeSpan.FromSeconds(10),
            http3Server: givesHttp3Server ? new UpstreamHttp3Server((_, _) => new RefusingDatagramConnector("http3 server")) : null);

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<features>\nnghttpx-h3\n</features>\n<server>\nnone\n</server>\n<command option=\"no-output,no-include\">\nx\n</command>\n</client>\n</testcase>\n");

        diagnostics.Assert("outcome", expected, outcome.Kind);
        Assert.AreEqual(expected, outcome.Kind, outcome.Detail);
    }

    // nghttpx-h3 stands on %HTTP3PORT with test-localhost.pem only for a case naming the http/3 server (BL-2261);
    // every other datagram, and every one of another case, reaches the tftpd emulation, which refuses port 9019.
    [TestMethod]
    [DataRow("http/3", "http3 server", true)]
    [DataRow("http", "tftpd", false)]
    public async Task RunAsync_WithHttp3ServerAndACertificateDirectory_RoutesHttp3PortDatagramsToItForAnHttp3Case(string serverLine, string expectedReached, bool expectsServer)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string certificateDirectory = CreateLogDirectory();
        Directory.CreateDirectory(Path.Combine(certificateDirectory, "certs"));
        using (System.Security.Cryptography.ECDsa key = System.Security.Cryptography.ECDsa.Create())
        {
            System.Security.Cryptography.X509Certificates.CertificateRequest request = new("CN=localhost", key, System.Security.Cryptography.HashAlgorithmName.SHA256);
            using System.Security.Cryptography.X509Certificates.X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            await File.WriteAllTextAsync(Path.Combine(certificateDirectory, "certs", "test-localhost.pem"), certificate.ExportCertificatePem() + "\n" + key.ExportPkcs8PrivateKeyPem() + "\n");
        }

        System.Security.Cryptography.X509Certificates.X509Certificate2? presented = null;
        IConnector? backend = null;
        string[]? arguments = null;
        string? reached = null;
        UpstreamHttp3Server http3Server = new((sws, certificate) =>
        {
            backend = sws;
            presented = certificate;
            return new RefusingDatagramConnector("http3 server");
        });
        UpstreamCaseRunner runner = new(
            async invocation =>
            {
                arguments = [.. CommandArguments(invocation)];
                DatagramOpenResult opened = await invocation.DatagramConnector.OpenAsync("127.0.0.1", UpstreamHttp3Server.Http3Port, CancellationToken.None);
                reached = opened.ErrorMessage!.Contains("http3 server", StringComparison.Ordinal) ? "http3 server" : "tftpd";
                return 0;
            },
            UpstreamCurlPlatform.Unix,
            TimeProvider.System,
            TimeSpan.FromSeconds(10),
            http3Server: http3Server);
        string testFile = $"<testcase>\n<client>\n<server>\n{serverLine}\n</server>\n<command option=\"no-output,no-include\">\nhttps://localhost:%HTTP3PORT/\n</command>\n</client>\n</testcase>\n";

        UpstreamCaseOutcome outcome = await runner.RunAsync(5, Encoding.Latin1.GetBytes(testFile), CreateLogDirectory(), null, certificateDirectory);
        diagnostics.Act("datagram connector reached on %HTTP3PORT", reached);

        Assert.AreNotEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
        CollectionAssert.AreEqual(new[] { "https://localhost:" + UpstreamCaseRunner.Http3Port + "/" }, arguments);
        Assert.AreEqual(expectedReached, reached);
        Assert.AreEqual(expectsServer, presented is not null);
        Assert.AreEqual(expectsServer, backend is SwsHttpServerConnector);
    }

    [TestMethod]
    public async Task RunAsync_Http3PortWithoutACertificateDirectory_IsSkippedForTheVariable()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = new(
            _ => Task.FromResult(0),
            UpstreamCurlPlatform.Unix,
            TimeProvider.System,
            TimeSpan.FromSeconds(10),
            http3Server: new UpstreamHttp3Server((_, _) => new RefusingDatagramConnector("http3 server")));

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<server>\nhttp/3\n</server>\n<command>\nhttps://localhost:%HTTP3PORT/\n</command>\n</client>\n</testcase>\n");

        diagnostics.Assert("skip reason", "the harness has no value for HTTP3PORT", outcome.Detail);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        Assert.Contains("HTTP3PORT", outcome.Detail!);
    }

    // Refuses every datagram open with its own name, so a test can tell it was reached.
    private sealed class RefusingDatagramConnector(string name) : IDatagramConnector
    {
        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
            ValueTask.FromResult(DatagramOpenResult.Failed(CurlExitCode.CouldntConnect, name));
    }

    // Refuses every connection with its own name, so a test can tell it was reached.
    private sealed class RefusingConnector(string name) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Refused(name));
    }

    private const string PerlCurlCase =
        "<testcase>\n<client>\n<server>\nnone\n</server>\n<command type=\"perl\">\n/src/tests/test1707.pl %CURL -h all\n</command>\n</client>\n"
        + "<verify>\n<stdout>\ncurl help\n</stdout>\n</verify>\n</testcase>\n";

    // A test-only interpretation of test1707.pl: runs %CURL in process with the words after it.
    private static readonly Func<UpstreamCurlInvocation, Func<UpstreamCurlInvocation, Task<int>>, Task<int>> CurlRunningScript =
        (invocation, runCurl) => invocation.Arguments[0] == UpstreamCaseRunner.CurlProgram
            ? runCurl(new UpstreamCurlInvocation([.. invocation.Arguments.Skip(1)], invocation.StandardOutput, invocation.StandardError, invocation.StandardInput, invocation.Connector, invocation.DatagramConnector))
            : Task.FromResult(9);

    [TestMethod]
    public async Task RunAsync_PerlCommandHoldingCurlWithAnInterpretation_RunsTheInProcessCurl()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        List<string>? curlArguments = null;
        UpstreamCaseRunner runner = new(
            async invocation =>
            {
                curlArguments = [.. invocation.Arguments];
                await invocation.StandardOutput.WriteAsync("curl help\n"u8.ToArray());
                return 0;
            },
            UpstreamCurlPlatform.Unix,
            TimeProvider.System,
            TimeSpan.FromSeconds(10),
            perlCommandScripts: new Dictionary<string, Func<UpstreamCurlInvocation, Func<UpstreamCurlInvocation, Task<int>>, Task<int>>> { ["test1707.pl"] = CurlRunningScript });

        UpstreamCaseOutcome outcome = await RunAsync(runner, PerlCurlCase);

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Passed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        diagnostics.Assert("curl's arguments", "-h all", string.Join(' ', curlArguments!));
        CollectionAssert.AreEqual(new[] { "-h", "all" }, curlArguments);
    }

    [TestMethod]
    public async Task RunAsync_DateAndVernum_AreTheStartTimesUtcDateAndTheVersion()
    {
        // runtests.pl's %DATE is strftime "%Y-%m-%d", gmtime of its start time; %VERNUM the version's digits and dots.
        var diagnostics = TestDiagnostics.For(TestContext);
        string[]? arguments = null;
        UpstreamCaseRunner runner = new(
            invocation =>
            {
                arguments = CommandArguments(invocation);
                return Task.FromResult(0);
            },
            UpstreamCurlPlatform.Unix,
            new StartTimeProvider(new DateTimeOffset(2026, 3, 4, 23, 59, 30, TimeSpan.FromHours(-5))),
            TimeSpan.FromSeconds(10));

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<command>\n%DATE %VERNUM\n</command>\n</client>\n</testcase>\n");

        diagnostics.Assert("command words", "2026-03-05 8.22.0", string.Join(' ', arguments!));
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        CollectionAssert.AreEqual(new[] { "2026-03-05", "8.22.0" }, arguments);
    }

    [TestMethod]
    public async Task RunAsync_PerlCommandScript_IsGivenTheStartTimeAsSourceDateEpoch()
    {
        // runtests.pl exports SOURCE_DATE_EPOCH, its start time, which managen reads for the page date.
        var diagnostics = TestDiagnostics.For(TestContext);
        IReadOnlyDictionary<string, string>? environment = null;
        UpstreamCaseRunner runner = new(
            _ => Task.FromResult(0),
            UpstreamCurlPlatform.Unix,
            new StartTimeProvider(DateTimeOffset.FromUnixTimeSeconds(1772668770).AddMilliseconds(400)),
            TimeSpan.FromSeconds(10),
            perlCommandScripts: new Dictionary<string, Func<UpstreamCurlInvocation, Func<UpstreamCurlInvocation, Task<int>>, Task<int>>>
            {
                ["test1707.pl"] = (invocation, _) =>
                {
                    environment = invocation.EnvironmentVariables;
                    return Task.FromResult(0);
                },
            });

        await RunAsync(runner, "<testcase>\n<client>\n<server>\nnone\n</server>\n<command type=\"perl\">\n/src/tests/test1707.pl\n</command>\n</client>\n</testcase>\n");

        diagnostics.Assert("SOURCE_DATE_EPOCH", "1772668770", environment!.GetValueOrDefault("SOURCE_DATE_EPOCH"));
        Assert.AreEqual("1772668770", environment!["SOURCE_DATE_EPOCH"]);
    }

    private sealed class StartTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }

    [TestMethod]
    public async Task RunAsync_PerlCommandWithoutInterpretations_SkipsAsAPerlCommand()
    {
        UpstreamCaseOutcome outcome = await RunAsync(Runner(_ => Task.FromResult(0)), PerlCurlCase);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        Assert.AreEqual("the harness does not run a perl command", outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_PerlCommandWhoseScriptIsNotRegistered_SkipsAsAPerlCommand()
    {
        UpstreamCaseRunner runner = new(
            _ => Task.FromResult(0),
            UpstreamCurlPlatform.Unix,
            TimeProvider.System,
            TimeSpan.FromSeconds(10),
            perlCommandScripts: new Dictionary<string, Func<UpstreamCurlInvocation, Func<UpstreamCurlInvocation, Task<int>>, Task<int>>> { ["test971.pl"] = CurlRunningScript });

        UpstreamCaseOutcome outcome = await RunAsync(runner, PerlCurlCase);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        Assert.AreEqual("the harness does not run a perl command", outcome.Detail);
    }

    // The case's own command: what follows the --trace-time runtests.pl ends its arguments with.
    private static string[] CommandArguments(UpstreamCurlInvocation invocation) =>
        [.. invocation.Arguments.SkipWhile(argument => argument != "--trace-time").Skip(1)];

    private static UpstreamCaseRunner Runner(Func<UpstreamCurlInvocation, Task<int>> runCurl) =>
        Runner(runCurl, TimeSpan.FromSeconds(10));

    private static UpstreamCaseRunner Runner(Func<UpstreamCurlInvocation, Task<int>> runCurl, TimeSpan timeLimit) =>
        new(runCurl, UpstreamCurlPlatform.Unix, TimeProvider.System, timeLimit);

    private Task<UpstreamCaseOutcome> RunAsync(UpstreamCaseRunner runner, string testFile) =>
        RunWithLogDirectoryAsync(runner, testFile, CreateLogDirectory());

    private async Task<UpstreamCaseOutcome> RunWithLogDirectoryAsync(UpstreamCaseRunner runner, string testFile, string logDirectory)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test file", testFile);
        UpstreamCaseOutcome outcome = await runner.RunAsync(5, Encoding.Latin1.GetBytes(testFile), logDirectory);
        diagnostics.Act("outcome kind", outcome.Kind);
        diagnostics.Act("outcome detail", outcome.Detail);
        return outcome;
    }

    // Beside the tests, whose path has no blank in the development checkout, unlike the user's temporary folder.
    private static string CreateLogDirectory() =>
        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"runner-{Guid.NewGuid():N}")).FullName;

    // Sends a request to the emulation, reads the whole reply and writes it where --output points.
    private static async Task ExchangeAsync(IConnector connector, string request, string outputFile)
    {
        ConnectResult connected = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 8990, false), CancellationToken.None);
        await connected.Connection!.WriteAsync(Encoding.Latin1.GetBytes(request), CancellationToken.None);
        using MemoryStream reply = new();
        byte[] buffer = new byte[1024];
        int count;
        while ((count = await connected.Connection.ReadAsync(buffer, CancellationToken.None)) > 0)
        {
            reply.Write(buffer, 0, count);
        }

        await File.WriteAllBytesAsync(outputFile, reply.ToArray());
    }

    // A curl whose every exchange completes at once and which never returns until the server
    // throws: it never yields, so only a run on another thread lets the time limit start.
    private static Task<int> LoopUntilTheServerFails(IConnector connector, TaskCompletionSource<Exception> loopEnded)
    {
        try
        {
            while (connector.ConnectAsync(new ConnectTarget("127.0.0.1", 8990, false), CancellationToken.None).IsCompletedSuccessfully)
            {
            }

            return Task.FromResult(1);
        }
        catch (IOException exception)
        {
            loopEnded.SetResult(exception);
            return Task.FromResult(0);
        }
    }

    // A clock that never moves on its own: every timer made from it fires when the test says so.
    private sealed class ExpiringTimeProvider : TimeProvider
    {
        private readonly TaskCompletionSource<(TimerCallback Callback, object? State)> firstTimer = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            firstTimer.TrySetResult((callback, state));
            return new InertTimer();
        }

        // The runner starts its time limit once curl has its own thread, so this waits for the
        // timer to exist before firing it.
        public async Task ExpireTheTimeLimitAsync()
        {
            (TimerCallback callback, object? state) = await firstTimer.Task.WaitAsync(TimeSpan.FromSeconds(10));
            callback(state);
        }
    }

    private sealed class InertTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [TestMethod]
    [DataRow("%PERL -e \"print 'Test requires X' if('a' ne 'b');\"", "Test requires X")]
    [DataRow("%PERL -e 'exit((stat(\"%LOGDIR/missing\"))[9] != 5)'", "precheck command error")]
    [DataRow("%RESOLVE --ipv6 %HOSTIP", "Resolving IPv6 '127.0.0.1' didn't work")]
    public async Task RunAsync_PrecheckThatPrintsOrFails_SkipsTheCaseWithoutRunningCurl(string precheck, string expectedReason)
    {
        bool curlRan = false;
        UpstreamCaseRunner runner = Runner(_ =>
        {
            curlRan = true;
            return Task.FromResult(0);
        });
        string testFile = $"<testcase>\n<client>\n<command>\na\n</command>\n<precheck>\n{precheck}\n</precheck>\n</client>\n</testcase>\n";

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
        Assert.AreEqual(expectedReason, outcome.Detail);
        Assert.IsFalse(curlRan);
    }

    [TestMethod]
    public async Task RunAsync_ResolvePrecheckThatSucceeds_RunsTheCase()
    {
        bool curlRan = false;
        UpstreamCaseRunner runner = Runner(_ =>
        {
            curlRan = true;
            return Task.FromResult(0);
        });
        string testFile = "<testcase>\n<client>\n<command>\na\n</command>\n<precheck>\n%RESOLVE --ipv6 ::1\n</precheck>\n</client>\n</testcase>\n";

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        Assert.IsTrue(curlRan);
    }

    [TestMethod]
    public async Task RunAsync_Test1085_IsNotSkipped()
    {
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(45));
        string testFile = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "UpstreamTestData", "test1085.curltest"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
    }

    [TestMethod]
    [DataRow(1200)]
    [DataRow(1609)]
    public async Task RunAsync_GopherCase_RunsCurlAtGopherPort(int testNumber)
    {
        string? commandLine = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            commandLine = string.Join(' ', invocation.Arguments);
            return Task.FromResult(0);
        });
        string testFile = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "UpstreamTestData", $"test{testNumber}.curltest"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        Assert.AreNotEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
        Assert.Contains(":" + UpstreamCaseRunner.GopherPort + "/", commandLine!);
    }

    [TestMethod]
    public async Task RunAsync_GopherCase_RecordsTheSelectorForVerifyProtocol()
    {
        UpstreamCaseRunner runner = Runner(async invocation =>
        {
            ConnectResult connected = await invocation.Connector.ConnectAsync(new ConnectTarget("127.0.0.1", GopherServerConnector.GopherPort, false), CancellationToken.None);
            await connected.Connection!.WriteAsync("/5\r\n"u8.ToArray(), CancellationToken.None);
            return 0;
        });

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<server>\ngopher\n</server>\n<command>\ngopher://%HOSTIP:%GOPHERPORT/1/%TESTNUMBER\n</command>\n</client>\n<verify>\n<protocol crlf=\"yes\">\n/%TESTNUMBER\n</protocol>\n</verify>\n</testcase>\n");

        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
    }

    [TestMethod]
    [DataRow(5028)]
    [DataRow(2407)]
    public async Task RunAsync_CaseNamingGopherOrHttp2Port_IsNotSkippedForAMissingPort(int testNumber)
    {
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(0));
        string testFile = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "UpstreamTestData", $"test{testNumber}.curltest"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        Assert.DoesNotContain("has no value", outcome.Detail ?? string.Empty);
    }

    [TestMethod]
    public async Task RunAsync_Http2Port_HasItsValue()
    {
        string? commandLine = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            commandLine = string.Join(' ', invocation.Arguments);
            return Task.FromResult(0);
        });

        await RunAsync(runner, "<testcase>\n<client>\n<server>\nhttp\n</server>\n<command>\nhttp://%HOSTIP:%HTTP2PORT/%TESTNUMBER\n</command>\n</client>\n<verify>\n</verify>\n</testcase>\n");

        Assert.Contains(":" + UpstreamCaseRunner.Http2Port + "/", commandLine!);
    }

    [TestMethod]
    [DataRow(240)]
    [DataRow(241)]
    [DataRow(242)]
    [DataRow(263)]
    [DataRow(1324)]
    [DataRow(1408)]
    [DataRow(1456)]
    [DataRow(3202)]
    public async Task RunAsync_HttpIpv6Case_RunsCurlAtHttp6Port(int testNumber)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string? commandLine = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            commandLine = string.Join(' ', invocation.Arguments);
            return Task.FromResult(0);
        });
        string testFile = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "UpstreamTestData", $"test{testNumber}.curltest"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        diagnostics.Assert("outcome", "not skipped", $"{outcome.Kind}: {outcome.Detail}");
        Assert.AreNotEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
        Assert.Contains(":" + UpstreamCaseRunner.Http6Port, commandLine!);
    }

    [TestMethod]
    [DataRow(1435)]
    [DataRow(1436)]
    public async Task RunAsync_HttpUnixCase_RunsCurlOnTheHttpUnixPath(int testNumber)
    {
        string? commandLine = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            commandLine = string.Join(' ', invocation.Arguments);
            return Task.FromResult(0);
        });
        string testFile = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "UpstreamTestData", $"test{testNumber}.curltest"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        Assert.AreNotEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
        Assert.Contains("--unix-socket " + UpstreamCaseRunner.HttpUnixPath + " ", commandLine!);
    }

    [TestMethod]
    [DataRow(1467)]
    [DataRow(1468)]
    public async Task RunAsync_SocksUnixCase_RunsCurlWithTheSocksUnixPath(int testNumber)
    {
        string? commandLine = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            commandLine = string.Join(' ', invocation.Arguments);
            return Task.FromResult(0);
        });
        string testFile = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "UpstreamTestData", $"test{testNumber}.curltest"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        Assert.AreNotEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
        Assert.Contains("localhost" + SocksServerConnector.SocksUnixPath, commandLine!);
    }

    [TestMethod]
    public async Task RunAsync_HttpsMtlsCaseWithoutACertificateDirectory_SkipsForTheMtlsPort()
    {
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(0));
        string testFile = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "UpstreamTestData", "test2088.curltest"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        Assert.Contains("%HTTPS-MTLSPORT", outcome.Detail);
    }

    [TestMethod]
    public async Task HttpUnixSocketTarget_TheHttpUnixPath_ReachesTheSwsEmulationOnTheHttpPort()
    {
        SwsHttpServerConnector server = new(ParsedTestCase.From("<reply>\n<data>\nHTTP/1.1 200 OK\n\n</data>\n</reply>\n"));

        ConnectTarget target = UpstreamCaseRunner.HttpUnixSocketTarget(UpstreamCaseRunner.HttpUnixPath);
        ConnectResult connected = await server.ConnectAsync(target, CancellationToken.None);

        Assert.AreEqual(UpstreamCaseRunner.HttpUnixPath, target.Host);
        Assert.AreEqual(UpstreamCaseRunner.HttpPort, target.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.IsFalse(target.UseTls);
        Assert.IsNotNull(connected.Connection, connected.ErrorMessage);
    }

    [TestMethod]
    public async Task RunAsync_Ipv6Variables_HaveTheValuesOfTheInMemoryIpv6Servers()
    {
        string? commandLine = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            commandLine = string.Join(' ', invocation.Arguments);
            return Task.FromResult(0);
        });

        await RunAsync(runner, "<testcase>\n<client>\n<server>\nnone\n</server>\n<command>\n%CLIENT6IP %CLIENT6IP-NB %FTP6PORT %GOPHER6PORT\n</command>\n</client>\n</testcase>\n");

        Assert.Contains("[::1] ::1 9011 9012", commandLine!);
    }

    [TestMethod]
    [DataRow(252, UpstreamCaseRunner.Ftp6Port)]
    [DataRow(253, UpstreamCaseRunner.Ftp6Port)]
    [DataRow(254, UpstreamCaseRunner.Ftp6Port)]
    [DataRow(255, UpstreamCaseRunner.Ftp6Port)]
    [DataRow(1203, UpstreamCaseRunner.Gopher6Port)]
    public async Task RunAsync_FtpOrGopherIpv6Case_RunsCurlAtItsIpv6Port(int testNumber, string port)
    {
        string? commandLine = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            commandLine = string.Join(' ', invocation.Arguments);
            return Task.FromResult(0);
        });
        string testFile = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "UpstreamTestData", $"test{testNumber}.curltest"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        Assert.AreNotEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
        Assert.Contains("[::1]:" + port, commandLine!);
    }

    private const string LibCase =
        "<testcase>\n<client>\n<server>\nhttp\n</server>\n<tool>\nlib%TESTNUMBER\n</tool>\n"
        + "<command>\nhttp://%HOSTIP:%HTTPPORT/%TESTNUMBER %LOGDIR/ip%TESTNUMBER\n</command>\n</client>\n"
        + "<verify>\n<stdout>\nhello from lib5\n</stdout>\n<stderr>\nURL: http://%HOSTIP:%HTTPPORT/%TESTNUMBER\nstep failed\nTest ended with result 0\n</stderr>\n<errorcode>\n0\n</errorcode>\n</verify>\n</testcase>\n";

    // first.c's main() writes "URL: <url>" only when a URL follows the program's name, and always "Test ended with result <N>";
    // runner.pm passes "-" for a case with no <command>, so first.c writes "URL: -" (test1658).
    [TestMethod]
    [DataRow("http://%HOSTIP:%HTTPPORT/%TESTNUMBER", 0, "URL: http://127.0.0.1:8990/5\nstep failed\nTest ended with result 0\n")]
    [DataRow("http://%HOSTIP:%HTTPPORT/%TESTNUMBER", 7, "URL: http://127.0.0.1:8990/5\nstep failed\nTest ended with result 7\n")]
    [DataRow(null, 0, "URL: -\nstep failed\nTest ended with result 0\n")]
    [DataRow(" ", 0, "step failed\nTest ended with result 0\n")]
    public async Task RunAsync_PortedLibCase_WritesFirstCsUrlAndResultLinesAroundTheProgramsStderr(string? command, int exitCode, string expectedStandardError)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = LibRunner(new SampleLibTest("lib5", exitCode));
        string logDirectory = CreateLogDirectory();
        string commandPart = command is null ? string.Empty : $"<command>\n{command}\n</command>\n";
        string testCase = "<testcase>\n<client>\n<server>\nhttp\n</server>\n<tool>\nlib%TESTNUMBER\n</tool>\n" + commandPart + "</client>\n</testcase>\n";

        await RunWithLogDirectoryAsync(runner, testCase, logDirectory);

        string standardError = File.ReadAllText(Path.Combine(logDirectory, "stderr5"));
        diagnostics.Assert("stderr", expectedStandardError, standardError);
        Assert.AreEqual(expectedStandardError, standardError);
    }

    [TestMethod]
    public async Task RunAsync_PortedLibCase_RunsItsProgramInPlaceOfCurl()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        SampleLibTest program = new("lib5", exitCode: 0);
        UpstreamCaseRunner runner = LibRunner(program);
        string logDirectory = CreateLogDirectory();

        UpstreamCaseOutcome outcome = await RunWithLogDirectoryAsync(runner, LibCase, logDirectory);

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Passed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        LibTestContext context = program.Context!;
        string expectedArguments = $"http://127.0.0.1:8990/5 | {logDirectory.Replace('\\', '/')}/ip5";
        diagnostics.Assert("program arguments", expectedArguments, string.Join(" | ", context.Arguments));
        Assert.AreEqual(expectedArguments, string.Join(" | ", context.Arguments));
        Assert.AreEqual(5, context.TestNumber);
        StubCurlEngine engine = (StubCurlEngine)context.Engine!;
        CollectionAssert.AreEqual(context.Arguments.ToArray(), engine.Invocation.Arguments.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_PortedLibCaseReturningAnotherCode_FailsOnTheExitCode()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = LibRunner(new SampleLibTest("lib5", exitCode: 7));
        string libCaseVerifyingTheExitCodeAlone = LibCase.Replace(
            "<stderr>\nURL: http://%HOSTIP:%HTTPPORT/%TESTNUMBER\nstep failed\nTest ended with result 0\n</stderr>\n", string.Empty, StringComparison.Ordinal);

        UpstreamCaseOutcome outcome = await RunAsync(runner, libCaseVerifyingTheExitCodeAlone);

        diagnostics.Assert("outcome detail", "<verify><errorcode>: expected exit code 0, got 7", outcome.Detail);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Failed, outcome.Kind);
        Assert.AreEqual("<verify><errorcode>: expected exit code 0, got 7", outcome.Detail);
    }

    // test500's shape: <reply><data> is checked and <verify> has no <stdout>, so runtests.pl 8.22.0
    // compares the data with the program's stdout, which runner.pm makes a tool run's $CURLOUT (BL-2560).
    private static string LibCaseCheckingReplyData(string data) =>
        "<testcase>\n<reply>\n<data>\n" + data + "</data>\n</reply>\n<client>\n<server>\nhttp\n</server>\n<tool>\nlib%TESTNUMBER\n</tool>\n"
        + "<command>\nhttp://%HOSTIP:%HTTPPORT/%TESTNUMBER\n</command>\n</client>\n"
        + "<verify>\n<errorcode>\n0\n</errorcode>\n</verify>\n</testcase>\n";

    [TestMethod]
    public async Task RunAsync_LibCaseCheckingReplyDataTheProgramWroteToStdout_Passes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = LibRunner(new SampleLibTest("lib5", exitCode: 0));

        UpstreamCaseOutcome outcome = await RunAsync(runner, LibCaseCheckingReplyData("hello from lib5\n"));

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Passed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_LibCaseCheckingReplyDataTheProgramDidNotWrite_FailsOnTheProgramsStdout()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = LibRunner(new SampleLibTest("lib5", exitCode: 0));

        UpstreamCaseOutcome outcome = await RunAsync(runner, LibCaseCheckingReplyData("HTTP/1.1 200 OK\n"));

        string firstLine = outcome.Detail.Split('\n')[0];
        diagnostics.Assert("outcome detail", "the program's stdout against <reply><data> differs at byte 0 (line 1)", firstLine);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Failed, outcome.Kind);
        StringAssert.StartsWith(firstLine, "the program's stdout against <reply><data> differs at byte 0 (line 1)");
    }

    [TestMethod]
    public async Task RunAsync_LibCaseWhoseProgramIsNotPorted_IsSkippedAsNotPorted()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = LibRunner(new SampleLibTest("lib6", exitCode: 0));

        UpstreamCaseOutcome outcome = await RunAsync(runner, LibCase);

        diagnostics.Assert("skip reason", "libtest program not ported", outcome.Detail);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        Assert.AreEqual("libtest program not ported", outcome.Detail);
    }

    [TestMethod]
    [DataRow("CURLOPT_CAINFO_BLOB is not supported\n", 0, "CURLOPT_CAINFO_BLOB is not supported")]
    [DataRow("", 1, "precheck command error")]
    public async Task RunAsync_LibTestsPrecheckThatPrintsOrFails_SkipsTheCaseWithoutRunningCurl(string output, int exitCode, string expectedReason)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        PrecheckLibTest check = new(output, exitCode);
        UpstreamCaseRunner runner = PrecheckRunner(check, _ => throw new AssertFailedException("curl must not run after a failed precheck"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, LibTestsPrecheckCase);

        diagnostics.Assert("skip reason", expectedReason, outcome.Detail);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        Assert.AreEqual(expectedReason, outcome.Detail);
        CollectionAssert.AreEqual(new[] { "check" }, check.Context!.Arguments.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_LibTestsPrecheckThatPassesQuietly_RunsTheCase()
    {
        bool curlRan = false;
        UpstreamCaseRunner runner = PrecheckRunner(new PrecheckLibTest("", 0), _ =>
        {
            curlRan = true;
            return Task.FromResult(0);
        });

        UpstreamCaseOutcome outcome = await RunAsync(runner, LibTestsPrecheckCase);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        Assert.IsTrue(curlRan);
    }

    [TestMethod]
    public async Task RunAsync_LibTestsPrecheckThatThrows_SkipsWithPrecheckCommandError()
    {
        UpstreamCaseRunner runner = PrecheckRunner(new PrecheckLibTest(null, 0), _ => throw new AssertFailedException("curl must not run after a failed precheck"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, LibTestsPrecheckCase);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        Assert.AreEqual("precheck command error", outcome.Detail);
    }

    private const string LibTestsPrecheckCase = "<testcase>\n<client>\n<command>\na\n</command>\n<precheck>\n%LIBTESTS lib678 check\n</precheck>\n</client>\n</testcase>\n";

    private static UpstreamCaseRunner PrecheckRunner(ILibTest check, Func<UpstreamCurlInvocation, Task<int>> runCurl) =>
        new(
            runCurl,
            UpstreamCurlPlatform.Unix,
            TimeProvider.System,
            TimeSpan.FromSeconds(10),
            libTests: new UpstreamLibTests(new LibTestPrograms([check]), invocation => new StubCurlEngine(invocation)));

    // lib678 as a precheck sees it: keeps its context, prints its output and returns its exit code, or throws for null output.
    private sealed class PrecheckLibTest(string? output, int exitCode) : ILibTest
    {
        public LibTestContext? Context { get; private set; }

        public string ProgramName => "lib678";

        public async Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
        {
            Context = context;
            if (output is null)
            {
                throw new InvalidOperationException("the check failed to start");
            }

            await context.StandardOutput.WriteAsync(Encoding.ASCII.GetBytes(output), cancellationToken);
            return new LibTestResult(exitCode);
        }
    }

    private const string UnitCase =
        "<testcase>\n<client>\n<features>\nunittest\nDebug\n</features>\n<name>\nunit test\n</name>\n</client>\n"
        + "<verify>\n<stdout>\nchecks run\n</stdout>\n</verify>\n</testcase>\n";

    [TestMethod]
    public async Task RunAsync_PortedUnitProgram_RunsInPlaceOfCurlAndItsStdoutAndExitCodeAreVerified()
    {
        SampleUnitProgram program = new("unit5", failures: 0);

        UpstreamCaseOutcome outcome = await RunAsync(UnitRunner(program), UnitCase);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        Assert.AreEqual(5, program.Context!.TestNumber);
        // runner.pm's "-" for a case with no <command>.
        CollectionAssert.AreEqual(new[] { "-" }, program.Context.Arguments.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_UnitProgramWithFailedChecks_FailsTheCase()
    {
        UpstreamCaseOutcome outcome = await RunAsync(UnitRunner(new SampleUnitProgram("unit5", failures: 2)), UnitCase);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Failed, outcome.Kind);
    }

    [TestMethod]
    public async Task RunAsync_UnitProgramNotPorted_IsSkippedNamingIt()
    {
        UpstreamCaseOutcome outcome = await RunAsync(UnitRunner(new SampleUnitProgram("unit6", failures: 0)), UnitCase);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        Assert.AreEqual("Curl has no port of unit5", outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_UnittestCaseWithNoUnitPrograms_IsSkippedNamingTheProgram()
    {
        UpstreamCaseRunner runner = new(_ => throw new AssertFailedException("curl must not run for a unit case"), UpstreamCurlPlatform.Unix, TimeProvider.System, TimeSpan.FromSeconds(10));

        UpstreamCaseOutcome outcome = await RunAsync(runner, UnitCase);

        Assert.AreEqual("Curl has no port of unit5", outcome.Detail);
    }

    private static UpstreamCaseRunner UnitRunner(IUnitProgram program) =>
        new(
            _ => throw new AssertFailedException("curl must not run for a unit case"),
            UpstreamCurlPlatform.Unix,
            TimeProvider.System,
            TimeSpan.FromSeconds(10),
            unitPrograms: new UnitPrograms([program]));

    // A unit program that keeps its context, writes a line to stdout and returns its failure count.
    private sealed class SampleUnitProgram(string programName, int failures) : IUnitProgram
    {
        public UnitProgramContext? Context { get; private set; }

        public string ProgramName => programName;

        public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
        {
            Context = context;
            await context.StandardOutput.WriteAsync("checks run\n"u8.ToArray(), cancellationToken);
            return new UnitProgramResult(failures);
        }
    }

    private static UpstreamCaseRunner LibRunner(ILibTest program) =>
        new(
            _ => throw new AssertFailedException("curl must not run for a lib case"),
            UpstreamCurlPlatform.Unix,
            TimeProvider.System,
            TimeSpan.FromSeconds(10),
            libTests: new UpstreamLibTests(new LibTestPrograms([program]), invocation => new StubCurlEngine(invocation)));

    // A libtest program that keeps its context, writes a line to stdout and one to stderr, and returns its exit code.
    private sealed class SampleLibTest(string programName, int exitCode) : ILibTest
    {
        public LibTestContext? Context { get; private set; }

        public string ProgramName => programName;

        public async Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
        {
            Context = context;
            await context.StandardOutput.WriteAsync("hello from lib5\n"u8.ToArray(), cancellationToken);
            await context.StandardError.WriteLineAsync("step failed".AsMemory(), cancellationToken);
            return new LibTestResult(exitCode);
        }
    }

    [TestMethod]
    [DataRow(567)]
    [DataRow(568)]
    [DataRow(569)]
    [DataRow(570)]
    [DataRow(571)]
    [DataRow(572)]
    [DataRow(577)]
    [DataRow(689)]
    [DataRow(3100)]
    public async Task RunAsync_RtspPortCase_IsSkippedForItsToolNotForRtspPort(int testNumber)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCaseRunner runner = Runner(_ => throw new AssertFailedException("curl must not run"));
        string testFile = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "UpstreamTestData", $"test{testNumber}.curltest"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        diagnostics.Assert("skip reason", "libtest program not ported", outcome.Detail);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        Assert.AreEqual("libtest program not ported", outcome.Detail);
    }

    [TestMethod]
    [DataRow(5, UpstreamCaseOutcomeKind.Failed, "postcheck FAILED: exit code 1")]
    [DataRow(0, UpstreamCaseOutcomeKind.Passed, "")]
    public async Task RunAsync_Postcheck_FailsTheCaseWhenItExitsNonZero(int epoch, UpstreamCaseOutcomeKind expectedKind, string expectedDetail)
    {
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(0));
        string check = $"%PERL -e 'exit((stat(\"%LOGDIR/missing\"))[9] != {epoch})'";
        string testFile = "<testcase>\n<client>\n<command>\na\n</command>\n<precheck>\n%PERL -e \"print 'x' if('a' ne 'a');\"\n</precheck>\n</client>\n"
            + $"<verify>\n<postcheck>\n{check}\n</postcheck>\n</verify>\n</testcase>\n";

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        Assert.AreEqual(expectedKind, outcome.Kind, outcome.Detail);
        Assert.AreEqual(expectedDetail, outcome.Detail ?? "");
    }

    [TestMethod]
    public void HttpsPort_IsFiveDigitsAndMatchesTheConnectorPort()
    {
        // Upstream's runner picks ports above 9999; test2100's Content-Length: 47 assumes a five-digit %HTTPSPORT.
        Assert.HasCount(5, UpstreamCaseRunner.HttpsPort);
        Assert.AreEqual(UpstreamCaseRunner.HttpsPort, HttpsServerConnector.HttpsPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task RunAsync_HttpsCaseWithACertificateDirectory_ServesTlsOnTheHttpsPort()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string certificateDirectory = CreateLogDirectory();
        Directory.CreateDirectory(Path.Combine(certificateDirectory, "certs"));
        using (System.Security.Cryptography.ECDsa key = System.Security.Cryptography.ECDsa.Create())
        {
            System.Security.Cryptography.X509Certificates.CertificateRequest request = new("CN=localhost", key, System.Security.Cryptography.HashAlgorithmName.SHA256);
            using System.Security.Cryptography.X509Certificates.X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            await File.WriteAllTextAsync(Path.Combine(certificateDirectory, "certs", "test-localhost.pem"), certificate.ExportCertificatePem() + "\n" + key.ExportPkcs8PrivateKeyPem() + "\n");
        }

        string[]? arguments = null;
        IOException? failure = null;
        UpstreamCaseRunner runner = Runner(async invocation =>
        {
            arguments = [.. CommandArguments(invocation)];
            ConnectResult connected = await invocation.Connector.ConnectAsync(new ConnectTarget("127.0.0.1", HttpsServerConnector.HttpsPort, false), CancellationToken.None);
            await using IConnection connection = connected.Connection!;
            await connection.WriteAsync("GET / HTTP/1.1\r\n\r\n"u8.ToArray(), CancellationToken.None);
            failure = await Assert.ThrowsExactlyAsync<IOException>(() => connection.ReadAsync(new byte[16], CancellationToken.None).AsTask());
            return 0;
        });
        string testFile = "<testcase>\n<client>\n<server>\nhttps\n</server>\n<command option=\"no-output,no-include\">\nhttps://localhost:%HTTPSPORT/\n</command>\n</client>\n</testcase>\n";

        UpstreamCaseOutcome outcome = await runner.RunAsync(5, Encoding.Latin1.GetBytes(testFile), CreateLogDirectory(), null, certificateDirectory);
        diagnostics.Act("arguments", string.Join(' ', arguments ?? []));

        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        CollectionAssert.AreEqual(new[] { "https://localhost:" + UpstreamCaseRunner.HttpsPort + "/" }, arguments);
        Assert.AreEqual(System.Net.Sockets.SocketError.ConnectionReset, ((System.Net.Sockets.SocketException)failure!.InnerException!).SocketErrorCode, "a plain-text request to the TLS server resets the connection, as stunnel does");
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task RunAsync_SmtpsCaseWithACertificateDirectory_ServesTlsOnTheSmtpsPort()
    {
        string certificateDirectory = CreateLogDirectory();
        Directory.CreateDirectory(Path.Combine(certificateDirectory, "certs"));
        using (System.Security.Cryptography.ECDsa key = System.Security.Cryptography.ECDsa.Create())
        {
            System.Security.Cryptography.X509Certificates.CertificateRequest request = new("CN=localhost", key, System.Security.Cryptography.HashAlgorithmName.SHA256);
            using System.Security.Cryptography.X509Certificates.X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            await File.WriteAllTextAsync(Path.Combine(certificateDirectory, "certs", "test-localhost.pem"), certificate.ExportCertificatePem() + "\n" + key.ExportPkcs8PrivateKeyPem() + "\n");
        }

        string[]? arguments = null;
        int read = -1;
        UpstreamCaseRunner runner = Runner(async invocation =>
        {
            arguments = [.. CommandArguments(invocation)];
            ConnectResult connected = await invocation.Connector.ConnectAsync(new ConnectTarget("127.0.0.1", MailTlsServerConnector.SmtpsPort, false), CancellationToken.None);
            await using IConnection connection = connected.Connection!;
            await connection.WriteAsync("EHLO x\r\n"u8.ToArray(), CancellationToken.None);
            read = await connection.ReadAsync(new byte[16], CancellationToken.None);
            return 0;
        });
        string testFile = "<testcase>\n<client>\n<server>\nsmtps\n</server>\n<command option=\"no-output,no-include\">\nsmtps://localhost:%SMTPSPORT/ %IMAPSPORT %POP3SPORT\n</command>\n</client>\n</testcase>\n";

        UpstreamCaseOutcome outcome = await runner.RunAsync(5, Encoding.Latin1.GetBytes(testFile), CreateLogDirectory(), null, certificateDirectory);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        CollectionAssert.AreEqual(new[] { "smtps://localhost:" + UpstreamCaseRunner.SmtpsPort + "/", UpstreamCaseRunner.ImapsPort, UpstreamCaseRunner.Pop3sPort }, arguments);
        Assert.AreEqual(0, read, "a plain-text command to the TLS server ends the connection");
    }

    [TestMethod]
    public async Task RunAsync_SmtpsCaseWithoutACertificateDirectory_SkipsForTheSmtpsPort()
    {
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(0));

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<server>\nsmtps\n</server>\n<command>\nsmtps://localhost:%SMTPSPORT/\n</command>\n</client>\n</testcase>\n");

        Assert.AreEqual("the harness has no value for %SMTPSPORT", outcome.Detail);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task RunAsync_FtpsCaseWithACertificateDirectory_ServesTlsOnTheFtpsPort()
    {
        string certificateDirectory = CreateLogDirectory();
        Directory.CreateDirectory(Path.Combine(certificateDirectory, "certs"));
        using (System.Security.Cryptography.ECDsa key = System.Security.Cryptography.ECDsa.Create())
        {
            System.Security.Cryptography.X509Certificates.CertificateRequest request = new("CN=localhost", key, System.Security.Cryptography.HashAlgorithmName.SHA256);
            using System.Security.Cryptography.X509Certificates.X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            await File.WriteAllTextAsync(Path.Combine(certificateDirectory, "certs", "test-localhost.pem"), certificate.ExportCertificatePem() + "\n" + key.ExportPkcs8PrivateKeyPem() + "\n");
        }

        string[]? arguments = null;
        int read = -1;
        UpstreamCaseRunner runner = Runner(async invocation =>
        {
            arguments = [.. CommandArguments(invocation)];
            ConnectResult connected = await invocation.Connector.ConnectAsync(new ConnectTarget("127.0.0.1", FtpsServerConnector.FtpsPort, false), CancellationToken.None);
            await using IConnection connection = connected.Connection!;
            await connection.WriteAsync("USER x\r\n"u8.ToArray(), CancellationToken.None);
            read = await connection.ReadAsync(new byte[16], CancellationToken.None);
            return 0;
        });
        string testFile = "<testcase>\n<client>\n<server>\nftps\n</server>\n<command option=\"no-output,no-include\">\nftps://localhost:%FTPSPORT/\n</command>\n</client>\n</testcase>\n";

        UpstreamCaseOutcome outcome = await runner.RunAsync(5, Encoding.Latin1.GetBytes(testFile), CreateLogDirectory(), null, certificateDirectory);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        CollectionAssert.AreEqual(new[] { "ftps://localhost:" + UpstreamCaseRunner.FtpsPort + "/" }, arguments);
        Assert.AreEqual(0, read, "a plain-text command to the TLS server ends the connection");
    }

    [TestMethod]
    public async Task RunAsync_FtpsCaseWithoutACertificateDirectory_SkipsForTheFtpsPort()
    {
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(0));

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<server>\nftps\n</server>\n<command>\nftps://localhost:%FTPSPORT/\n</command>\n</client>\n</testcase>\n");

        Assert.AreEqual("the harness has no value for %FTPSPORT", outcome.Detail);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task RunAsync_HttpsProxyCaseWithACertificateDirectory_ServesTlsOnTheHttpsProxyPort()
    {
        string certificateDirectory = CreateLogDirectory();
        Directory.CreateDirectory(Path.Combine(certificateDirectory, "certs"));
        using (System.Security.Cryptography.ECDsa key = System.Security.Cryptography.ECDsa.Create())
        {
            System.Security.Cryptography.X509Certificates.CertificateRequest request = new("CN=localhost", key, System.Security.Cryptography.HashAlgorithmName.SHA256);
            using System.Security.Cryptography.X509Certificates.X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            await File.WriteAllTextAsync(Path.Combine(certificateDirectory, "certs", "test-localhost.pem"), certificate.ExportCertificatePem() + "\n" + key.ExportPkcs8PrivateKeyPem() + "\n");
        }

        string[]? arguments = null;
        int read = -1;
        UpstreamCaseRunner runner = Runner(async invocation =>
        {
            arguments = [.. CommandArguments(invocation)];
            ConnectResult connected = await invocation.Connector.ConnectAsync(new ConnectTarget("127.0.0.1", HttpsProxyServerConnector.HttpsProxyPort, false), CancellationToken.None);
            await using IConnection connection = connected.Connection!;
            await connection.WriteAsync("GET http://x/ HTTP/1.1\r\n\r\n"u8.ToArray(), CancellationToken.None);
            read = await connection.ReadAsync(new byte[16], CancellationToken.None);
            return 0;
        });
        string testFile = "<testcase>\n<client>\n<server>\nhttp\nhttps-proxy\n</server>\n<command option=\"no-output,no-include\">\n-x https://localhost:%HTTPSPROXYPORT\n</command>\n</client>\n</testcase>\n";

        UpstreamCaseOutcome outcome = await runner.RunAsync(5, Encoding.Latin1.GetBytes(testFile), CreateLogDirectory(), null, certificateDirectory);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        CollectionAssert.AreEqual(new[] { "-x", "https://localhost:" + UpstreamCaseRunner.HttpsProxyPort }, arguments);
        Assert.AreEqual(0, read, "a plain-text request to the TLS proxy ends the connection");
    }

    [TestMethod]
    public async Task RunAsync_HttpsProxyCaseWithoutACertificateDirectory_SkipsForTheHttpsProxyPort()
    {
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(0));

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<server>\nhttps-proxy\n</server>\n<command>\n-x https://localhost:%HTTPSPROXYPORT http://x/\n</command>\n</client>\n</testcase>\n");

        Assert.AreEqual("the harness has no value for %HTTPSPROXYPORT", outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_HttpsCaseWithoutACertificateDirectory_SkipsForTheHttpsPort()
    {
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(0));

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<server>\nhttps\n</server>\n<command>\nhttps://localhost:%HTTPSPORT/\n</command>\n</client>\n</testcase>\n");

        Assert.AreEqual("the harness has no value for %HTTPSPORT", outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_HttpsCaseNotNamingTheHttpsPortWithoutACertificateDirectory_Runs()
    {
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(0));

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<server>\nhttps\n</server>\n<command option=\"no-output,no-include\">\nhttp://localhost/\n</command>\n</client>\n</testcase>\n");

        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
    }
}
