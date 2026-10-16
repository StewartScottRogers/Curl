using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;

namespace Curl.Api.Concurrency;

[TestClass]
public sealed class MultiTransferDriverTests
{
    private static (MultiTransferDriver Driver, EasyPerformingMultiTransfer Multi, Func<int> Created) NewDriver()
    {
        EasyPerformingMultiTransfer multi = new();
        int created = 0;
        MultiTransferDriver driver = new(() =>
        {
            created++;
            return multi;
        });
        return (driver, multi, () => created);
    }

    [TestMethod]
    public async Task RunAsync_TransferEnds_ReturnsItsResultAndRemovesIt()
    {
        (MultiTransferDriver driver, EasyPerformingMultiTransfer multi, _) = NewDriver();
        await using MultiTransferDriver disposing = driver;
        GatedEasyTransfer easy = new();

        Task<CurlCode> run = driver.RunAsync(easy, CancellationToken.None);
        await easy.Performing;
        easy.End(CurlCode.PartialFile);

        Assert.AreEqual(CurlCode.PartialFile, await run);
        CollectionAssert.AreEqual(new[] { easy }, multi.Removed);
    }

    [TestMethod]
    public async Task RunAsync_SecondTransferWhileOneRuns_WakesTheLoopAndRunsBoth()
    {
        (MultiTransferDriver driver, EasyPerformingMultiTransfer multi, Func<int> created) = NewDriver();
        await using MultiTransferDriver disposing = driver;
        GatedEasyTransfer first = new();
        GatedEasyTransfer second = new();
        Task<CurlCode> firstRun = driver.RunAsync(first, CancellationToken.None);
        await first.Performing;

        Task<CurlCode> secondRun = driver.RunAsync(second, CancellationToken.None);
        await second.Performing;
        first.End(CurlCode.Ok);
        second.End(CurlCode.Ok);

        Assert.AreEqual(CurlCode.Ok, await firstRun);
        Assert.AreEqual(CurlCode.Ok, await secondRun);
        Assert.IsGreaterThan(0, multi.WakeupCount);
        Assert.AreEqual(2, multi.MostRunningAtOnce);
        Assert.AreEqual(1, created());
    }

    [TestMethod]
    public async Task RunAsync_AfterTheLoopEnded_StartsItAgainOnTheSameMultiHandle()
    {
        (MultiTransferDriver driver, _, Func<int> created) = NewDriver();
        await using MultiTransferDriver disposing = driver;
        GatedEasyTransfer first = new();
        first.End(CurlCode.Ok);
        Assert.AreEqual(CurlCode.Ok, await driver.RunAsync(first, CancellationToken.None));
        GatedEasyTransfer second = new();
        second.End(CurlCode.Ok);

        CurlCode result = await driver.RunAsync(second, CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, result);
        Assert.AreEqual(1, created());
    }

    [TestMethod]
    public async Task RunAsync_Cancelled_RemovesTheTransferWhichEndsAbortedByCallback()
    {
        (MultiTransferDriver driver, EasyPerformingMultiTransfer multi, _) = NewDriver();
        await using MultiTransferDriver disposing = driver;
        GatedEasyTransfer easy = new();
        using CancellationTokenSource cancellation = new();
        Task<CurlCode> run = driver.RunAsync(easy, cancellation.Token);
        await easy.Performing;

        await cancellation.CancelAsync();

        Assert.AreEqual(CurlCode.AbortedByCallback, await run);
        Assert.IsTrue(easy.PerformTokenCancelled);
        CollectionAssert.AreEqual(new[] { easy }, multi.Removed);
    }

    [TestMethod]
    public async Task RunAsync_MultiHandleRefusesTheTransfer_ThrowsInvalidOperationException()
    {
        (MultiTransferDriver driver, EasyPerformingMultiTransfer multi, _) = NewDriver();
        await using MultiTransferDriver disposing = driver;
        multi.AddCode = CurlMultiCode.AddedAlready;

        InvalidOperationException failure = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => driver.RunAsync(new GatedEasyTransfer(), CancellationToken.None));

