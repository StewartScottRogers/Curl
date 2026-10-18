using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Errors;
using Curl.Api.Options;

namespace Curl.Api.Cancellation;

[TestClass]
public sealed class TransferCancellationTests
{
    [TestMethod]
    public void Progress_TokenNotCancelled_CarriesOn()
    {
        using CancellationTokenSource source = new();
        using TransferCancellation cancellation = new(new CancellableEasyTransfer(), source.Token);

        int answer = cancellation.Progress(10, 5, 0, 0);

        Assert.AreEqual(0, answer);
    }

    [TestMethod]
    public async Task Progress_TokenCancelled_AbortsAndUnpausesAfterOnCancel()
    {
        CancellableEasyTransfer easy = new();
        using CancellationTokenSource source = new();
        int resumesSeenByOnCancel = -1;
        using TransferCancellation cancellation = new(easy, source.Token, () => resumesSeenByOnCancel = easy.ResumeCalls);

        await source.CancelAsync();

        Assert.AreEqual(TransferCancellation.Abort, cancellation.Progress(0, 0, 0, 0));
        Assert.AreEqual(0, resumesSeenByOnCancel);
        Assert.AreEqual(1, easy.ResumeCalls);
    }

    [TestMethod]
    public async Task Dispose_ThenTokenCancelled_LeavesTheHandleAlone()
    {
        CancellableEasyTransfer easy = new();
        using CancellationTokenSource source = new();
        TransferCancellation cancellation = new(easy, source.Token);

        cancellation.Dispose();
        await source.CancelAsync();

        Assert.AreEqual(0, cancellation.Progress(0, 0, 0, 0));
        Assert.AreEqual(0, easy.ResumeCalls);
    }

    [TestMethod]
    public void ApplyTo_SetsTheTransferInfoCallbackAndTurnsProgressOn_KeepingOtherOptions()
    {
        using TransferCancellation cancellation = new(new CancellableEasyTransfer(), CancellationToken.None);
        WriteCallback write = data => data.Length;
        EasyOptions options = new() { Callbacks = new EasyCallbackOptions { Write = write } };

        EasyOptions applied = cancellation.ApplyTo(options);

        Assert.IsFalse(applied.Behaviour.NoProgress);
        Assert.IsNotNull(applied.Callbacks.TransferInfo);
        Assert.AreSame(write, applied.Callbacks.Write);
    }

    [TestMethod]
    public async Task ThrowIfFailed_FailedAfterCancellation_ThrowsOperationCanceled()
    {
        using CancellationTokenSource source = new();
        await source.CancelAsync();

        OperationCanceledException exception = Assert.ThrowsExactly<OperationCanceledException>(
            () => TransferCancellation.ThrowIfFailed(CurlCode.AbortedByCallback, "", source.Token));

        Assert.AreEqual(source.Token, exception.CancellationToken);
    }

    [TestMethod]
    public void ThrowIfFailed_TimedOutWithoutCancellation_ThrowsCurlException()
    {
        CurlException exception = Assert.ThrowsExactly<CurlException>(
            () => TransferCancellation.ThrowIfFailed(CurlCode.OperationTimedOut, "Operation timed out", CancellationToken.None));

        Assert.AreEqual(CurlCode.OperationTimedOut, exception.Code);
    }

    [TestMethod]
    public async Task ThrowIfFailed_SucceededAfterCancellation_Returns()
    {
        using CancellationTokenSource source = new();
        await source.CancelAsync();

        TransferCancellation.ThrowIfFailed(CurlCode.Ok, "", source.Token);

        Assert.IsTrue(source.IsCancellationRequested);
    }
}
