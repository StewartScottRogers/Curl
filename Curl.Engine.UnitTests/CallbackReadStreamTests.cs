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
}
