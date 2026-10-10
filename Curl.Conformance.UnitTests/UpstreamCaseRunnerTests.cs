using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamCaseRunner"/> with a hand-written curl: the arguments it builds as
/// <c>runtests.pl</c> does, the files and standard input it prepares, the outcomes it reports, and
/// the failures it turns a hang or an exception into.
/// </summary>
[TestClass]
public sealed class UpstreamCaseRunnerTests
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
            await ExchangeAsync(invocation.Connector, "GET /5 HTTP/1.1\r\nHost: 127.0.0.1:8990\r\nUser-Agent: curl/8.21.0\r\n\r\n", invocation.Arguments[1]);
            return 0;
        });

        UpstreamCaseOutcome outcome = await RunAsync(runner, HttpCase);

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Passed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        diagnostics.Assert("first argument", "--output", arguments![0]);
        Assert.AreEqual("--output", arguments![0]);
        diagnostics.Assert("arguments after the output file", "--include http://127.0.0.1:8990/5", string.Join(' ', arguments.Skip(2)));
        CollectionAssert.AreEqual(new[] { "--include", "http://127.0.0.1:8990/5" }, arguments.Skip(2).ToArray());
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
    [DataRow("", "", new[] { "--output", "*", "--include", "a" })]
    [DataRow(" option=\"no-include\"", "", new[] { "--output", "*", "a" })]
    [DataRow(" option=\"no-output,no-include\"", "", new[] { "a" })]
    [DataRow("", "<verify>\n<stdout>\n</stdout>\n</verify>\n", new[] { "--include", "a" })]
    [DataRow(" option=\"force-output\"", "<verify>\n<stdout>\n</stdout>\n</verify>\n", new[] { "--output", "*", "--include", "a" })]
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
        diagnostics.Assert("arguments", string.Join(' ', expected), string.Join(' ', arguments!.Select(argument => argument.EndsWith("/curl.out", StringComparison.Ordinal) ? "*" : argument).ToArray()));
        CollectionAssert.AreEqual(expected, arguments!.Select(argument => argument.EndsWith("/curl.out", StringComparison.Ordinal) ? "*" : argument).ToArray());
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

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<command>\nhttp://%CLIENT6IP/\n</command>\n</client>\n</testcase>\n");

        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        diagnostics.Assert("detail", "the harness has no value for %CLIENT6IP", outcome.Detail);
        Assert.AreEqual("the harness has no value for %CLIENT6IP", outcome.Detail);
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
            await ExchangeAsync(invocation.Connector, "GET /5 HTTP/1.1\r\nHost: 127.0.0.1:8990\r\nUser-Agent: curl/8.21.0\r\n\r\n", invocation.Arguments[1]);
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
            await ExchangeAsync(invocation.Connector, "GET /5 HTTP/1.1\r\nHost: 127.0.0.1:8990\r\nUser-Agent: curl/8.21.0\r\n\r\n", invocation.Arguments[1]);
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
    public async Task RunAsync_CurlReachingForUdp_GetsAnUnreachableConnector()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IDatagramConnector? datagramConnector = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            datagramConnector = invocation.DatagramConnector;
            return Task.FromResult(0);
        });

        await RunAsync(runner, "<testcase>\n<client>\n<command>\na\n</command>\n</client>\n</testcase>\n");

        diagnostics.Assert("datagram connector type", nameof(UnreachableDatagramConnector), datagramConnector?.GetType().Name ?? "(none)");
        Assert.IsInstanceOfType<UnreachableDatagramConnector>(datagramConnector);
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
            arguments = invocation.Arguments;
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
            arguments = invocation.Arguments;
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
            arguments = [.. invocation.Arguments];
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
        string testFile = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "UpstreamTestData", "test1085.rawhttp"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
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
        string testFile = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "UpstreamTestData", $"test{testNumber}.rawhttp"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        diagnostics.Assert("outcome", "not skipped", $"{outcome.Kind}: {outcome.Detail}");
        Assert.AreNotEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
        Assert.Contains(":" + UpstreamCaseRunner.Http6Port, commandLine!);
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
        string testFile = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "UpstreamTestData", $"test{testNumber}.rawhttp"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        diagnostics.Assert("skip reason", "the harness does not act on <client><tool>", outcome.Detail);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        Assert.AreEqual("the harness does not act on <client><tool>", outcome.Detail);
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
}
