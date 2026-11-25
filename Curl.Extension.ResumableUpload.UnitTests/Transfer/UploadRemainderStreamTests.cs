using System.Text;

namespace Curl.Extension.ResumableUpload.Transfer;

[TestClass]
public sealed class UploadRemainderStreamTests
{
    [TestMethod]
    public void Constructor_MovesTheUploadToTheStartAndGivesTheRemainder()
    {
        var upload = Upload();

        var window = new UploadRemainderStream(upload, 4);

        Assert.AreEqual(4L, upload.Position);
        Assert.AreEqual((6L, 0L), (window.Length, window.Position));
        Assert.AreEqual((true, true, false), (window.CanRead, window.CanSeek, window.CanWrite));
    }

    [TestMethod]
    public async Task Read_EveryOverload_GivesTheUploadsNextBytes()
    {
        var window = new UploadRemainderStream(Upload(), 4);
        var read = new byte[6];

        Assert.AreEqual(1, window.Read(read, 0, 1));
        Assert.AreEqual(1, window.Read(read.AsSpan(1, 1)));
        Assert.AreEqual(2, await window.ReadAsync(read, 2, 2, CancellationToken.None));
        Assert.AreEqual(2, await window.ReadAsync(read.AsMemory(4)));

        Assert.AreEqual("456789", Encoding.ASCII.GetString(read));
        Assert.AreEqual(0, window.Read(read, 0, 6));
        Assert.AreEqual(6L, window.Position);
    }

    [TestMethod]
    [DataRow(1L, SeekOrigin.Begin, 1L)]
    [DataRow(1L, SeekOrigin.Current, 3L)]
    [DataRow(-1L, SeekOrigin.End, 5L)]
    public void Seek_IsRelativeToTheWindow(long offset, SeekOrigin origin, long expected)
    {
        var upload = Upload();
        var window = new UploadRemainderStream(upload, 4) { Position = 2 };

        Assert.AreEqual(expected, window.Seek(offset, origin));

        Assert.AreEqual((expected, expected + 4), (window.Position, upload.Position));
    }

    [TestMethod]
    public void Position_BeforeTheWindow_IsRefused()
    {
        var window = new UploadRemainderStream(Upload(), 4);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => window.Position = -1);
    }

    [TestMethod]
    public void Writing_IsNotSupportedAndFlushAndDisposeLeaveTheUploadAsItIs()
    {
        var upload = Upload();
        var window = new UploadRemainderStream(upload, 4);

        Assert.ThrowsExactly<NotSupportedException>(() => window.Write([1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => window.SetLength(0));
        window.Flush();
        window.Dispose();

        Assert.IsTrue(upload.CanRead);
        Assert.AreEqual("0123456789", Encoding.ASCII.GetString(upload.ToArray()));
    }

    private static MemoryStream Upload() => new(Encoding.ASCII.GetBytes("0123456789"));
}
