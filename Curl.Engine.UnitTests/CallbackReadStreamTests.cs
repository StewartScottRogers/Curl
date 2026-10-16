using Curl.Api.Callbacks;

namespace Curl.Engine;

[TestClass]
public sealed class CallbackReadStreamTests
{
    [TestMethod]
    public async Task ReadAsync_ReadCallbackFillsPartOfTheBuffer_ReturnsWhatItFilledThenZeroAtTheEnd()
    {
        Queue<byte[]> chunks = new([[1, 2], [3]]);
        ReadCallback callback = buffer =>
        {
            if (!chunks.TryDequeue(out byte[]? chunk))
            {
                return 0;
            }

            chunk.CopyTo(buffer);
            return chunk.Length;
        };
        await using CallbackReadStream stream = new(callback);
        byte[] buffer = new byte[8];

        int first = await stream.ReadAsync(buffer, 1, 4, CancellationToken.None);
        int second = stream.Read(buffer, 0, 4);
        int end = await stream.ReadAsync(buffer.AsMemory(), CancellationToken.None);
        await stream.FlushAsync(CancellationToken.None);

        Assert.AreEqual(2, first);
        Assert.AreEqual(1, second);
        Assert.AreEqual(0, end);
        Assert.AreEqual(3, buffer[0]);
        Assert.AreEqual(2, buffer[2]);
    }

    [TestMethod]
    public void Read_ReadCallbackReturnsMoreThanTheBuffer_ThrowsIOExceptionWithLibcurlsText()
    {
        using CallbackReadStream stream = new(buffer => buffer.Length + 1);

        IOException failure = Assert.ThrowsExactly<IOException>(() => stream.Read(new byte[2], 0, 2));

        Assert.AreEqual(CallbackReadStream.FunnyValueText, failure.Message);
    }

    [TestMethod]
    public void Read_ReadCallbackReturnsBelowZero_ThrowsIOException()
    {
        using CallbackReadStream stream = new(_ => -1);

        Assert.ThrowsExactly<IOException>(() => stream.Read(new byte[2], 0, 2));
    }

    [TestMethod]
    public void Read_ReadCallbackAborts_MarksTheStreamAbortedAndThrowsWithLibcurlsText()
    {
        using CallbackReadStream stream = new(_ => ReadCallbackResult.Abort);

        IOException failure = Assert.ThrowsExactly<IOException>(() => stream.Read(new byte[2], 0, 2));

        Assert.AreEqual(CallbackReadStream.AbortedText, failure.Message);
        Assert.IsTrue(stream.Aborted);
    }

    [TestMethod]
    public async Task ReadAsync_ReadCallbackPauses_WaitsForUnpauseThenAsksAgain()
    {
        EasyPauseGate gate = new();
        int calls = 0;
        ReadCallback callback = buffer =>
        {
            calls++;
            if (calls == 1)
            {
                return ReadCallbackResult.Pause;
            }

            buffer[0] = 7;
            return 1;
        };
        await using CallbackReadStream stream = new(callback, gate);
        byte[] buffer = new byte[4];

        Task<int> reading = stream.ReadAsync(buffer.AsMemory()).AsTask();
        bool pausedWhileWaiting = gate.Directions == Api.EngineContracts.CurlPause.Send && !reading.IsCompleted;
        gate.Set(Api.EngineContracts.CurlPause.Continue);
        int read = await reading;

        Assert.IsTrue(pausedWhileWaiting);
        Assert.AreEqual(1, read);
        Assert.AreEqual(7, buffer[0]);
        Assert.AreEqual(2, calls);
        Assert.IsFalse(stream.Aborted);
    }

    [TestMethod]
    public async Task ReadAsync_UnpauseArrivesBeforeThePauseIsRecorded_AsksAgainAndReadsOnce()
    {
        EasyPauseGate gate = new();
        int calls = 0;
        ReadCallback callback = buffer =>
        {
            calls++;
            if (calls == 1)
            {
                gate.Set(Api.EngineContracts.CurlPause.Continue);
                return ReadCallbackResult.Pause;
            }

            buffer[0] = 9;
            return 1;
        };
        await using CallbackReadStream stream = new(callback, gate);
        byte[] buffer = new byte[4];

        int read = await stream.ReadAsync(buffer.AsMemory()).AsTask().WaitAsync(TimeSpan.FromSeconds(30));

        Assert.AreEqual(1, read);
        Assert.AreEqual(9, buffer[0]);
        Assert.AreEqual(2, calls);
        Assert.AreEqual(Api.EngineContracts.CurlPause.Continue, gate.Directions);
    }

    [TestMethod]
    public async Task ReadAsync_ReadCallbackPausesWithNoGate_ThrowsWithLibcurlsPauseNotSupportedText()
    {
        await using CallbackReadStream stream = new(_ => ReadCallbackResult.Pause);

        IOException failure = await Assert.ThrowsExactlyAsync<IOException>(async () => _ = await stream.ReadAsync(new byte[2].AsMemory()));

        Assert.AreEqual(CallbackReadStream.PauseNotSupportedText, failure.Message);
    }

    [TestMethod]
    public void Read_ReadCallbackPausesOnTheSynchronousPath_ThrowsWithLibcurlsPauseNotSupportedText()
    {
        using CallbackReadStream stream = new(_ => ReadCallbackResult.Pause, new EasyPauseGate());

        IOException failure = Assert.ThrowsExactly<IOException>(() => stream.Read(new byte[2], 0, 2));

        Assert.AreEqual(CallbackReadStream.PauseNotSupportedText, failure.Message);
    }

    [TestMethod]
    public void Capabilities_ReadOnlyStream_RefuseWriteSeekAndLength()
    {
        using CallbackReadStream stream = new(_ => 0);

        Assert.IsTrue(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsFalse(stream.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }

    [TestMethod]
    public void SuppliesTrailers_NoTrailerCallbackSet_IsFalse()
    {
        using CallbackReadStream stream = new(_ => 0);

        Assert.IsFalse(stream.SuppliesTrailers);
    }

    [TestMethod]
    [DataRow(TrailerResult.Ok, true)]
    [DataRow(TrailerResult.Abort, false)]
    public void TryTakeTrailers_TrailerCallbackSet_GivesTheLinesItAddedAndWhetherToSendThem(TrailerResult result, bool expected)
    {
        using CallbackReadStream stream = new(_ => 0)
        {
            Trailer = trailers =>
            {
                trailers.Add("X-T: v");
                return result;
            },
        };

        bool send = stream.TryTakeTrailers(out IReadOnlyList<string> lines);

        Assert.IsTrue(stream.SuppliesTrailers);
        Assert.AreEqual(expected, send);
        CollectionAssert.AreEqual(new[] { "X-T: v" }, lines.ToArray());
    }
}
