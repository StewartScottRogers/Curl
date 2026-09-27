using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamCaseRunner"/> with a hand-written curl: the arguments it builds as
/// <c>runtests.pl</c> does, the files and standard input it prepares, the outcomes it reports, and
/// the failures it turns a hang or an exception into.
/// </summary>
[TestClass]
public sealed class UpstreamCaseRunnerTests
{
    private const string HttpCase =
        "<testcase>\n<reply>\n<data crlf=\"headers\">\nHTTP/1.1 200 OK\n\nbody\n</data>\n</reply>\n"
        + "<client>\n<server>\nhttp\n</server>\n<command>\nhttp://%HOSTIP:%HTTPPORT/%TESTNUMBER\n</command>\n</client>\n"
        + "<verify>\n<protocol crlf=\"yes\">\nGET /%TESTNUMBER HTTP/1.1\nHost: %HOSTIP:%HTTPPORT\nUser-Agent: curl/%VERSION\n\n</protocol>\n</verify>\n</testcase>\n";

    [TestMethod]
    public async Task RunAsync_CaseThatMatches_Passes()
    {
        List<string>? arguments = null;
        UpstreamCaseRunner runner = Runner(async invocation =>
        {
            arguments = [.. invocation.Arguments];
            await ExchangeAsync(invocation.Connector, "GET /5 HTTP/1.1\r\nHost: 127.0.0.1:8990\r\nUser-Agent: curl/8.21.0\r\n\r\n", invocation.Arguments[1]);
            return 0;
        });

        UpstreamCaseOutcome outcome = await RunAsync(runner, HttpCase);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        Assert.AreEqual("--output", arguments![0]);
        CollectionAssert.AreEqual(new[] { "--include", "http://127.0.0.1:8990/5" }, arguments.Skip(2).ToArray());
    }

    [TestMethod]
    public async Task RunAsync_CaseThatDiffers_FailsWithTheFirstDifference()
    {
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(3));

