using System.Text;

using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that a transfer failing with one of the codes curl's <c>is_fatal_error()</c> names (2, 27, 43, 48)
/// ends the whole run, <c>--next</c> groups and further URLs included, and that under <c>-Z</c> the
/// abandoned transfers are reported as aborted by a critical error in another transfer (BL-5313).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerFatalErrorTests
{
    private const string Fail = "http://h.test/fail";

    private const string Next = "http://h.test/next";

    private const string Other = "http://h.test/other";

    private const string Aborted = "curl: (42) Transfer aborted due to critical error in another transfer";

    private readonly MemoryStream standardOutput = new();

    private readonly MemoryStream standardError = new();

    private string StandardOutputText => Encoding.UTF8.GetString(standardOutput.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal);

    [TestMethod]
    [DataRow(CurlExitCode.FailedInit)]
    [DataRow(CurlExitCode.OutOfMemory)]
    [DataRow(CurlExitCode.BadFunctionArgument)]
    [DataRow(CurlExitCode.UnknownOption)]
    public async Task RunAsync_FatalFailureThenNextGroup_NeverTransfersTheNextGroup(CurlExitCode code)
    {
        RecordingProtocolHandler http = Handler(code);

        int exitCode = await RunAsync(http, ["-sS", Fail, "--next", Next]);

        Assert.AreEqual((int)code, exitCode);
        Assert.HasCount(1, http.Contexts);
    }

    [TestMethod]
    [DataRow(CurlExitCode.FailedInit)]
    [DataRow(CurlExitCode.OutOfMemory)]
    [DataRow(CurlExitCode.BadFunctionArgument)]
    [DataRow(CurlExitCode.UnknownOption)]
    public async Task RunAsync_FatalFailureThenNextUrlOfTheGroup_NeverTransfersTheNextUrl(CurlExitCode code)
    {
        RecordingProtocolHandler http = Handler(code);

        int exitCode = await RunAsync(http, ["-sS", "-w", "%{response_code}\n", Fail, Next]);

        Assert.AreEqual((int)code, exitCode);
        Assert.HasCount(1, http.Contexts);
        Assert.AreEqual("000\n", StandardOutputText);
        Assert.AreEqual(1, StandardErrorText.Split("curl: (", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [TestMethod]
    [DataRow(CurlExitCode.CouldntConnect)]
    [DataRow(CurlExitCode.OperationTimedOut)]
    public async Task RunAsync_NonFatalFailure_StillRunsTheNextGroupAndUrl(CurlExitCode code)
    {
        RecordingProtocolHandler http = Handler(code);

        int exitCode = await RunAsync(http, ["-sS", Fail, "--next", Next, Other]);

        Assert.AreEqual(0, exitCode);
        Assert.HasCount(3, http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_FatalFailureUnderParallel_ExitsWithItAndReportsTheAbandonedTransferAborted()
    {
        HeldTransferHandler http = new();

        Task<int> run = RunParallelAsync(http, ["-Z", "-sS", "http://h.test/a", "--next", "http://o.test/b"]);
        await Task.WhenAll(http.WhenStartedAsync("/a"), http.WhenStartedAsync("/b")).WaitAsync(TimeSpan.FromSeconds(10));
        http.Fail("/a", CurlExitCode.BadFunctionArgument, "Session ID cannot be set as a custom header.");

        int exitCode = await run.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(43, exitCode);
        Assert.AreEqual($"curl: (43) Session ID cannot be set as a custom header.\n{Aborted}\n", StandardErrorText);
    }

    private static RecordingProtocolHandler Handler(CurlExitCode code) =>
        new("http", async context =>
        {
            if (context.Url.AbsolutePath == "/fail")
            {
                return TransferResult.Failure(code, "failed on purpose");
            }

            await context.Output.WriteAsync("hi"u8.ToArray(), context.CancellationToken);
            return TransferResult.Success(2);
        });

    private Task<int> RunAsync(IProtocolHandler handler, string[] arguments) => RunParallelAsync(handler, arguments);

    private Task<int> RunParallelAsync(IProtocolHandler handler, string[] arguments) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                new InMemoryFileSystem(),
                new InMemoryFileSystem(),
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: true)
            .RunAsync(arguments);
}
