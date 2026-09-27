using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Pins which results <see cref="TransferRetrier" /> retries, the waits between attempts and
/// the warning before each, measured against curl 8.21.0 (mingw, Schannel) on 2026-09-26
/// with a loopback server that timed each request; the commands and timings are in BL-208's
/// notes. Every wait runs on <see cref="FakeTimeProvider" />.
/// </summary>
[TestClass]
public sealed class TransferRetrierTests
{
    private const string Url = "http://127.0.0.1:18208/s/503";

    private static readonly DateTimeOffset Start = new(2026, 9, 27, 5, 26, 14, TimeSpan.Zero);

    [TestMethod]
    public async Task RunAsync_503FourRetries_BacksOffOneTwoFourEightSecondsWithMeasuredWarnings()
    {
        Run run = await Retry(new RetryPolicy { Retries = 4 }, Http(503), Http(503), Http(503), Http(503), Http(503));

        CollectionAssert.AreEqual(Seconds(1, 2, 4, 8), run.Waits);
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: Problem : HTTP error. Retrying in 1 second. 4 retries left.",
                "Warning: Problem : HTTP error. Retrying in 2 seconds. 3 retries left.",
                "Warning: Problem : HTTP error. Retrying in 4 seconds. 2 retries left.",
                "Warning: Problem : HTTP error. Retrying in 8 seconds. 1 retry left.",
            },
            run.Warnings);
        Assert.AreEqual(5, run.Attempts);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
    }

    [TestMethod]
    [DataRow(408)]
    [DataRow(429)]
    [DataRow(500)]
    [DataRow(502)]
    [DataRow(503)]
    [DataRow(504)]
    [DataRow(522)]
    [DataRow(524)]
    public async Task RunAsync_TransientStatus_RetriesUntilOk(int status)
    {
        TransferResult ok = Http(200);

        Run run = await Retry(new RetryPolicy { Retries = 1 }, Http(status), ok);

        Assert.AreSame(ok, run.Result);
        Assert.AreEqual(2, run.Attempts);
        CollectionAssert.AreEqual(Seconds(1), run.Waits);
    }

    [TestMethod]
    [DataRow(200)]
    [DataRow(403)]
    [DataRow(404)]
    [DataRow(501)]
    public async Task RunAsync_OtherStatus_IsFinal(int status)
    {
        Run run = await Retry(new RetryPolicy { Retries = 1 }, Http(status), Http(200));

        Assert.AreEqual(status, run.Result.Report!.ResponseCode);
        Assert.AreEqual(1, run.Attempts);
        Assert.IsEmpty(run.Waits);
    }

    [TestMethod]
    public async Task RunAsync_NoRetries_RunsOnce()
    {
        Run run = await Retry(new RetryPolicy(), Http(503), Http(200));

        Assert.AreEqual(1, run.Attempts);
        Assert.AreEqual(503, run.Result.Report!.ResponseCode);
    }

    [TestMethod]
    public async Task RunAsync_RetriesExhausted_ReturnsLastAttempt()
    {
        TransferResult last = Http(503);

        Run run = await Retry(new RetryPolicy { Retries = 1 }, Http(503), last, Http(200));

        Assert.AreSame(last, run.Result);
        Assert.AreEqual(2, run.Attempts);
    }

    [TestMethod]
    public async Task RunAsync_FailWith503_RetriesAndReturnsExit22()
    {
        Run run = await Retry(new RetryPolicy { Retries = 1 }, Failed(CurlExitCode.HttpReturnedError, 503), Failed(CurlExitCode.HttpReturnedError, 503));

        Assert.AreEqual(CurlExitCode.HttpReturnedError, run.Result.ExitCode);
        CollectionAssert.AreEqual(new[] { "Warning: Problem : HTTP error. Retrying in 1 second. 1 retry left." }, run.Warnings);
    }

    [TestMethod]
    public async Task RunAsync_FailWith404_IsFinal()
    {
        Run run = await Retry(new RetryPolicy { Retries = 1 }, Failed(CurlExitCode.HttpReturnedError, 404), Http(200));

        Assert.AreEqual(1, run.Attempts);
    }

    [TestMethod]
    public async Task RunAsync_TimeoutTwoRetries_BacksOffWithTimeoutWarnings()
    {
        Run run = await Retry(
            new RetryPolicy { Retries = 2 },
            Failed(CurlExitCode.OperationTimedOut),
            Failed(CurlExitCode.OperationTimedOut),
            Failed(CurlExitCode.OperationTimedOut));

        CollectionAssert.AreEqual(Seconds(1, 2), run.Waits);
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: Problem : timeout. Retrying in 1 second. 2 retries left.",
                "Warning: Problem : timeout. Retrying in 2 seconds. 1 retry left.",
            },
            run.Warnings);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, run.Result.ExitCode);
    }

    [TestMethod]
    [DataRow(CurlExitCode.CouldntResolveHost)]
    [DataRow(CurlExitCode.CouldntResolveProxy)]
    [DataRow(CurlExitCode.FtpAcceptTimeout)]
    public async Task RunAsync_OtherTimeoutFamilyFailure_RetriesAsTimeout(CurlExitCode exitCode)
    {
        Run run = await Retry(new RetryPolicy { Retries = 1 }, Failed(exitCode), Http(200));

        Assert.AreEqual(2, run.Attempts);
        CollectionAssert.AreEqual(new[] { "Warning: Problem : timeout. Retrying in 1 second. 1 retry left." }, run.Warnings);
    }

    [TestMethod]
    [DataRow(CurlExitCode.CouldntConnect)]
    [DataRow(CurlExitCode.WriteError)]
    public async Task RunAsync_OtherFailure_IsFinal(CurlExitCode exitCode)
    {
        Run run = await Retry(new RetryPolicy { Retries = 1 }, Failed(exitCode), Http(200));

        Assert.AreEqual(1, run.Attempts);
        Assert.AreEqual(exitCode, run.Result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_TimeoutWithRetryAfter_IgnoresRetryAfter()
    {
        TransferResult timeout = Failed(CurlExitCode.OperationTimedOut) with { Report = Report(503, retryAfter: "7") };

        Run run = await Retry(new RetryPolicy { Retries = 1 }, timeout, Http(200));

        CollectionAssert.AreEqual(Seconds(1), run.Waits);
    }

    [TestMethod]
    public async Task RunAsync_BackoffPastTenMinutes_IsCappedAtTenMinutes()
    {
        TransferResult[] attempts = [.. Enumerable.Range(0, 13).Select(_ => Http(503))];

        Run run = await Retry(new RetryPolicy { Retries = 12 }, attempts);

        CollectionAssert.AreEqual(Seconds(1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 600, 600), run.Waits);
    }

    [TestMethod]
    public async Task RunAsync_RetryDelay_WaitsTheFixedDelayEveryTime()
    {
        Run run = await Retry(new RetryPolicy { Retries = 3, Delay = TimeSpan.FromSeconds(3) }, Http(503), Http(503), Http(503), Http(503));

        CollectionAssert.AreEqual(Seconds(3, 3, 3), run.Waits);
        Assert.AreEqual("Warning: Problem : HTTP error. Retrying in 3 seconds. 1 retry left.", run.Warnings[2]);
    }

    [TestMethod]
    public async Task RunAsync_FractionalRetryDelay_WaitsItAndPrintsMilliseconds()
    {
        Run run = await Retry(new RetryPolicy { Retries = 2, Delay = TimeSpan.FromMilliseconds(1500) }, Http(503), Http(503), Http(503));

        CollectionAssert.AreEqual(new[] { TimeSpan.FromMilliseconds(1500), TimeSpan.FromMilliseconds(1500) }, run.Waits);
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: Problem : HTTP error. Retrying in 1.500 seconds. 2 retries left.",
                "Warning: Problem : HTTP error. Retrying in 1.500 seconds. 1 retry left.",
            },
            run.Warnings);
    }

    [TestMethod]
    public async Task RunAsync_RetryAfterLongerThanBackoff_ReplacesIt()
    {
        Run run = await Retry(new RetryPolicy { Retries = 3 }, Http(503, "3"), Http(503, "3"), Http(503, "3"), Http(503, "3"));

        CollectionAssert.AreEqual(Seconds(3, 3, 3), run.Waits);
    }

    [TestMethod]
    public async Task RunAsync_RetryAfterShorterThanBackoff_ReplacesIt()
    {
        TransferResult[] attempts = [.. Enumerable.Range(0, 5).Select(_ => Http(503, "1"))];

        Run run = await Retry(new RetryPolicy { Retries = 4 }, attempts);

        CollectionAssert.AreEqual(Seconds(1, 1, 1, 1), run.Waits);
    }

    [TestMethod]
    public async Task RunAsync_RetryAfterOnce_DoesNotAdvanceTheBackoff()
    {
        Run run = await Retry(new RetryPolicy { Retries = 3 }, Http(503, "5"), Http(503), Http(503), Http(503));

        CollectionAssert.AreEqual(Seconds(5, 1, 2), run.Waits);
        Assert.AreEqual("Warning: Problem : HTTP error. Retrying in 5 seconds. 3 retries left.", run.Warnings[0]);
    }

    [TestMethod]
    public async Task RunAsync_RetryAfterZero_UsesTheBackoff()
    {
        Run run = await Retry(new RetryPolicy { Retries = 2 }, Http(429, "0"), Http(429, "0"), Http(429, "0"));

        CollectionAssert.AreEqual(Seconds(1, 2), run.Waits);
    }

    [TestMethod]
    public async Task RunAsync_RetryAfterWithRetryDelay_OverridesTheDelay()
    {
        Run longer = await Retry(new RetryPolicy { Retries = 2, Delay = TimeSpan.FromSeconds(1) }, Http(503, "3"), Http(503, "3"), Http(503, "3"));
        Run shorter = await Retry(new RetryPolicy { Retries = 1, Delay = TimeSpan.FromSeconds(3) }, Http(503, "1"), Http(503, "1"));

        CollectionAssert.AreEqual(Seconds(3, 3), longer.Waits);
        CollectionAssert.AreEqual(Seconds(1), shorter.Waits);
    }

    [TestMethod]
    public async Task RunAsync_RetryAfterDate_WaitsUntilTheDateOnTheTransferClock()
    {
        Run run = await Retry(new RetryPolicy { Retries = 1 }, Http(503, "Sun, 27 Sep 2026 05:26:19 GMT"), Http(200));

        CollectionAssert.AreEqual(Seconds(5), run.Waits);
    }

    [TestMethod]
    public async Task RunAsync_RetryAfterHeaderNameInAnyCase_IsRead()
    {
        TransferResult attempt = TransferResult.Success(8) with
        {
            Report = new TransferReport { ResponseCode = 503, ResponseHeaders = [new("retry-after", "4")] },
        };

        Run run = await Retry(new RetryPolicy { Retries = 1 }, attempt, Http(200));

        CollectionAssert.AreEqual(Seconds(4), run.Waits);
    }

    [TestMethod]
    public async Task RunAsync_SuccessWithoutReport_IsFinal()
    {
        Run run = await Retry(new RetryPolicy { Retries = 1 }, TransferResult.Success(0), Http(200));

        Assert.AreEqual(1, run.Attempts);
    }

    [TestMethod]
    [DataRow("ftp://127.0.0.1/f")]
    [DataRow("file:///Z:/f")]
    public async Task RunAsync_TransientStatusFromAnotherScheme_IsFinal(string url)
    {
        Run run = await Retry(new RetryPolicy { Retries = 1 }, new Uri(url), Http(503), Http(200));

        Assert.AreEqual(1, run.Attempts);
    }

    [TestMethod]
    public async Task RunAsync_RedirectToHttps_TheLastHopSchemeDecides()
    {
        TransferResult redirected = Http(503) with { Report = Report(503) with { EffectiveUrl = "https://127.0.0.1/next" } };

        Run run = await Retry(new RetryPolicy { Retries = 1 }, new Uri("ftp://127.0.0.1/f"), redirected, Http(200));

        Assert.AreEqual(2, run.Attempts);
    }

    [TestMethod]
    public async Task RunAsync_RedirectToFtp_TheLastHopSchemeDecides()
    {
        TransferResult redirected = Http(503) with { Report = Report(503) with { EffectiveUrl = "ftp://127.0.0.1/f" } };

        Run run = await Retry(new RetryPolicy { Retries = 1 }, redirected, Http(200));

        Assert.AreEqual(1, run.Attempts);
    }

    [TestMethod]
    public async Task RunAsync_UnparsableEffectiveUrl_FallsBackToTheRequestedScheme()
    {
        TransferResult attempt = Http(503) with { Report = Report(503) with { EffectiveUrl = "not a url" } };

        Run run = await Retry(new RetryPolicy { Retries = 1 }, attempt, Http(200));

        Assert.AreEqual(2, run.Attempts);
    }

    [TestMethod]
    public async Task RunAsync_Retrying_ReceivesTheAttemptBeingRetried()
    {
        TransferResult first = Failed(CurlExitCode.OperationTimedOut);
        List<TransferResult> retried = [];
        FakeTimeProvider clock = new(Start);
        TransferRetrier retrier = new(Script([first, Http(200)], out _));

        await retrier.RunAsync(Context(new Uri(Url), clock), new RetryPolicy { Retries = 1 }, (attempt, _) => retried.Add(attempt));

        Assert.HasCount(1, retried);
        Assert.AreSame(first, retried[0]);
    }

    [TestMethod]
    public async Task RunAsync_CancelledBeforeTheWait_Throws()
    {
        using CancellationTokenSource cancel = new();
        await cancel.CancelAsync();
        TransferRetrier retrier = new(Script([Http(503), Http(200)], out Func<int> attempts));
        TransferContext context = new() { Url = new Uri(Url), Output = Stream.Null, TimeProvider = new FakeTimeProvider(Start), CancellationToken = cancel.Token };

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await retrier.RunAsync(context, new RetryPolicy { Retries = 1 }, (_, _) => { }));

        Assert.AreEqual(1, attempts());
    }

    [TestMethod]
    public async Task RunAsync_NullArguments_Throw()
    {
        TransferRetrier retrier = new(Script([Http(200)], out _));
        TransferContext context = Context(new Uri(Url), new FakeTimeProvider(Start));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await retrier.RunAsync(null!, new RetryPolicy(), (_, _) => { }));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await retrier.RunAsync(context, null!, (_, _) => { }));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await retrier.RunAsync(context, new RetryPolicy(), null!));
    }

    private static Task<Run> Retry(RetryPolicy policy, params TransferResult[] attempts) =>
        Retry(policy, new Uri(Url), attempts);

    private static async Task<Run> Retry(RetryPolicy policy, Uri url, params TransferResult[] attempts)
    {
        FakeTimeProvider clock = new(Start);
        List<string> warnings = [];
        TransferRetrier retrier = new(Script(attempts, out Func<int> count));

        TransferResult result = await retrier.RunAsync(Context(url, clock), policy, (_, warning) => warnings.Add(warning));

        return new Run(result, count(), [.. clock.Waits], warnings);
    }

    private static Func<ITransferContext, ValueTask<TransferResult>> Script(TransferResult[] attempts, out Func<int> count)
    {
        int next = 0;
        count = () => next;
        return _ => ValueTask.FromResult(attempts[next++]);
    }

    private static TransferContext Context(Uri url, TimeProvider clock) =>
        new() { Url = url, Output = Stream.Null, TimeProvider = clock };

    private static TransferResult Http(int status, string? retryAfter = null) =>
        TransferResult.Success(8) with { Report = Report(status, retryAfter) };

    private static TransferResult Failed(CurlExitCode exitCode, int status = 0) =>
        TransferResult.Failure(exitCode, "failed") with { Report = status == 0 ? null : Report(status) };

    private static TransferReport Report(int status, string? retryAfter = null) =>
        new()
        {
            ResponseCode = status,
            ResponseHeaders = retryAfter is null ? [] : [new("Retry-After", retryAfter)],
        };

    private static TimeSpan[] Seconds(params int[] seconds) =>
        [.. seconds.Select(value => TimeSpan.FromSeconds(value))];

    private sealed record Run(TransferResult Result, int Attempts, TimeSpan[] Waits, List<string> Warnings);
}