        UpstreamCaseOutcome outcome = await RunAsync(runner, HttpCase);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Failed, outcome.Kind);
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
        string[]? arguments = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            arguments = [.. invocation.Arguments];
            return Task.FromResult(0);
        });

        await RunAsync(runner, $"<testcase>\n<client>\n<command{commandAttributes}>\na\n</command>\n</client>\n{verify}</testcase>\n");

        CollectionAssert.AreEqual(expected, arguments!.Select(argument => argument.EndsWith("/curl.out", StringComparison.Ordinal) ? "*" : argument).ToArray());
    }

    [TestMethod]
    public async Task RunAsync_WritesClientFilesAndGivesStdinAndSavesTheStandardStreams()
    {
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

        UpstreamCaseOutcome outcome = await runner.RunAsync(5, Encoding.Latin1.GetBytes(testFile), logDirectory);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        Assert.AreEqual("typed\n", stdin);
        Assert.AreEqual("file body", File.ReadAllText($"{logDirectory}/stdout5"));
    }

    [TestMethod]
    public async Task RunAsync_OutputFileCurlWrote_IsComparedWithTheReplyData()
    {
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            File.WriteAllText(invocation.Arguments[1], "served\n");
            return Task.FromResult(0);
        });

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<reply>\n<data>\nserved\n</data>\n</reply>\n<client>\n<command>\na\n</command>\n</client>\n</testcase>\n");

        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_CaseTheHarnessCannotRun_IsSkippedWithTheReason()
    {
        UpstreamCaseRunner runner = Runner(_ => throw new AssertFailedException("curl must not run"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<command>\nftp://%HOSTIP:%FTPPORT/\n</command>\n</client>\n</testcase>\n");

        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        Assert.AreEqual("the harness has no value for %FTPPORT", outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_FileThatDoesNotParse_IsSkippedWithTheParseFailure()
    {
        UpstreamCaseRunner runner = Runner(_ => throw new AssertFailedException("curl must not run"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<reply>\n<data>\n</testcase>\n");

        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind);
        Assert.IsFalse(string.IsNullOrEmpty(outcome.Detail));
    }

    [TestMethod]
    public async Task RunAsync_FeaturesDecideIfBlocks()
    {
        UpstreamCaseRunner runner = new(_ => Task.FromResult(0), UpstreamCurlPlatform.Windows, TimeProvider.System, TimeSpan.FromSeconds(10));
        string testFile = "<testcase>\n<client>\n<command>\na\n</command>\n</client>\n<verify>\n<errorcode>\n%if win32\n0\n%else\n1\n%endif\n</errorcode>\n</verify>\n</testcase>\n";

        UpstreamCaseOutcome outcome = await RunAsync(runner, testFile);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_CurlThatNeverFinishes_FailsAfterTheTimeLimit()
    {
        ExpiringTimeProvider time = new();
        UpstreamCaseRunner runner = new(_ => new TaskCompletionSource<int>().Task, UpstreamCurlPlatform.Unix, time, TimeSpan.FromMilliseconds(1));

        Task<UpstreamCaseOutcome> running = RunAsync(runner, "<testcase>\n<client>\n<command>\na\n</command>\n</client>\n</testcase>\n");
        time.ExpireEveryTimer();
        UpstreamCaseOutcome outcome = await running;

        Assert.AreEqual(UpstreamCaseOutcomeKind.Failed, outcome.Kind);
        Assert.AreEqual("curl did not finish within 0.001 seconds", outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_CurlThatThrows_FailsNamingTheException()
    {
        UpstreamCaseRunner runner = Runner(_ => throw new InvalidOperationException("boom"));

        UpstreamCaseOutcome outcome = await RunAsync(runner, "<testcase>\n<client>\n<command>\na\n</command>\n</client>\n</testcase>\n");

        Assert.AreEqual(UpstreamCaseOutcomeKind.Failed, outcome.Kind);
        Assert.AreEqual("curl threw InvalidOperationException: boom", outcome.Detail);
    }

    [TestMethod]
    public async Task RunAsync_CurlReachingForUdp_GetsAnUnreachableConnector()
    {
        IDatagramConnector? datagramConnector = null;
        UpstreamCaseRunner runner = Runner(invocation =>
        {
            datagramConnector = invocation.DatagramConnector;
            return Task.FromResult(0);
        });

        await RunAsync(runner, "<testcase>\n<client>\n<command>\na\n</command>\n</client>\n</testcase>\n");

        Assert.IsInstanceOfType<UnreachableDatagramConnector>(datagramConnector);
    }

    [TestMethod]
    public async Task RunAsync_LogDirectoryWithABlank_Throws()
    {
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(0));

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => runner.RunAsync(1, ReadOnlyMemory<byte>.Empty, "/a b"));
    }

    [TestMethod]
    public async Task RunAsync_NullLogDirectory_Throws()
    {
        UpstreamCaseRunner runner = Runner(_ => Task.FromResult(0));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => runner.RunAsync(1, ReadOnlyMemory<byte>.Empty, null!));
    }

    private static UpstreamCaseRunner Runner(Func<UpstreamCurlInvocation, Task<int>> runCurl) =>
        new(runCurl, UpstreamCurlPlatform.Unix, TimeProvider.System, TimeSpan.FromSeconds(10));

    private static Task<UpstreamCaseOutcome> RunAsync(UpstreamCaseRunner runner, string testFile) =>
        runner.RunAsync(5, Encoding.Latin1.GetBytes(testFile), CreateLogDirectory());

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

    // A clock that never moves on its own: every timer made from it fires when the test says so.
    private sealed class ExpiringTimeProvider : TimeProvider
    {
        private readonly List<(TimerCallback Callback, object? State)> timers = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            timers.Add((callback, state));
            return new InertTimer();
        }

        public void ExpireEveryTimer()
        {
            foreach ((TimerCallback callback, object? state) in timers.ToArray())
            {
                callback(state);
            }
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
}
