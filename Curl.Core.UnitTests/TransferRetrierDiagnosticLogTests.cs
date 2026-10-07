using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core;

/// <summary>
/// Pins what <see cref="TransferRetrier" /> writes to Curl's own diagnostic log, component
/// <c>retry</c> (ADR-0222, BL-921): each retry as <c>warning</c> with its attempt and delay, a
/// <c>--retry</c> budget run out as <c>error</c> with the last exit code, a <c>--retry-max-time</c>
/// stop as <c>warning</c>, and each attempt's counters as <c>verbose</c>.
/// </summary>
[TestClass]
public sealed class TransferRetrierDiagnosticLogTests
{
    private const string Url = "http://127.0.0.1:18921/a";

    private static readonly DateTimeOffset Start = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_RetriedTransfer_LogsAWarningWithTheAttemptAndTheDelayInMilliseconds()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        await RetryAsync(log, new RetryPolicy { Retries = 2 }, Http(503), Http(200));

        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("warning line", "attempt 1 failed (HttpError); retrying in 1000 ms, 2 retries left", string.Join(" | ", log.At(DiagnosticLogLevel.Warning)));
        Assert.AreEqual(
            (DiagnosticLogLevel.Warning, DiagnosticLogComponents.Retry, "attempt 1 failed (HttpError); retrying in 1000 ms, 2 retries left"),
            log.Lines.Single(line => line.Level == DiagnosticLogLevel.Warning));
        diagnostics.Assert("verbose line count", 2, log.At(DiagnosticLogLevel.Verbose).Length);
        CollectionAssert.AreEqual(
            new[]
            {
                "attempt 1 ended with exit 0 (Ok); 2 retries left, 0 ms since the first attempt began",
                "attempt 2 ended with exit 0 (Ok); 1 retries left, 1000 ms since the first attempt began",
            },
            log.At(DiagnosticLogLevel.Verbose));
        diagnostics.Assert("error line count", 0, log.At(DiagnosticLogLevel.Error).Length);
        Assert.IsEmpty(log.At(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task RunAsync_RetriesRunOut_LogsAnErrorWithTheLastExitCode()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Error);

        await RetryAsync(log, new RetryPolicy { Retries = 1 }, Timeout(), Timeout());

        TestDiagnostics.For(TestContext).Assert("lines", "Error retry: --retry 1 exhausted after 2 attempts; the last ended with exit 28 (OperationTimedOut)", Describe(log));
        CollectionAssert.AreEqual(
            new[] { (DiagnosticLogLevel.Error, DiagnosticLogComponents.Retry, "--retry 1 exhausted after 2 attempts; the last ended with exit 28 (OperationTimedOut)") },
            log.Lines);
    }

    [TestMethod]
    public async Task RunAsync_WithoutRetry_LogsNoExhaustedBudget()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);

        await RetryAsync(log, new RetryPolicy(), Timeout());

        TestDiagnostics.For(TestContext).Assert("line count", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task RunAsync_RetryMaxTimePassed_LogsAWarning()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);

        await RetryAsync(log, new RetryPolicy { Retries = 3, MaxTime = TimeSpan.FromSeconds(2) }, Http(503), Http(503), Http(503));

        TestDiagnostics.For(TestContext).Assert("last warning", "--retry-max-time 2000 ms reached after attempt 3; not retrying", log.At(DiagnosticLogLevel.Warning)[^1]);
        Assert.AreEqual("--retry-max-time 2000 ms reached after attempt 3; not retrying", log.At(DiagnosticLogLevel.Warning)[^1]);
    }

    [TestMethod]
    public async Task RunAsync_RetryAfterPastRetryMaxTime_LogsAWarning()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);

        await RetryAsync(log, new RetryPolicy { Retries = 3, MaxTime = TimeSpan.FromSeconds(5) }, Http(503, "10"));

        TestDiagnostics.For(TestContext).Assert("warnings", "--retry-max-time 5000 ms reached after attempt 1; not retrying", string.Join(" | ", log.At(DiagnosticLogLevel.Warning)));
        CollectionAssert.AreEqual(new[] { "--retry-max-time 5000 ms reached after attempt 1; not retrying" }, log.At(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task RunAsync_AtLogLevelError_RecordsNoInfoOrVerboseLine()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Error);

        await RetryAsync(log, new RetryPolicy { Retries = 2 }, Http(503), Http(200));

        TestDiagnostics.For(TestContext).Assert("line count", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }

    private static string Describe(RecordingDiagnosticLog log) =>
        string.Join(" | ", log.Lines.Select(line => $"{line.Level} {line.Component}: {line.Message}"));

    private static string Describe(TransferResult attempt) =>
        $"exit {(int)attempt.ExitCode} ({attempt.ExitCode}), response {attempt.Report?.ResponseCode}";

    private async Task RetryAsync(RecordingDiagnosticLog log, RetryPolicy policy, params TransferResult[] attempts)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("policy", policy);
        diagnostics.Arrange("attempts", string.Join(" | ", attempts.Select(Describe)));
        FakeTimeProvider clock = new(Start);
        int next = 0;
        TransferRetrier retrier = new(_ => ValueTask.FromResult(attempts[next++]));

        await retrier.RunAsync(
            new TransferContext { Url = CurlUrl.Parse(Url), Output = Stream.Null, TimeProvider = clock, DiagnosticLog = log },
            policy,
            (_, _) => { },
            (_, _) => { });

        diagnostics.Act("attempts run", next);
        diagnostics.Act("log lines", Describe(log));
    }

    private static TransferResult Http(int status, string? retryAfter = null) =>
        TransferResult.Success(0) with
        {
            Report = new TransferReport
            {
                ResponseCode = status,
                ResponseHeaders = retryAfter is null ? [] : [new("Retry-After", retryAfter)],
            },
        };

    private static TransferResult Timeout() => TransferResult.Failure(CurlExitCode.OperationTimedOut, "timed out");
}
