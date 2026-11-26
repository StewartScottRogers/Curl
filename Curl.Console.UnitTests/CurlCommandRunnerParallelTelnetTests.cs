using System.Text;

using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that under <c>-Z</c> a <c>telnet</c> session holds back every later transfer until it ends, as
/// curl 8.22.0 runs the whole session inside one call of its parallel loop (STD-183, BL-6017).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerParallelTelnetTests
{
    private readonly MemoryStream standardOutput = new();

    private readonly MemoryStream standardError = new();

    private string StandardOutputText => Encoding.UTF8.GetString(standardOutput.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal);

    [TestMethod]
    public async Task RunAsync_TelnetThenHttpUnderParallel_StartsTheHttpTransferOnlyWhenTheSessionHasEnded()
    {
        TaskCompletionSource sessionStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource sessionEnds = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingProtocolHandler telnet = new("telnet", async _ =>
        {
            sessionStarted.SetResult();
            await sessionEnds.Task;
            return TransferResult.Success(0);
        });
        RecordingProtocolHandler http = new("http", async context =>
        {
            await context.Output.WriteAsync("hello\n"u8.ToArray(), context.CancellationToken);
            return TransferResult.Success(6);
        });

        Task<int> run = new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([telnet, http])),
                new InMemoryFileSystem(),
                new InMemoryFileSystem(),
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: true)
            .RunAsync(["-sS", "-Z", "-w", "[%{exitcode}]\n", "telnet://h.test", "--next", "-w", "[%{exitcode}]\n", "http://h.test/hello"]);
        await sessionStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        int startedDuringTheSession = http.Contexts.Count;
        sessionEnds.SetResult();
        int exitCode = await run.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(0, startedDuringTheSession);
        Assert.AreEqual(0, exitCode);
        Assert.HasCount(1, http.Contexts);
        Assert.AreEqual("[0]\nhello\n[0]\n", StandardOutputText);
    }

    // Each expectation below was measured on curl 8.22.0 (Schannel, Windows) on 2026-11-26 (BL-6035, ADR-1428).
    [TestMethod]
    [DataRow("1", 1, "[43 000 0]\n[43 000 0]\n", "43 43", 0)]
    [DataRow("1", 2, "[43 000 0]\n[43 000 0]\n", "43 43", 0)]
    [DataRow("50", 1, "[43 000 0]\nhello\n[42 000 6]\n", "43 42", 1)]
    [DataRow("2", 2, "[43 000 0]\nhello\n[42 000 6]\n[43 000 0]\n", "43 42 43", 1)]
    [DataRow("50", 2, "[43 000 0]\n[42 000 0]\n[42 000 0]\n", "43 42 42", 0)]
    public async Task RunAsync_TelnetFailsFatallyUnderParallel_ReportsTheTransfersBehindItAsCurlDoes(
        string parallelMax, int httpTransfers, string expectedOutput, string expectedErrorCodes, int expectedHttpStarts)
    {
        RecordingProtocolHandler http = new("http", async context =>
        {
            await context.Output.WriteAsync("hello\n"u8.ToArray(), context.CancellationToken);
            return TransferResult.Success(6);
        });

        int exitCode = await RunBehindFailingTelnetAsync(http, parallelMax, httpTransfers);

        Assert.AreEqual(43, exitCode);
        Assert.HasCount(expectedHttpStarts, http.Contexts);
        Assert.AreEqual(expectedOutput, StandardOutputText);
        CollectionAssert.AreEqual(expectedErrorCodes.Split(' '), StandardErrorCodes);
    }

    [TestMethod]
    public async Task RunAsync_TransferBehindAFatalTelnetFailureGetsNoResponse_IsAbortedAfterTheAbortDelay()
    {
        RecordingProtocolHandler http = new("http", async context =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken);
            return TransferResult.Success(0);
        });

        int exitCode = await RunBehindFailingTelnetAsync(http, "50", 1);

        Assert.AreEqual(43, exitCode);
        Assert.AreEqual("[43 000 0]\n[42 000 0]\n", StandardOutputText);
        CollectionAssert.AreEqual(new[] { "43", "42" }, StandardErrorCodes);
    }

    [TestMethod]
    public async Task RunAsync_TransferBehindAFatalTelnetFailureFailsItself_KeepsItsOwnFailure()
    {
        RecordingProtocolHandler http = new(
            "http",
            _ => ValueTask.FromResult(TransferResult.Failure(CurlExitCode.CouldntConnect, "Could not connect")));

        int exitCode = await RunBehindFailingTelnetAsync(http, "50", 1);

        Assert.AreEqual(43, exitCode);
        Assert.AreEqual("[43 000 0]\n[7 000 0]\n", StandardOutputText);
        CollectionAssert.AreEqual(new[] { "43", "7" }, StandardErrorCodes);
    }

    private string[] StandardErrorCodes =>
        Encoding.UTF8.GetString(standardError.ToArray())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('(', ')')[1])
            .ToArray();

    private async Task<int> RunBehindFailingTelnetAsync(RecordingProtocolHandler http, string parallelMax, int httpTransfers)
    {
        RecordingProtocolHandler telnet = new(
            "telnet",
            _ => ValueTask.FromResult(TransferResult.Failure(CurlExitCode.BadFunctionArgument, "A libcurl function was given a bad argument")));
        const string WriteOut = "[%{exitcode} %{http_code} %{size_download}]\n";
        string[] arguments = ["-sS", "-Z", "--parallel-max", parallelMax, "-w", WriteOut, "telnet://h.test"];
        for (int index = 0; index < httpTransfers; index++)
        {
            arguments = [.. arguments, "--next", "-sS", "-w", WriteOut, "http://h.test/hello"];
        }

        return await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([telnet, http])),
                new InMemoryFileSystem(),
                new InMemoryFileSystem(),
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: true)
            .RunAsync(arguments)
            .WaitAsync(TimeSpan.FromSeconds(30));
    }
}
