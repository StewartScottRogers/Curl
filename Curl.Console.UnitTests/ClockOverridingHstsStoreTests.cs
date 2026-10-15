using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="ClockOverridingHstsStore" />: every header reaches the inner store with the
/// HSTS clock's time, not the transfer's (ADR-0493, BL-2234).
/// </summary>
[TestClass]
public sealed class ClockOverridingHstsStoreTests
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void StoreFromResponse_GivenTheTransfersTime_PassesTheClocksTimeAndReturnsTheInnerAnswer(bool legal)
    {
        DateTimeOffset hstsTime = DateTimeOffset.FromUnixTimeSeconds(1728465947);
        RecordingHstsStore inner = new(legal);
        ClockOverridingHstsStore store = new(inner, new FixedWallClock(hstsTime));
        CurlUrl origin = CurlUrl.Parse("https://this.hsts.example/");

        bool stored = store.StoreFromResponse(origin, "max-age=1000", DateTimeOffset.UnixEpoch);

        Assert.AreEqual(legal, stored);
        Assert.AreEqual(hstsTime, inner.Now);
        Assert.AreSame(origin, inner.Origin);
        Assert.AreEqual("max-age=1000", inner.HeaderValue);
    }

    /// <summary>An <see cref="IHstsStore" /> that records its one call and answers <c>legal</c>.</summary>
    private sealed class RecordingHstsStore(bool legal) : IHstsStore
    {
        public CurlUrl? Origin { get; private set; }

        public string? HeaderValue { get; private set; }

        public DateTimeOffset Now { get; private set; }

        public bool StoreFromResponse(CurlUrl origin, string headerValue, DateTimeOffset now)
        {
            Origin = origin;
            HeaderValue = headerValue;
            Now = now;
            return legal;
        }
    }
}
