using Curl.Api.Callbacks;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

[TestClass]
public sealed class CallbackWriteStreamTests
{
    [TestMethod]
    public async Task WriteAsync_WriteCallbackTakesEveryByte_HandsTheChunkOver()
    {
        List<byte[]> chunks = [];
        WriteCallback callback = data =>
        {
            chunks.Add(data.ToArray());
            return data.Length;
        };
        await using CallbackWriteStream stream = CallbackWriteStream.Of(callback);

        await stream.WriteAsync(new byte[] { 9, 1, 2, 9 }, 1, 2, CancellationToken.None);
        stream.Write([3], 0, 1);
        await stream.FlushAsync(CancellationToken.None);

        Assert.HasCount(2, chunks);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, chunks[0]);
        CollectionAssert.AreEqual(new byte[] { 3 }, chunks[1]);
    }

    [TestMethod]
    public void Write_HeaderCallbackTakesFewerBytes_ThrowsOutputWriteFailedWithWhatItTook()
    {
        HeaderCallback callback = header => header.Length - 1;
        using CallbackWriteStream stream = CallbackWriteStream.Of(callback);

        OutputWriteFailedException failure = Assert.ThrowsExactly<OutputWriteFailedException>(() => stream.Write([1, 2]));

        Assert.AreEqual(1, failure.BytesAccepted);
        Assert.AreEqual("Failure writing output to destination, passed 2 returned 1", failure.Message);
    }

    [TestMethod]
    public void Write_WriteCallbackReturnsANegativeCount_AcceptsNothingAndSaysWhatItReturned()
    {
        using CallbackWriteStream stream = new(_ => -1);

        OutputWriteFailedException failure = Assert.ThrowsExactly<OutputWriteFailedException>(() => stream.Write([1, 2, 3]));

        Assert.AreEqual(0, failure.BytesAccepted);
        Assert.AreEqual("Failure writing output to destination, passed 3 returned -1", failure.Message);
    }

    [TestMethod]
    public void Write_WriteCallbackReturnsWriteFuncError_SaysItReturnedLibcurlsErrorValue()
    {
        using CallbackWriteStream stream = new(_ => WriteCallbackResult.Error);

        OutputWriteFailedException failure = Assert.ThrowsExactly<OutputWriteFailedException>(() => stream.Write([1, 2]));

        Assert.AreEqual("Failure writing output to destination, passed 2 returned 4294967295", failure.Message);
    }

    [TestMethod]
    public async Task WriteAsync_WriteCallbackPauses_WaitsForUnpauseThenHandsTheSameChunkAgain()
    {
        EasyPauseGate gate = new();
        List<byte[]> calls = [];
        WriteCallback callback = data =>
        {
            calls.Add(data.ToArray());
            return calls.Count == 1 ? WriteCallbackResult.Pause : data.Length;
        };
        await using CallbackWriteStream stream = CallbackWriteStream.Of(callback, gate);

        Task writing = stream.WriteAsync(new byte[] { 1, 2 }).AsTask();
        bool pausedWhileWaiting = gate.Directions == Api.EngineContracts.CurlPause.Receive && !writing.IsCompleted;
        gate.Set(Api.EngineContracts.CurlPause.Continue);
        await writing;

        Assert.IsTrue(pausedWhileWaiting);
        Assert.HasCount(2, calls);
        CollectionAssert.AreEqual(calls[0], calls[1]);
    }

    [TestMethod]
    public async Task WriteAsync_UnpauseArrivesBeforeThePauseIsRecorded_DeliversTheChunkOnceMore()
    {
        EasyPauseGate gate = new();
        int calls = 0;
        WriteCallback callback = data =>
        {
            calls++;
            if (calls == 1)
            {
                gate.Set(Api.EngineContracts.CurlPause.Continue);
                return WriteCallbackResult.Pause;
            }

            return data.Length;
        };
        await using CallbackWriteStream stream = CallbackWriteStream.Of(callback, gate);

        await stream.WriteAsync(new byte[] { 1, 2 }).AsTask().WaitAsync(TimeSpan.FromSeconds(30));

        Assert.AreEqual(2, calls);
        Assert.AreEqual(Api.EngineContracts.CurlPause.Continue, gate.Directions);
    }

    [TestMethod]
    public async Task WriteAsync_HeaderCallbackPausesWithNoGate_FailsWithLibcurlsPauseNotSupportedText()
    {
        HeaderCallback callback = _ => WriteCallbackResult.Pause;
        await using CallbackWriteStream stream = CallbackWriteStream.Of(callback);

        OutputWriteFailedException failure = await Assert.ThrowsExactlyAsync<OutputWriteFailedException>(async () => await stream.WriteAsync(new byte[] { 1 }));

        Assert.AreEqual(CallbackWriteStream.PauseNotSupportedText, failure.Message);
    }

    [TestMethod]
    public void Write_WriteCallbackPausesOnTheSynchronousPath_FailsWithLibcurlsPauseNotSupportedText()
    {
        using CallbackWriteStream stream = new(_ => WriteCallbackResult.Pause, new EasyPauseGate());

        OutputWriteFailedException failure = Assert.ThrowsExactly<OutputWriteFailedException>(() => stream.Write([1]));

        Assert.AreEqual(CallbackWriteStream.PauseNotSupportedText, failure.Message);
    }

    [TestMethod]
    public void Capabilities_WriteOnlyStream_RefuseReadSeekAndLength()
    {
        using CallbackWriteStream stream = new(data => data.Length);

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }
}