        Assert.Contains("curl_multi_add_handle", failure.Message);
    }

    [TestMethod]
    public async Task RunAsync_PerformFails_FailsEveryRunningTransfer()
    {
        (MultiTransferDriver driver, EasyPerformingMultiTransfer multi, _) = NewDriver();
        await using MultiTransferDriver disposing = driver;
        GatedEasyTransfer easy = new();
        Task<CurlCode> run = driver.RunAsync(easy, CancellationToken.None);
        await easy.Performing;

        multi.PerformCode = CurlMultiCode.InternalError;
        multi.Wakeup();

        InvalidOperationException failure = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => run);
        Assert.Contains("curl_multi_perform", failure.Message);
        CollectionAssert.AreEqual(new[] { easy }, multi.Removed);
    }

    [TestMethod]
    public async Task RunAsync_PollFails_FailsEveryRunningTransfer()
    {
        (MultiTransferDriver driver, EasyPerformingMultiTransfer multi, _) = NewDriver();
        await using MultiTransferDriver disposing = driver;
        multi.PollCode = CurlMultiCode.BadHandle;
        GatedEasyTransfer easy = new();
        Task<CurlCode> run = driver.RunAsync(easy, CancellationToken.None);
        await easy.Performing;

        multi.Wakeup();

        InvalidOperationException failure = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => run);
        Assert.Contains("curl_multi_poll", failure.Message);
    }

    [TestMethod]
    public async Task RunAsync_TransferPerformThrows_TheExceptionFailsTheRunningTransfers()
    {
        (MultiTransferDriver driver, _, _) = NewDriver();
        await using MultiTransferDriver disposing = driver;
        GatedEasyTransfer easy = new();
        Task<CurlCode> run = driver.RunAsync(easy, CancellationToken.None);
        await easy.Performing;

        easy.Throw(new IOException("disk gone"));

        IOException failure = await Assert.ThrowsExactlyAsync<IOException>(() => run);
        Assert.AreEqual("disk gone", failure.Message);
    }

    [TestMethod]
    public async Task RunAsync_MessageAboutAnUnknownTransfer_IsIgnored()
    {
        (MultiTransferDriver driver, EasyPerformingMultiTransfer multi, _) = NewDriver();
        await using MultiTransferDriver disposing = driver;
        multi.StrayMessages.Add(new CurlMultiMessage(CurlMultiMessageType.Done, new GatedEasyTransfer(), CurlCode.Ok));
        GatedEasyTransfer easy = new();
        Task<CurlCode> run = driver.RunAsync(easy, CancellationToken.None);
        await easy.Performing;

        easy.End(CurlCode.SendError);

        Assert.AreEqual(CurlCode.SendError, await run);
        CollectionAssert.AreEqual(new[] { easy }, multi.Removed);
    }

    [TestMethod]
    public async Task DisposeAsync_TransferRunning_EndsItAbortedByCallbackAndDisposesTheMultiHandle()
    {
        (MultiTransferDriver driver, EasyPerformingMultiTransfer multi, _) = NewDriver();
        GatedEasyTransfer easy = new();
        Task<CurlCode> run = driver.RunAsync(easy, CancellationToken.None);
        await easy.Performing;

        await driver.DisposeAsync();
        await driver.DisposeAsync();

        Assert.AreEqual(CurlCode.AbortedByCallback, await run);
        Assert.IsTrue(multi.Disposed);
    }

    [TestMethod]
    public async Task DisposeAsync_NeverUsed_CreatesNoMultiHandle()
    {
        (MultiTransferDriver driver, _, Func<int> created) = NewDriver();

        await driver.DisposeAsync();

        Assert.AreEqual(0, created());
    }

    [TestMethod]
    public async Task RunAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        (MultiTransferDriver driver, _, _) = NewDriver();
        await driver.DisposeAsync();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => driver.RunAsync(new GatedEasyTransfer(), CancellationToken.None));
    }

    [TestMethod]
    public async Task RunAsync_CancelledAfterDispose_DoesNothing()
    {
        (MultiTransferDriver driver, _, _) = NewDriver();
        GatedEasyTransfer easy = new();
        using CancellationTokenSource cancellation = new();
        Task<CurlCode> run = driver.RunAsync(easy, cancellation.Token);
        await easy.Performing;
        Task disposed = driver.DisposeAsync().AsTask();

        await cancellation.CancelAsync();
        await disposed;

        Assert.AreEqual(CurlCode.AbortedByCallback, await run);
    }
}
