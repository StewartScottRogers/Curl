using System.Security.Cryptography;

namespace Curl.Extension.ContentDigest.UnitTests;

[TestClass]
public sealed class DigestingStreamTests
{
    [TestMethod]
    public async Task Writes_PassTheBytesOnAndHashThemWithSha256AndSha512()
    {
        using MemoryStream output = new();
        using DigestingStream stream = new(output);

        stream.Write([0, 1, 2, 3], 1, 1);
        await stream.WriteAsync(new byte[] { 2 }.AsMemory());
        await stream.WriteAsync([0, 3, 0], 1, 1, CancellationToken.None);
        stream.Flush();
        await stream.FlushAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, output.ToArray());
        CollectionAssert.AreEqual(SHA256.HashData(new byte[] { 1, 2, 3 }), stream.DigestOf("sha-256"));
        CollectionAssert.AreEqual(SHA512.HashData(new byte[] { 1, 2, 3 }), stream.DigestOf("sha-512"));
        Assert.IsNull(stream.DigestOf("md5"));
    }

    [TestMethod]
    public void TheStream_IsWriteOnlyAndNotSeekable()
    {
        using DigestingStream stream = new(Stream.Null);

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
