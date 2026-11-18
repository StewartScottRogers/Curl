using System.Text;

using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that under <c>-Z</c> a transfer failing at once with a fatal code (an RTSP transfer's 43) makes
/// the transfer queued beside it end as aborted by a critical error in another transfer, exit 42 for
/// <c>%{exitcode}</c>, while the run's exit stays 43 (BL-5386); a non-fatal failure aborts nothing.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerParallelRtspFatalTests
{
    private const string Aborted = "curl: (42) Transfer aborted due to critical error in another transfer";

    private readonly MemoryStream standardOutput = new();

    private readonly MemoryStream standardError = new();

    private string StandardOutputText => Encoding.UTF8.GetString(standardOutput.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal);

    [TestMethod]
    public async Task RunAsync_FatalFailureBeforeTheOtherTransferStarts_AbortsItAndExitsWithTheFatalCode()
    {
        RecordingProtocolHandler http = Handler(CurlExitCode.BadFunctionArgument);

        int exitCode = await RunAsync(http, ["-Z", "-sS", "http://h.test/fail", "--next", "http://h.test/other"]);

        Assert.AreEqual(43, exitCode);
        Assert.AreEqual($"curl: (43) failed on purpose\n{Aborted}\n", StandardErrorText);
        Assert.HasCount(1, http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_FatalFailureBeforeTheOtherTransferStarts_PrintsExitCode42ForTheAbortedTransfer()
    {
        RecordingProtocolHandler http = Handler(CurlExitCode.BadFunctionArgument);

        int exitCode = await RunAsync(http, ["-Z", "-sS", "http://h.test/fail", "--next", "-w", "[%{exitcode}]\n", "http://h.test/other"]);

        Assert.AreEqual(43, exitCode);
        Assert.AreEqual("[42]\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_NonFatalFailureUnderParallel_DoesNotAbortTheOtherTransfer()
    {
        RecordingProtocolHandler http = Handler(CurlExitCode.CouldntConnect);

        await RunAsync(http, ["-Z", "-sS", "http://h.test/fail", "--next", "-w", "[%{exitcode}]\n", "http://h.test/other"]);

        Assert.AreEqual("hi[0]\n", StandardOutputText);
        Assert.DoesNotContain("(42)", StandardErrorText, StringComparison.Ordinal);
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

    private Task<int> RunAsync(IProtocolHandler handler, string[] arguments) =>
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
