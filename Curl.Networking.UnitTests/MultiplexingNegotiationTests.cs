namespace Curl.Networking;

/// <summary>
/// Pins that transfers waiting on a <see cref="MultiplexingNegotiation" /> resume in the order
/// they began to wait, so <c>-Z</c> opens HTTP/2 streams in command-line order (CGR-279).
/// </summary>
[TestClass]
public sealed class MultiplexingNegotiationTests
{
    [TestMethod]
    public async Task Decide_WithThreeWaiters_ResumesThemInTheOrderTheyBeganToWait()
    {
        var negotiation = new MultiplexingNegotiation(new ConnectionPoolKey("http", "origin.example", 80, false, false, null, null));
        var resumed = new List<int>();
        var waiters = new List<Task>();
        for (var number = 1; number <= 3; number++)
        {
            waiters.Add(WaitThenRecordAsync(negotiation, resumed, number));
        }

        negotiation.Decide();

        await Task.WhenAll(waiters).WaitAsync(TimeSpan.FromSeconds(30));

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, resumed);
    }

    [TestMethod]
    public async Task WaitAsync_WhenCancelledBeforeItsTurn_PassesTheTurnToTheNextWaiter()
    {
        var negotiation = new MultiplexingNegotiation(new ConnectionPoolKey("http", "origin.example", 80, false, false, null, null));
        using var source = new CancellationTokenSource();
        var cancelled = negotiation.WaitAsync(source.Token);
        var resumed = new List<int>();
        var next = WaitThenRecordAsync(negotiation, resumed, 2);

        await source.CancelAsync();
        negotiation.Decide();

        await Assert.ThrowsAsync<TaskCanceledException>(() => cancelled);
        await next.WaitAsync(TimeSpan.FromSeconds(30));
        CollectionAssert.AreEqual(new[] { 2 }, resumed);
    }

    [TestMethod]
    public async Task WaitAsync_AfterDecideWithNobodyWaiting_ReturnsAtOnce()
    {
        var negotiation = new MultiplexingNegotiation(new ConnectionPoolKey("http", "origin.example", 80, false, false, null, null));
        negotiation.Decide();
        negotiation.ReleaseNext();

        await negotiation.WaitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
    }

    [TestMethod]
    public void ReleaseNext_BeforeDecide_ReleasesNobody()
    {
        var negotiation = new MultiplexingNegotiation(new ConnectionPoolKey("http", "origin.example", 80, false, false, null, null));
        var waiting = negotiation.WaitAsync(CancellationToken.None);

        negotiation.ReleaseNext();

        Assert.IsFalse(waiting.IsCompleted);
    }

    private static async Task WaitThenRecordAsync(MultiplexingNegotiation negotiation, List<int> resumed, int number)
    {
        await negotiation.WaitAsync(CancellationToken.None);
        resumed.Add(number);
        negotiation.ReleaseNext();
    }
}
