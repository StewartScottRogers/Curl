using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Pins which results <see cref="TransferRetrier" /> retries, the waits between attempts and
/// the warning before each, measured against curl 8.21.0 (mingw, Schannel) on 2026-09-26
/// and 2026-09-27 with loopback HTTP and FTP servers that timed each request; the commands
/// and timings are in BL-208's and BL-317's notes. Every wait runs on
/// <see cref="FakeTimeProvider" />.
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
    [DataRow("file:///f")]
    public async Task RunAsync_TransientStatusFromAnotherScheme_IsFinal(string url)
    {
        Run run = await Retry(new RetryPolicy { Retries = 1 }, CurlUrl.Parse(url), Http(503), Http(200));

        Assert.AreEqual(1, run.Attempts);
    }

    [TestMethod]
    public async Task RunAsync_RedirectToHttps_TheLastHopSchemeDecides()
    {
        TransferResult redirected = Http(503) with { Report = Report(503) with { EffectiveUrl = "https://127.0.0.1/next" } };

        Run run = await Retry(new RetryPolicy { Retries = 1 }, CurlUrl.Parse("ftp://127.0.0.1/f"), redirected, Http(200));

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
    public async Task RunAsync_MaxTimeFiveSeconds_StopsAfterTheAttemptThatEndsPastIt()
    {
        TransferResult[] attempts = [.. Enumerable.Range(0, 11).Select(_ => Http(503))];

        Run run = await Retry(new RetryPolicy { Retries = 10, MaxTime = TimeSpan.FromSeconds(5) }, attempts);

        CollectionAssert.AreEqual(Seconds(1, 2, 4), run.Waits);
        Assert.AreEqual(4, run.Attempts);
        Assert.AreEqual("Warning: Problem : HTTP error. Retrying in 4 seconds. 8 retries left.", run.Warnings[2]);
        Assert.IsEmpty(run.Abandoned);
    }

    [TestMethod]
    public async Task RunAsync_MaxTimeWithRetryDelay_DoesNotShortenTheDelay()
    {
        TransferResult[] attempts = [.. Enumerable.Range(0, 6).Select(_ => Http(503))];

        Run run = await Retry(new RetryPolicy { Retries = 5, Delay = TimeSpan.FromSeconds(2), MaxTime = TimeSpan.FromSeconds(3) }, attempts);

        CollectionAssert.AreEqual(Seconds(2, 2), run.Waits);
        Assert.AreEqual(3, run.Attempts);
    }

    [TestMethod]
    public async Task RunAsync_AttemptEndingAtMaxTime_IsFinal()
    {
        Run run = await Retry(
            new RetryPolicy { Retries = 3, MaxTime = TimeSpan.FromSeconds(5) },
            CurlUrl.Parse(Url),
            TimeSpan.FromSeconds(5),
            Failed(CurlExitCode.OperationTimedOut),
            Http(200));

        Assert.AreEqual(1, run.Attempts);
        Assert.IsEmpty(run.Warnings);
    }

    [TestMethod]
    public async Task RunAsync_RetryAfterPastMaxTime_WarnsAndStopsWithoutWaiting()
    {
        TransferResult first = Http(503, "10");

        Run run = await Retry(new RetryPolicy { Retries = 3, MaxTime = TimeSpan.FromSeconds(5) }, first, Http(200));

        Assert.AreSame(first, run.Result);
        Assert.AreEqual(1, run.Attempts);
        Assert.IsEmpty(run.Waits);
        Assert.IsEmpty(run.Warnings);
        CollectionAssert.AreEqual(
            new[] { "Warning: The Retry-After: time would make this command line exceed the maximum allowed time for retries." },
            run.Abandoned);
    }

    [TestMethod]
    public async Task RunAsync_SecondRetryAfterPastMaxTime_RetriesOnceThenWarns()
    {
        Run run = await Retry(new RetryPolicy { Retries = 3, MaxTime = TimeSpan.FromSeconds(5) }, Http(503, "3"), Http(503, "3"), Http(200));

        CollectionAssert.AreEqual(Seconds(3), run.Waits);
        CollectionAssert.AreEqual(new[] { "Warning: Problem : HTTP error. Retrying in 3 seconds. 3 retries left." }, run.Warnings);
        CollectionAssert.AreEqual(new[] { TransferRetryWarning.RetryAfterExceedsMaxTime }, run.Abandoned);
        Assert.AreEqual(2, run.Attempts);
    }

    [TestMethod]
    public async Task RunAsync_RetryAfterEndingExactlyAtMaxTime_IsWaited()
    {
        Run run = await Retry(new RetryPolicy { Retries = 1, MaxTime = TimeSpan.FromSeconds(5) }, Http(503, "5"), Http(200));

        CollectionAssert.AreEqual(Seconds(5), run.Waits);
        Assert.IsEmpty(run.Abandoned);
    }

    [TestMethod]
    public async Task RunAsync_RetryAfterWithoutMaxTime_IsNeverAbandoned()
    {
        Run run = await Retry(new RetryPolicy { Retries = 1 }, Http(503, "21600"), Http(200));

        CollectionAssert.AreEqual(Seconds(21600), run.Waits);
        Assert.IsEmpty(run.Abandoned);
    }

    [TestMethod]
    public async Task RunAsync_RetryAllErrorsOnFailedConnect_BacksOffWithAllErrorsWarnings()
    {
        Run run = await Retry(
            new RetryPolicy { Retries = 2, RetryAllErrors = true },
            Failed(CurlExitCode.CouldntConnect),
            Failed(CurlExitCode.CouldntConnect),
            Failed(CurlExitCode.CouldntConnect));

        CollectionAssert.AreEqual(Seconds(1, 2), run.Waits);
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: Problem (retrying all errors). Retrying in 1 second. 2 retries left.",
                "Warning: Problem (retrying all errors). Retrying in 2 seconds. 1 retry left.",
            },
            run.Warnings);
        Assert.AreEqual(CurlExitCode.CouldntConnect, run.Result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_RetryConnectionRefusedOnRefusedConnect_BacksOffWithConnectionRefusedWarnings()
    {
        Run run = await Retry(
            new RetryPolicy { Retries = 2, RetryConnectionRefused = true },
            Refused(),
            Refused(),
            Refused());

        CollectionAssert.AreEqual(Seconds(1, 2), run.Waits);
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: Problem : connection refused. Retrying in 1 second. 2 retries left.",
                "Warning: Problem : connection refused. Retrying in 2 seconds. 1 retry left.",
            },
            run.Warnings);
        Assert.AreEqual(3, run.Attempts);
        Assert.AreEqual(CurlExitCode.CouldntConnect, run.Result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_RetryConnectionRefusedOnRefusedThenSuccess_ReturnsTheSuccess()
    {
        Run run = await Retry(new RetryPolicy { Retries = 2, RetryConnectionRefused = true }, Refused(), Http(200));

        Assert.AreEqual(2, run.Attempts);
        Assert.IsTrue(run.Result.IsSuccess);
    }

    [TestMethod]
    public async Task RunAsync_RetryConnectionRefusedOnConnectNotRefused_IsFinal()
    {
        Run run = await Retry(
            new RetryPolicy { Retries = 2, RetryConnectionRefused = true },
            Failed(CurlExitCode.CouldntConnect),
            Http(200));

        Assert.AreEqual(1, run.Attempts);
        Assert.IsEmpty(run.Warnings);
        Assert.AreEqual(CurlExitCode.CouldntConnect, run.Result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_RefusedConnectWithoutRetryConnectionRefused_IsFinal()
    {
        Run run = await Retry(new RetryPolicy { Retries = 2 }, Refused(), Http(200));

        Assert.AreEqual(1, run.Attempts);
        Assert.IsEmpty(run.Warnings);
    }

    [TestMethod]
    public async Task RunAsync_RetryConnectionRefusedOnRefusalFlagWithAnotherExitCode_IsFinal()
    {
        Run run = await Retry(
            new RetryPolicy { Retries = 2, RetryConnectionRefused = true },
            Failed(CurlExitCode.SslConnectError) with { IsConnectionRefused = true },
            Http(200));

        Assert.AreEqual(1, run.Attempts);
    }

    [TestMethod]
    public async Task RunAsync_RetryConnectionRefusedAndAllErrorsOnConnectNotRefused_RetriesAllErrors()
    {
        Run run = await Retry(
            new RetryPolicy { Retries = 1, RetryConnectionRefused = true, RetryAllErrors = true },
            Failed(CurlExitCode.CouldntConnect),
            Http(200));

        CollectionAssert.AreEqual(new[] { "Warning: Problem (retrying all errors). Retrying in 1 second. 1 retry left." }, run.Warnings);
    }

    [TestMethod]
    public async Task RunAsync_RetryConnectionRefusedAndAllErrorsOnRefusedConnect_KeepsTheConnectionRefusedReason()
    {
        Run run = await Retry(
            new RetryPolicy { Retries = 1, RetryConnectionRefused = true, RetryAllErrors = true },
            Refused(),
            Http(200));

        CollectionAssert.AreEqual(new[] { "Warning: Problem : connection refused. Retrying in 1 second. 1 retry left." }, run.Warnings);
    }

    [TestMethod]
    public async Task RunAsync_RetryAllErrorsOnFailWith404_Retries()
    {
        Run run = await Retry(
            new RetryPolicy { Retries = 1, RetryAllErrors = true },
            Failed(CurlExitCode.HttpReturnedError, 404),
            Failed(CurlExitCode.HttpReturnedError, 404));

        CollectionAssert.AreEqual(new[] { "Warning: Problem (retrying all errors). Retrying in 1 second. 1 retry left." }, run.Warnings);
        Assert.AreEqual(2, run.Attempts);
    }

    [TestMethod]
    public async Task RunAsync_RetryAllErrorsOnSuccessfulNotFound_IsFinal()
    {
        Run run = await Retry(new RetryPolicy { Retries = 1, RetryAllErrors = true }, Http(404), Http(200));

        Assert.AreEqual(1, run.Attempts);
    }

    [TestMethod]
    public async Task RunAsync_RetryAllErrorsOnTimeout_KeepsTheTimeoutReason()
    {
        Run run = await Retry(new RetryPolicy { Retries = 1, RetryAllErrors = true }, Failed(CurlExitCode.OperationTimedOut), Http(200));

        CollectionAssert.AreEqual(new[] { "Warning: Problem : timeout. Retrying in 1 second. 1 retry left." }, run.Warnings);
    }

    [TestMethod]
    [DataRow("ftp://127.0.0.1:18320/f")]
    [DataRow("ftps://127.0.0.1:18320/f")]
    public async Task RunAsync_FtpFailureWith4xxReply_BacksOffWithFtpErrorWarnings(string url)
    {
        Run run = await Retry(
            new RetryPolicy { Retries = 2 },
            CurlUrl.Parse(url),
            Ftp(CurlExitCode.LoginDenied, 430),
            Ftp(CurlExitCode.LoginDenied, 430),
            Ftp(CurlExitCode.LoginDenied, 430));

        CollectionAssert.AreEqual(Seconds(1, 2), run.Waits);
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: Problem : FTP error. Retrying in 1 second. 2 retries left.",
                "Warning: Problem : FTP error. Retrying in 2 seconds. 1 retry left.",
            },
            run.Warnings);
        Assert.AreEqual(CurlExitCode.LoginDenied, run.Result.ExitCode);
    }

    [TestMethod]
    [DataRow(399)]
    [DataRow(530)]
    [DataRow(550)]
    public async Task RunAsync_FtpFailureWithOtherReply_IsFinal(int reply)
    {
        Run run = await Retry(new RetryPolicy { Retries = 1 }, CurlUrl.Parse("ftp://127.0.0.1/f"), Ftp(CurlExitCode.LoginDenied, reply), Http(200));

        Assert.AreEqual(1, run.Attempts);
    }

    [TestMethod]
    public async Task RunAsync_FtpFailureWithoutReport_IsFinal()
    {
        Run run = await Retry(new RetryPolicy { Retries = 1 }, CurlUrl.Parse("ftp://127.0.0.1/f"), Failed(CurlExitCode.LoginDenied), Http(200));

        Assert.AreEqual(1, run.Attempts);
    }

    [TestMethod]
    public async Task RunAsync_4xxFailureFromHttp_IsNotAnFtpError()
    {
        Run run = await Retry(new RetryPolicy { Retries = 1 }, Ftp(CurlExitCode.RecvError, 430), Http(200));

        Assert.AreEqual(1, run.Attempts);
    }

    [TestMethod]
    public async Task RunAsync_FtpSuccessWith4xxReply_IsFinal()
    {
        TransferResult attempt = TransferResult.Success(0) with { Report = new TransferReport { ResponseCode = 450 } };

        Run run = await Retry(new RetryPolicy { Retries = 1 }, CurlUrl.Parse("ftp://127.0.0.1/f"), attempt, Http(200));

        Assert.AreEqual(1, run.Attempts);
    }

    [TestMethod]
    public async Task RunAsync_RetryAllErrorsOnFtp4xx_KeepsTheFtpReason()
    {
        Run run = await Retry(new RetryPolicy { Retries = 1, RetryAllErrors = true }, CurlUrl.Parse("ftp://127.0.0.1/f"), Ftp(CurlExitCode.LoginDenied, 430), Http(200));

        CollectionAssert.AreEqual(new[] { "Warning: Problem : FTP error. Retrying in 1 second. 1 retry left." }, run.Warnings);
    }

    [TestMethod]
    public async Task RunAsync_RetryAllErrorsOnRemoteFileNotFound_Retries()
    {
        Run run = await Retry(new RetryPolicy { Retries = 1, RetryAllErrors = true }, CurlUrl.Parse("ftp://127.0.0.1/f"), Ftp(CurlExitCode.RemoteFileNotFound, 550), Http(200));

        CollectionAssert.AreEqual(new[] { "Warning: Problem (retrying all errors). Retrying in 1 second. 1 retry left." }, run.Warnings);
    }

    [TestMethod]
    public async Task RunAsync_Retrying_ReceivesTheAttemptBeingRetried()
    {
        TransferResult first = Failed(CurlExitCode.OperationTimedOut);
        List<TransferResult> retried = [];
        FakeTimeProvider clock = new(Start);
        TransferRetrier retrier = new(Script([first, Http(200)], out _));

        await retrier.RunAsync(Context(CurlUrl.Parse(Url), clock), new RetryPolicy { Retries = 1 }, (attempt, _) => retried.Add(attempt), (_, _) => { });

        Assert.HasCount(1, retried);
        Assert.AreSame(first, retried[0]);
    }

    [TestMethod]
    public async Task RunAsync_CancelledBeforeTheWait_Throws()
    {
        using CancellationTokenSource cancel = new();
        await cancel.CancelAsync();
        TransferRetrier retrier = new(Script([Http(503), Http(200)], out Func<int> attempts));
        TransferContext context = new() { Url = CurlUrl.Parse(Url), Output = Stream.Null, TimeProvider = new FakeTimeProvider(Start), CancellationToken = cancel.Token };

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await retrier.RunAsync(context, new RetryPolicy { Retries = 1 }, (_, _) => { }, (_, _) => { }));

        Assert.AreEqual(1, attempts());
    }

    [TestMethod]
    public async Task RunAsync_NullArguments_Throw()
    {
        TransferRetrier retrier = new(Script([Http(200)], out _));
        TransferContext context = Context(CurlUrl.Parse(Url), new FakeTimeProvider(Start));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await retrier.RunAsync(null!, new RetryPolicy(), (_, _) => { }, (_, _) => { }));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await retrier.RunAsync(context, null!, (_, _) => { }, (_, _) => { }));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await retrier.RunAsync(context, new RetryPolicy(), null!, (_, _) => { }));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await retrier.RunAsync(context, new RetryPolicy(), (_, _) => { }, null!));
    }

    private static Task<Run> Retry(RetryPolicy policy, params TransferResult[] attempts) =>
        Retry(policy, CurlUrl.Parse(Url), attempts);

    private static Task<Run> Retry(RetryPolicy policy, CurlUrl url, params TransferResult[] attempts) =>
        Retry(policy, url, TimeSpan.Zero, attempts);

    private static async Task<Run> Retry(RetryPolicy policy, CurlUrl url, TimeSpan attemptDuration, params TransferResult[] attempts)
    {
        FakeTimeProvider clock = new(Start);
        List<string> warnings = [];
        List<string> abandoned = [];
        Func<ITransferContext, ValueTask<TransferResult>> script = Script(attempts, out Func<int> count);
        TransferRetrier retrier = new(context =>
        {
            clock.Advance(attemptDuration);
            return script(context);
        });

        TransferResult result = await retrier.RunAsync(
            Context(url, clock),
            policy,
            (_, warning) => warnings.Add(warning),
            (_, warning) => abandoned.Add(warning));

        return new Run(result, count(), [.. clock.Waits], warnings, abandoned);
    }

    private static Func<ITransferContext, ValueTask<TransferResult>> Script(TransferResult[] attempts, out Func<int> count)
    {
        int next = 0;
        count = () => next;
        return _ => ValueTask.FromResult(attempts[next++]);
    }

    private static TransferContext Context(CurlUrl url, TimeProvider clock) =>
        new() { Url = url, Output = Stream.Null, TimeProvider = clock };

    private static TransferResult Http(int status, string? retryAfter = null) =>
        TransferResult.Success(8) with { Report = Report(status, retryAfter) };

    private static TransferResult Failed(CurlExitCode exitCode, int status = 0) =>
        TransferResult.Failure(exitCode, "failed") with { Report = status == 0 ? null : Report(status) };

    private static TransferResult Refused() =>
        Failed(CurlExitCode.CouldntConnect) with { IsConnectionRefused = true };

    private static TransferReport Report(int status, string? retryAfter = null) =>
        new()
        {
            ResponseCode = status,
            ResponseHeaders = retryAfter is null ? [] : [new("Retry-After", retryAfter)],
        };

    private static TimeSpan[] Seconds(params int[] seconds) =>
        [.. seconds.Select(value => TimeSpan.FromSeconds(value))];

    private static TransferResult Ftp(CurlExitCode exitCode, int reply) =>
        TransferResult.Failure(exitCode, "failed") with { Report = new TransferReport { ResponseCode = reply } };

    private sealed record Run(TransferResult Result, int Attempts, TimeSpan[] Waits, List<string> Warnings, List<string> Abandoned);
}
