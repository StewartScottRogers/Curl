using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.RateLimit.UnitTests;

[TestClass]
public sealed class RateLimitTransferHookTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly SteppingTimeProvider _clock = new(Start);

    private readonly ITransferHook _hook = new RateLimitTransferHook();

    [TestMethod]
    public async Task ExecuteAsync_NoQuotaLeft_HoldsTheNextTransferToTheOriginForTheEffectiveWindow_Sections4Point1Point1And4Point1Point2And7()
    {
        await _hook.ExecuteAsync(Context("http://Example.com/a"), Origin(429, ("RateLimit", "\"default\";r=0;t=30")));

        var result = await _hook.ExecuteAsync(Context("http://example.com:80/b"), Origin(200));

        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(30) }, _clock.DueTimes.ToArray());
        Assert.AreEqual(200, result.Report!.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_AnotherOrigin_IsNotHeld_Section7()
    {
        await _hook.ExecuteAsync(Context("http://example.com/a"), Origin(200, ("RateLimit", "\"default\";r=0;t=30")));

        await _hook.ExecuteAsync(Context("https://example.com/a"), Origin(200));
        await _hook.ExecuteAsync(Context("http://example.com:8080/a"), Origin(200));
        await _hook.ExecuteAsync(Context("http://other.example/a"), Origin(200));

        Assert.IsEmpty(_clock.DueTimes);
    }

    [TestMethod]
    public async Task ExecuteAsync_QuotaLeft_DoesNotHold_Section4Point1Point1()
    {
        await _hook.ExecuteAsync(Context(), Origin(200, ("RateLimit", "\"default\";r=1;t=30")));

        await _hook.ExecuteAsync(Context(), Origin(200));

        Assert.IsEmpty(_clock.DueTimes);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoQuotaLeftAndNoEffectiveWindow_HoldsForThePolicyWindowOfTheSameName_Section3Point1Point3()
    {
        await _hook.ExecuteAsync(Context(), Origin(429, ("ratelimit-policy", "\"burst\";q=10;w=5, \"day\";q=1000;w=86400"), ("ratelimit", "\"burst\";r=0")));

        await _hook.ExecuteAsync(Context(), Origin(200));

        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(5) }, _clock.DueTimes.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_NoQuotaLeftAndNoWindowAnywhere_DoesNotHold()
    {
        await _hook.ExecuteAsync(Context(), Origin(429, ("RateLimit-Policy", "\"other\";q=10;w=5"), ("RateLimit", "\"burst\";r=0, \"zero\";r=0;t=0")));

        await _hook.ExecuteAsync(Context(), Origin(200));

        Assert.IsEmpty(_clock.DueTimes);
    }

    [TestMethod]
    public async Task ExecuteAsync_SeveralUsedUpQuotasOnSeveralFieldLines_HoldsForTheLongest_AppendixB1Point2()
    {
        await _hook.ExecuteAsync(Context(), Origin(429, ("RateLimit", "\"hour\";r=0;t=600"), ("RateLimit", "\"minute\";r=0;t=20, \"day\";r=3;t=9000")));

        await _hook.ExecuteAsync(Context(), Origin(200));

        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(600) }, _clock.DueTimes.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_AnEffectiveWindowPastWhatADelayTakes_IsCapped()
    {
        await _hook.ExecuteAsync(Context(), Origin(429, ("RateLimit", "\"default\";r=0;t=99999999999")));

        await _hook.ExecuteAsync(Context(), Origin(200));

        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(RateLimitTransferHook.LongestHoldSeconds) }, _clock.DueTimes.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_AParallelTransferEndingLaterWithAShorterHold_KeepsTheLongerHold()
    {
        await _hook.ExecuteAsync(Context(), async inner =>
        {
            await _hook.ExecuteAsync(Context(), Origin(429, ("RateLimit", "\"default\";r=0;t=60")));
            return await Origin(429, ("RateLimit", "\"default\";r=0;t=1"))(inner);
        });

        await _hook.ExecuteAsync(Context(), Origin(200));

        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(60) }, _clock.DueTimes.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_AHoldThatHasPassed_DoesNotWait()
    {
        await _hook.ExecuteAsync(Context(), Origin(429, ("RateLimit", "\"default\";r=0;t=30")));
        _clock.Advance(TimeSpan.FromSeconds(30));

        await _hook.ExecuteAsync(Context(), Origin(200));

        Assert.IsEmpty(_clock.DueTimes);
    }

    [TestMethod]
    public async Task ExecuteAsync_ACancelledTransferWhileHeld_EndsTheWait()
    {
        await _hook.ExecuteAsync(Context(), Origin(429, ("RateLimit", "\"default\";r=0;t=30")));
        TransferContext cancelled = new()
        {
            Url = CurlUrl.Parse("http://example.com/r"),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions(),
            TimeProvider = _clock,
            CancellationToken = new CancellationToken(canceled: true),
        };

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await _hook.ExecuteAsync(cancelled, Origin(200)));
    }

    [TestMethod]
    public async Task ExecuteAsync_AFailedTransferWithNoReport_IsReturnedAsItIsAndHoldsNothing()
    {
        var failure = TransferResult.Failure(CurlExitCode.CouldntConnect, "refused");

        Assert.AreSame(failure, await _hook.ExecuteAsync(Context(), _ => ValueTask.FromResult(failure)));
        await _hook.ExecuteAsync(Context(), Origin(200));

        Assert.IsEmpty(_clock.DueTimes);
    }

    [TestMethod]
    public async Task ExecuteAsync_ATransferThatIsNotHttp_RunsWithTheContextUnchangedAndHoldsNothing()
    {
        TransferContext context = new() { Url = CurlUrl.Parse("ftp://example.com/r"), Output = new MemoryStream(), TimeProvider = _clock };
        ITransferContext? sent = null;

        await _hook.ExecuteAsync(context, inner =>
        {
            sent = inner;
            return Origin(429, ("RateLimit", "\"default\";r=0;t=30"))(inner);
        });
        await _hook.ExecuteAsync(context, Origin(200));

        Assert.AreSame(context, sent);
        Assert.IsEmpty(_clock.DueTimes);
    }

    [TestMethod]
    public void RequestHeadersAndBody_AreLeftToCurl()
    {
        using MemoryStream output = new();

        Assert.IsEmpty(_hook.RequestHeadersFor(Context()));
        Assert.AreSame(output, _hook.ResponseBodyOver(output));
    }

    private TransferContext Context(string url = "http://example.com/r") => new()
    {
        Url = CurlUrl.Parse(url),
        Output = new MemoryStream(),
        Http = new HttpRequestOptions(),
        TimeProvider = _clock,
    };

    /// <summary>Stands in for curl's handler: reports one recorded response's status and fields.</summary>
    private static Func<ITransferContext, ValueTask<TransferResult>> Origin(int code, params (string Name, string Value)[] fields) =>
        _ => ValueTask.FromResult(TransferResult.Success(0) with
        {
            Report = new TransferReport
            {
                ResponseCode = code,
                ResponseHeaders = [.. fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value))],
            },
        });
}
