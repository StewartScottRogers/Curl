using Curl.Api.Codes;
using Curl.Api.Errors;

namespace Curl.Api.Concurrency;

[TestClass]
public sealed class CurlClientConcurrencyTests
{
    private static Task<CurlResponse>[] SendAll(CurlClient client, CancellationToken cancellationToken, params string[] urls) =>
        [.. urls.Select(url => client.SendAsync(new CurlRequest(HttpMethod.Get, url), cancellationToken))];

    private static async Task WaitUntilPerformingAsync(ConcurrentCurlEngine engine, int count)
    {
        while (engine.CreatedEasyHandles.Count(easy => easy.Performing.IsCompleted) < count)
        {
            await Task.Delay(1);
        }
    }

    [TestMethod]
    public async Task SendAsync_ManyCallsTogether_RunConcurrentlyOnOneMultiHandle()
    {
        ConcurrentCurlEngine engine = new();
        await using CurlClient client = new(engine);

        Task<CurlResponse>[] calls = SendAll(client, CancellationToken.None, "http://a/1", "http://b/2", "http://c/3", "http://d/4", "http://e/5");
        await WaitUntilPerformingAsync(engine, 5);
        engine.CreatedEasyHandles.ForEach(easy => easy.End(CurlCode.Ok));
        CurlResponse[] responses = await Task.WhenAll(calls);

        Assert.HasCount(5, responses);
        Assert.AreEqual(1, engine.MultiCount);
        Assert.AreEqual(5, engine.Multi.MostRunningAtOnce);
        Assert.HasCount(5, engine.Multi.Removed);
    }

    [TestMethod]
    public async Task SendAsync_ConnectionLimits_AreSetOnTheMultiHandleAndHoldTransfersBack()
    {
        ConcurrentCurlEngine engine = new();
        CurlClientOptions options = new() { MaxConnections = 2, MaxConnectionsPerHost = 1 };
        await using CurlClient client = new(engine, options, ownsEngine: true);

        Task<CurlResponse>[] calls = SendAll(client, CancellationToken.None, "http://a/1", "http://a/2", "http://b/3", "http://c/4");
        await WaitUntilPerformingAsync(engine, 2);
        while (!Task.WhenAll(calls).IsCompleted)
        {
            engine.CreatedEasyHandles.Where(easy => easy.Performing.IsCompleted).ToList().ForEach(easy => easy.End(CurlCode.Ok));
            await Task.Delay(1);
        }

        await Task.WhenAll(calls);
        Assert.AreEqual(2, engine.Multi.Options.MaxTotalConnections);
        Assert.AreEqual(1, engine.Multi.Options.MaxHostConnections);
        Assert.AreEqual(2, engine.Multi.MostRunningAtOnce);
        Assert.AreEqual(1, engine.Multi.MostRunningOnOneHostAtOnce);
    }

    [TestMethod]
    public async Task SendAsync_OneTransferFails_OnlyThatCallThrows()
    {
        ConcurrentCurlEngine engine = new();
        await using CurlClient client = new(engine);
        Task<CurlResponse>[] calls = SendAll(client, CancellationToken.None, "http://a/1", "http://b/2", "http://c/3");
        await WaitUntilPerformingAsync(engine, 3);

        engine.CreatedEasyHandles[1].End(CurlCode.CouldntResolveHost);
        CurlException failure = await Assert.ThrowsExactlyAsync<CurlException>(() => calls[1]);
        Assert.IsFalse(calls[0].IsCompleted);
        Assert.IsFalse(calls[2].IsCompleted);
        engine.CreatedEasyHandles[0].End(CurlCode.Ok);
        engine.CreatedEasyHandles[2].End(CurlCode.Ok);

        Assert.AreEqual(CurlCode.CouldntResolveHost, failure.Code);
        Assert.IsNotNull(await calls[0]);
        Assert.IsNotNull(await calls[2]);
    }

    [TestMethod]
    public async Task SendAsync_OneCallCancelled_TheOthersGoOn()
    {
        ConcurrentCurlEngine engine = new();
        await using CurlClient client = new(engine);
        using CancellationTokenSource cancelOne = new();
        Task<CurlResponse> cancelled = client.SendAsync(new CurlRequest(HttpMethod.Get, "http://a/1"), cancelOne.Token);
        Task<CurlResponse>[] others = SendAll(client, CancellationToken.None, "http://b/2", "http://c/3");
        await WaitUntilPerformingAsync(engine, 3);

        await cancelOne.CancelAsync();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => cancelled);
        bool othersStillRunning = others.All(call => !call.IsCompleted);
        engine.CreatedEasyHandles.ForEach(easy => easy.End(CurlCode.Ok));

        Assert.IsTrue(othersStillRunning);
        Assert.HasCount(2, await Task.WhenAll(others));
        Assert.IsTrue(engine.CreatedEasyHandles[0].PerformTokenCancelled);
    }

    [TestMethod]
    public async Task DisposeAsync_TransfersRunning_CancelsEveryCallAndDisposesTheMultiHandle()
    {
        ConcurrentCurlEngine engine = new();
        CurlClient client = new(engine);
        Task<CurlResponse>[] calls = SendAll(client, CancellationToken.None, "http://a/1", "http://b/2", "http://c/3");
        await WaitUntilPerformingAsync(engine, 3);

        await client.DisposeAsync();

        foreach (Task<CurlResponse> call in calls)
        {
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => call);
        }

        Assert.IsTrue(engine.Multi.Disposed);
        Assert.IsTrue(engine.CreatedEasyHandles.TrueForAll(easy => easy.Disposed));
    }

    [TestMethod]
    public async Task DisposeAsync_NoTransferEverSent_CreatesNoMultiHandle()
    {
        ConcurrentCurlEngine engine = new();
        CurlClient client = new(engine);

        await client.DisposeAsync();

        Assert.AreEqual(0, engine.MultiCount);
    }
}
