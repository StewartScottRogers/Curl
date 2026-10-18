using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Errors;

namespace Curl.Api.ResponseBodies;

[TestClass]
public sealed class ResponseBodyBufferTests
{
    private int _resumes;

    private ResponseBodyBuffer NewBuffer(int capacity) => new(capacity, () => _resumes++);

    private static async Task<byte[]> ReadAllAsync(ResponseBodyBuffer buffer, int chunkSize)
    {
        List<byte> all = [];
        byte[] destination = new byte[chunkSize];
        int read;
        while ((read = await buffer.ReadAsync(destination, CancellationToken.None)) > 0)
        {
            all.AddRange(destination[..read]);
        }

        return [.. all];
    }

    [TestMethod]
    public async Task ReadAsync_SmallReads_ReturnTheBytesInOrderAcrossDeliveries()
    {
        ResponseBodyBuffer buffer = NewBuffer(100);
        buffer.Write([1, 2, 3]);
        buffer.Write([4, 5]);
        buffer.Complete(CurlCode.Ok, "");

        byte[] all = await ReadAllAsync(buffer, chunkSize: 2);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5 }, all);
    }

    [TestMethod]
    public async Task ReadAsync_LargeRead_TakesSeveralDeliveriesAtOnce()
    {
        ResponseBodyBuffer buffer = NewBuffer(100);
        buffer.Write([1, 2]);
        buffer.Write([3]);
        byte[] destination = new byte[10];

        int read = await buffer.ReadAsync(destination, CancellationToken.None);

        Assert.AreEqual(3, read);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, destination[..3]);
    }

    [TestMethod]
    public async Task ReadAsync_NothingBuffered_WaitsForTheNextDelivery()
    {
        ResponseBodyBuffer buffer = NewBuffer(100);
        byte[] destination = new byte[4];

        ValueTask<int> pending = buffer.ReadAsync(destination, CancellationToken.None);
        Assert.IsFalse(pending.IsCompleted);
        buffer.Write([7]);
        int read = await pending;

        Assert.AreEqual(1, read);
        Assert.AreEqual(7, destination[0]);
    }

    [TestMethod]
    public void Write_EmptyDelivery_TakesNothingAndDoesNotStartTheBody()
    {
        ResponseBodyBuffer buffer = NewBuffer(4);

        long taken = buffer.Write([]);

        Assert.AreEqual(0, taken);
        Assert.IsFalse(buffer.Started.IsCompleted);
    }

    [TestMethod]
    public void Write_FirstDelivery_StartsTheBody()
    {
        ResponseBodyBuffer buffer = NewBuffer(4);

        long taken = buffer.Write([1, 2]);

        Assert.AreEqual(2, taken);
        Assert.IsTrue(buffer.Started.IsCompleted);
        Assert.IsFalse(buffer.Ended);
    }

    [TestMethod]
    public void Write_DeliveryLargerThanCapacityIntoAnEmptyBuffer_IsTakenWhole()
    {
        ResponseBodyBuffer buffer = NewBuffer(2);

        Assert.AreEqual(5, buffer.Write([1, 2, 3, 4, 5]));
    }

    [TestMethod]
    public async Task Write_PastCapacity_PausesUntilTheReaderDrainsHalf()
    {
        ResponseBodyBuffer buffer = NewBuffer(4);
        buffer.Write([1, 2, 3, 4]);

        long paused = buffer.Write([4, 5]);
        byte[] one = new byte[1];
        await buffer.ReadAsync(one, CancellationToken.None);
        int resumesAfterOneByte = _resumes;
        await buffer.ReadAsync(one, CancellationToken.None);
        int resumesAfterTwoBytes = _resumes;
        await buffer.ReadAsync(one, CancellationToken.None);

        Assert.AreEqual(WriteCallbackResult.Pause, paused);
        Assert.AreEqual(0, resumesAfterOneByte);
        Assert.AreEqual(1, resumesAfterTwoBytes);
        Assert.AreEqual(1, _resumes);
        Assert.AreEqual(2, buffer.Write([4, 5]));
    }

    [TestMethod]
    public void Write_AfterAbort_FailsTheTransfer()
    {
        ResponseBodyBuffer buffer = NewBuffer(4);
        buffer.Abort();

        Assert.AreEqual(WriteCallbackResult.Error, buffer.Write([1]));
    }

    [TestMethod]
    public async Task ReadAsync_AfterAFailedEnd_ReturnsWhatWasBufferedThenThrows()
    {
        ResponseBodyBuffer buffer = NewBuffer(4);
        buffer.Write([9]);
        buffer.Complete(CurlCode.PartialFile, "transfer closed with 3 bytes remaining to read");
        byte[] destination = new byte[4];

        int read = await buffer.ReadAsync(destination, CancellationToken.None);
        CurlException exception = await Assert.ThrowsExactlyAsync<CurlException>(
            async () => await buffer.ReadAsync(destination, CancellationToken.None));

        Assert.AreEqual(1, read);
        Assert.AreEqual(CurlCode.PartialFile, exception.Code);
        Assert.AreEqual("transfer closed with 3 bytes remaining to read", exception.Message);
        Assert.IsTrue(buffer.Ended);
    }

    [TestMethod]
    public void Complete_StartsTheBodyAndEndsIt()
    {
        ResponseBodyBuffer buffer = NewBuffer(4);

        buffer.Complete(CurlCode.Ok, "");

        Assert.IsTrue(buffer.Started.IsCompleted);
        Assert.IsTrue(buffer.Ended);
    }

    [TestMethod]
    public void ThrowIfFailed_BeforeTheEndOrAfterSuccess_Returns()
    {
        ResponseBodyBuffer buffer = NewBuffer(4);
        buffer.ThrowIfFailed();
        buffer.Complete(CurlCode.Ok, "");

        buffer.ThrowIfFailed();

        Assert.IsTrue(buffer.Ended);
    }

    [TestMethod]
    public void ThrowIfFailed_AfterAFailedEnd_Throws()
    {
        ResponseBodyBuffer buffer = NewBuffer(4);
        buffer.Complete(CurlCode.RecvError, "");

        CurlException exception = Assert.ThrowsExactly<CurlException>(buffer.ThrowIfFailed);

        Assert.AreEqual(CurlCode.RecvError, exception.Code);
    }

    [TestMethod]
    public async Task ReadAsync_CancelledWhileWaiting_ThrowsAndLaterReadsStillWork()
    {
        ResponseBodyBuffer buffer = NewBuffer(4);
        using CancellationTokenSource source = new();
        byte[] destination = new byte[4];

        ValueTask<int> pending = buffer.ReadAsync(destination, source.Token);
        await source.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await pending);
        buffer.Write([3]);
        int read = await buffer.ReadAsync(destination, CancellationToken.None);

        Assert.AreEqual(1, read);
    }
}
