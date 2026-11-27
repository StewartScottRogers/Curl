using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Engine;
using Curl.Original.Protocol.Http;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that a malformed <c>--resolve</c> entry exits 49 as each transfer starts, as curl 8.22.0 does: it loads the
/// list before it parses the URL (AF-0807) and before it looks for a connection to reuse (STD-225, BL-6022,
/// measured with <c>Record-CurlExchange.ps1</c> on 2026-11-26).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerMalformedResolveTests
{
    private const string Expected = "curl: (49) Could not parse CURLOPT_RESOLVE entry 'x:'";

    /// <summary>How long a run that should end at once may take before the test fails rather than hangs.</summary>
    private static readonly TimeSpan HangGuard = TimeSpan.FromSeconds(30);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("/")]
    [DataRow("http://")]
    [DataRow(":1")]
    public async Task RunAsync_MalformedResolveWithRejectedUrl_Exits49(string url)
    {
        (int exitCode, string error) = await RunAsync("-sS", "--resolve", "x:", url);

        Assert.AreEqual(49, exitCode);
        Assert.AreEqual(Expected, error.TrimEnd());
    }

    [TestMethod]
    public async Task RunAsync_MalformedResolveWithRejectedQueryUrl_Exits3AsCurlsToolFailsTheQueryFirst()
    {
        (int exitCode, string error) = await RunAsync("-sS", "-G", "--url-query", "a=b", "--resolve", "x:", "http://a b/");

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual("curl: (3) Could not parse the URL, failed to set query", error.TrimEnd());
    }

    [TestMethod]
    public async Task RunAsync_VerboseMalformedResolveAfterGoodEntry_PrintsAddedLineAndEntryInfoThenExits49()
    {
        (int exitCode, string error) = await RunAsync("-v", "--resolve", "good:80:127.0.0.1", "--resolve", "x:", "/");

        Assert.AreEqual(49, exitCode);
        Assert.AreEqual("* [DNS] added good:80:127.0.0.1 to cache\n* Could not parse CURLOPT_RESOLVE entry 'x:'\n" + Expected, error.Replace("\r\n", "\n").TrimEnd());
    }

    [TestMethod]
    public async Task RunAsync_WellFormedResolveWithRejectedUrl_StillExits3()
    {
        (int exitCode, _) = await RunAsync("-sS", "--resolve", "x:80:127.0.0.1", "/");

        Assert.AreEqual(3, exitCode);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_MalformedResolveInALaterGroupOnAReusableConnection_Exits49WithoutSendingTheRequest(bool parallel)
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello")]);
        MemoryStream standardOutput = new();
        string[] arguments =
        [
            .. parallel ? new[] { "-Z" } : [],
            "-sS", "http://127.0.0.1:24000/hello",
            "--next", "-sS", "--resolve", "bad", "http://127.0.0.1:24000/json", "-w", "[%{exitcode} %{num_connects} %{conn_id}]\n",
        ];

        (int exitCode, string error) = await RunAsync(server, standardOutput, arguments);

        Assert.AreEqual(49, exitCode);
        Assert.AreEqual("curl: (49) Could not parse CURLOPT_RESOLVE entry 'bad'", error.TrimEnd());
        string output = Encoding.Latin1.GetString(standardOutput.ToArray());
        // Under -Z the transfers report in completion order, so only the two outputs themselves are pinned.
        Assert.AreEqual("hello", output.Replace("[49 0 -1]\n", string.Empty, StringComparison.Ordinal));
        StringAssert.Contains(output, "[49 0 -1]\n");
        Assert.HasCount(1, server.Targets);
    }

    [TestMethod]
    public async Task RunAsync_ParallelMalformedResolveBesideARunningTransfer_ReportsItBeforeThatTransfersBody()
    {
        // The server answers /hello only once something is on standard output, so the failure must be reported while
        // /hello still runs; waiting for its host, the failing transfer would never report and the run would not end.
        FirstWriteSignallingStream standardOutput = new();
        HoldingServer server = new(releasedBy: standardOutput.Written.Task);

        (int exitCode, string error) = await RunAsync(
                server,
                standardOutput,
                [
                    "-sS", "-Z", "http://127.0.0.1:24000/hello",
                    "--next", "-sS", "--resolve", "bad", "-w", "[%{exitcode}]\n", "http://127.0.0.1:24000/json",
                ])
            .WaitAsync(HangGuard, TestContext.CancellationToken);

        Assert.AreEqual(49, exitCode);
        Assert.AreEqual("curl: (49) Could not parse CURLOPT_RESOLVE entry 'bad'", error.TrimEnd());
        Assert.AreEqual("[49]\n/hello", Encoding.Latin1.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_ParallelMalformedResolveBehindATransferWaitingForTheHost_FreesThatTransferAsItEnds()
    {
        // curl 8.22.0 dials /b once the failing transfer ends, before /a is answered (measured 2026-11-26, BL-6040).
        MemoryStream standardOutput = new();
        HoldingServer server = new(releasedBy: null);

        (int exitCode, _) = await RunAsync(
                server,
                standardOutput,
                [
                    "-sS", "-Z", "http://127.0.0.1:24000/a", "http://127.0.0.1:24000/b",
                    "--next", "-sS", "--resolve", "bad", "-w", "[%{exitcode}]\n", "http://127.0.0.1:24000/c",
                ])
            .WaitAsync(HangGuard, TestContext.CancellationToken);

        Assert.AreEqual(49, exitCode);
        Assert.AreEqual("[49]\n/b/a", Encoding.Latin1.GetString(standardOutput.ToArray()));
    }

    private static Task<(int ExitCode, string Error)> RunAsync(params string[] arguments) =>
        RunAsync(new ScriptedConnector([]), new MemoryStream(), arguments);

    private static Task<(int ExitCode, string Error)> RunAsync(ScriptedConnector server, MemoryStream standardOutput, string[] arguments) =>
        RunAsync(
            new HttpProtocolHandler(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false))),
            standardOutput,
            arguments);

    private static async Task<(int ExitCode, string Error)> RunAsync(IProtocolHandler http, MemoryStream standardOutput, string[] arguments)
    {
        MemoryStream standardError = new();
        InMemoryFileSystem files = new();
        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http]), [], loadResolveEntries: events => events.ReportInfo("[DNS] added good:80:127.0.0.1 to cache")),
                files,
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false)
            .RunAsync(arguments);

        return (exitCode, Encoding.UTF8.GetString(standardError.ToArray()));
    }

    /// <summary>
    /// An <c>http</c> server whose every transfer writes its URL path as its body: the first one only once
    /// <paramref name="releasedBy" /> completes or, when that is <see langword="null" />, once a second one has
    /// written its body.
    /// </summary>
    private sealed class HoldingServer(Task? releasedBy) : IProtocolHandler
    {
        private readonly TaskCompletionSource secondAnswered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int started;

        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["http"];

        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            bool isFirst = Interlocked.Increment(ref started) == 1;
            if (isFirst)
            {
                await (releasedBy ?? secondAnswered.Task);
            }

            string path = context.Url.AbsolutePath;
            await context.Output.WriteAsync(Encoding.ASCII.GetBytes(path), CancellationToken.None);
            if (!isFirst)
            {
                secondAnswered.TrySetResult();
            }

            return TransferResult.Success(path.Length);
        }
    }

    /// <summary>A memory stream that signals its first write.</summary>
    private sealed class FirstWriteSignallingStream : MemoryStream
    {
        public TaskCompletionSource Written { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void Write(byte[] buffer, int offset, int count)
        {
            base.Write(buffer, offset, count);
            Written.TrySetResult();
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            base.Write(buffer);
            Written.TrySetResult();
        }
    }
}
