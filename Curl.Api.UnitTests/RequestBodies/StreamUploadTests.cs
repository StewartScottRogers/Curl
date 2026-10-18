using Curl.Api.Callbacks;

namespace Curl.Api.RequestBodies;

[TestClass]
public sealed class StreamUploadTests
{
    private static readonly byte[] Bytes = [1, 2, 3, 4, 5];

    [TestMethod]
    public void Read_FillsTheBufferFromTheStream()
    {
        using ScriptedStream stream = new(Bytes);
        StreamUpload upload = new(stream, 0);
        byte[] buffer = new byte[3];

        long read = upload.Read(buffer);

        Assert.AreEqual(3, read);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, buffer);
    }

    [TestMethod]
    public void Read_StreamThrows_ReturnsAbort()
    {
        using ScriptedStream stream = new(Bytes) { ThrowOnRead = true };
        StreamUpload upload = new(stream, 0);

        Assert.AreEqual(ReadCallbackResult.Abort, upload.Read(new byte[3]));
    }

    [TestMethod]
    public void Seek_FromBegin_CountsFromTheUploadsStart()
    {
        using ScriptedStream stream = new(Bytes) { Position = 4 };
        StreamUpload upload = new(stream, 2);

        SeekResult result = upload.Seek(1, SeekOrigin.Begin);

        Assert.AreEqual(SeekResult.Ok, result);
        Assert.AreEqual(3, stream.Position);
    }

    [TestMethod]
    public void Seek_FromCurrent_MovesByTheOffset()
    {
        using ScriptedStream stream = new(Bytes) { Position = 4 };
        StreamUpload upload = new(stream, 2);

        SeekResult result = upload.Seek(-1, SeekOrigin.Current);

        Assert.AreEqual(SeekResult.Ok, result);
        Assert.AreEqual(3, stream.Position);
    }

    [TestMethod]
    public void Seek_NonSeekableStream_ReturnsCantSeek()
    {
        using ScriptedStream stream = new(Bytes) { Seekable = false };
        StreamUpload upload = new(stream, 0);

        Assert.AreEqual(SeekResult.CantSeek, upload.Seek(0, SeekOrigin.Begin));
    }

    [TestMethod]
    public void Seek_StreamThrows_ReturnsFail()
    {
        using ScriptedStream stream = new(Bytes) { ThrowOnSeek = true };
        StreamUpload upload = new(stream, 0);

        Assert.AreEqual(SeekResult.Fail, upload.Seek(0, SeekOrigin.Begin));
    }
}
