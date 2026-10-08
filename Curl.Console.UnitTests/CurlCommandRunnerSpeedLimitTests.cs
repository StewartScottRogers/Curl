using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>-Y</c>/<c>--speed-limit</c> and <c>-y</c>/<c>--speed-time</c> end to end through the
/// runner, against a handler that stalls until its transfer is cancelled, on a
/// <see cref="SteppingTimeProvider" /> so no second is real. Every expectation was measured on
/// 2026-09-27 with curl 8.21.0 (Windows, Schannel) through <c>Record-CurlExchange.ps1</c>,
/// against a server that holds its response back (BL-400 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerSpeedLimitTests
{
    private const string Url = "http://127.0.0.1:18400/a";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();
    private readonly SteppingTimeProvider clock = new();
    private readonly TaskCompletionSource stalled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.ASCII.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_StalledBelowSpeedLimitForSpeedTime_Exits28WithCurlsMessage()
    {
        Task<int> run = RunAsync(Stalling(), "-sS", "-Y", "100", "-y", "2", Url);
        await stalled.Task;

        clock.Advance(TimeSpan.FromSeconds(2));
        Diagnostics.Assert("run completed after 2 seconds", false, run.IsCompleted);
        Assert.IsFalse(run.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(1));

        int exitCode = await run;
        Diagnostics.Assert("exit code", 28, exitCode);
        Assert.AreEqual(28, exitCode);
        Diagnostics.Diff("stderr", "curl: (28) Operation too slow. Less than 100 bytes/sec transferred the last 2 seconds\n", Lf(StandardErrorText));
        Assert.AreEqual(
            "curl: (28) Operation too slow. Less than 100 bytes/sec transferred the last 2 seconds" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_StalledIntoOutputFile_Exits28WithCurlsMessage()
    {
        Task<int> run = RunAsync(Stalling(), "-sS", "--speed-limit", "100", "--speed-time", "2", "-o", "out.txt", Url);
        await stalled.Task;

        clock.Advance(TimeSpan.FromSeconds(3));

        int exitCode = await run;
        Diagnostics.Assert("exit code", 28, exitCode);
        Assert.AreEqual(28, exitCode);
        Diagnostics.Diff("stderr", "curl: (28) Operation too slow. Less than 100 bytes/sec transferred the last 2 seconds\n", Lf(StandardErrorText));
        Assert.AreEqual(
            "curl: (28) Operation too slow. Less than 100 bytes/sec transferred the last 2 seconds" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_SpeedLimitWithoutSpeedTime_WaitsCurlsThirtySeconds()
    {
        Task<int> run = RunAsync(Stalling(), "-sS", "-Y", "100", Url);
        await stalled.Task;

        clock.Advance(TimeSpan.FromSeconds(30));
        Diagnostics.Assert("run completed after 30 seconds", false, run.IsCompleted);
        Assert.IsFalse(run.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(1));

        int exitCode = await run;
        Diagnostics.Assert("exit code", 28, exitCode);
        Assert.AreEqual(28, exitCode);
        Diagnostics.Diff("stderr", "curl: (28) Operation too slow. Less than 100 bytes/sec transferred the last 30 seconds\n", Lf(StandardErrorText));
        Assert.AreEqual(
            "curl: (28) Operation too slow. Less than 100 bytes/sec transferred the last 30 seconds" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_SpeedTimeWithoutSpeedLimit_WatchesForOneBytePerSecond()
    {
        Task<int> run = RunAsync(Stalling(), "-sS", "-y", "2", Url);
        await stalled.Task;

        clock.Advance(TimeSpan.FromSeconds(3));

        int exitCode = await run;
        Diagnostics.Assert("exit code", 28, exitCode);
        Assert.AreEqual(28, exitCode);
        Diagnostics.Diff("stderr", "curl: (28) Operation too slow. Less than 1 bytes/sec transferred the last 2 seconds\n", Lf(StandardErrorText));
        Assert.AreEqual(
            "curl: (28) Operation too slow. Less than 1 bytes/sec transferred the last 2 seconds" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_SpeedTimeAfterZeroSpeedLimit_WatchesForOneBytePerSecond()
    {
        Task<int> run = RunAsync(Stalling(), "-sS", "-Y", "0", "-y", "2", Url);
        await stalled.Task;

        clock.Advance(TimeSpan.FromSeconds(2));
        Diagnostics.Assert("run completed after 2 seconds", false, run.IsCompleted);
        Assert.IsFalse(run.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(1));

        int exitCode = await run;
        Diagnostics.Assert("exit code", 28, exitCode);
        Assert.AreEqual(28, exitCode);
        Diagnostics.Diff("stderr", "curl: (28) Operation too slow. Less than 1 bytes/sec transferred the last 2 seconds\n", Lf(StandardErrorText));
        Assert.AreEqual(
            "curl: (28) Operation too slow. Less than 1 bytes/sec transferred the last 2 seconds" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    [DataRow("-y", "2", "-Y", "0")]
    [DataRow("-Y", "100", "-y", "0")]
    public async Task RunAsync_ZeroLastSpeedOption_WatchesNothing(string firstOption, string firstValue, string lastOption, string lastValue)
    {
        RecordingProtocolHandler writing = RecordingProtocolHandler.WritingPath("http");

        int exitCode = await RunAsync(writing, "-sS", firstOption, firstValue, lastOption, lastValue, Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("handler token is CancellationToken.None", true, writing.Contexts[0].CancellationToken == CancellationToken.None);
        Assert.AreEqual(CancellationToken.None, writing.Contexts[0].CancellationToken);
    }

    [TestMethod]
    public async Task RunAsync_FastTransferUnderSpeedLimit_Succeeds()
    {
        RecordingProtocolHandler writing = RecordingProtocolHandler.WritingPath("http");

        int exitCode = await RunAsync(writing, "-sS", "-Y", "100", "-y", "2", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", "/a", StandardOutputText);
        Assert.AreEqual("/a", Encoding.ASCII.GetString(standardOutput.ToArray()));
        Diagnostics.Assert("handler token is CancellationToken.None", false, writing.Contexts[0].CancellationToken == CancellationToken.None);
        Assert.AreNotEqual(CancellationToken.None, writing.Contexts[0].CancellationToken);
    }

    [TestMethod]
    public async Task RunAsync_WithoutSpeedOptions_GivesTheHandlerNoCancellation()
    {
        RecordingProtocolHandler writing = RecordingProtocolHandler.WritingPath("http");

        int exitCode = await RunAsync(writing, "-sS", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("handler token is CancellationToken.None", true, writing.Contexts[0].CancellationToken == CancellationToken.None);
        Assert.AreEqual(CancellationToken.None, writing.Contexts[0].CancellationToken);
    }

    [TestMethod]
    public async Task RunAsync_CancelledButNotTooSlow_LetsTheCancellationOut()
    {
        RecordingProtocolHandler cancelled = new("http", _ => throw new OperationCanceledException());

        OperationCanceledException thrown = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => RunAsync(cancelled, "-sS", "-Y", "100", Url));

        Diagnostics.Act("exception", thrown.GetType().Name);
        Diagnostics.Assert("exception type", nameof(OperationCanceledException), thrown.GetType().Name);
    }

    [TestMethod]
    public async Task RunAsync_CancelledWithoutSpeedOptions_LetsTheCancellationOut()
    {
        RecordingProtocolHandler cancelled = new("http", _ => throw new OperationCanceledException());

        OperationCanceledException thrown = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => RunAsync(cancelled, "-sS", Url));

        Diagnostics.Act("exception", thrown.GetType().Name);
        Diagnostics.Assert("exception type", nameof(OperationCanceledException), thrown.GetType().Name);
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>A handler that reports it has stalled, then waits until its transfer is cancelled.</summary>
    private RecordingProtocolHandler Stalling() =>
        new("http", async context =>
        {
            stalled.TrySetResult();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return TransferResult.Success(0);
        });

    private async Task<int> RunAsync(IProtocolHandler handler, params string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("handler scheme", "http");
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                    outputFiles,
                    outputFiles,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: false,
                    timeProvider: clock)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stdout", Lf(StandardOutputText));
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        return exitCode;
    }
}
