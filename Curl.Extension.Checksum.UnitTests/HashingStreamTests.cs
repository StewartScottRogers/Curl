using System.Security.Cryptography;

namespace Curl.Extension.Checksum.UnitTests;

[TestClass]
public sealed class HashingStreamTests
{
    [TestMethod]
    public void IsWriteOnly()
    {
        using var hash = new BclChecksumHash(HashAlgorithmName.SHA256);
        using var stream = new HashingStream(Stream.Null, hash);

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

    [TestMethod]
    public async Task Write_PassesEveryByteOnAndHashesIt()
    {
        using var hash = new BclChecksumHash(HashAlgorithmName.SHA256);
        var output = new MemoryStream();
        await using (var stream = new HashingStream(output, hash))
        {
            stream.Write("ab"u8.ToArray(), 1, 1);
            await stream.WriteAsync("xcx"u8.ToArray(), 1, 1);
            stream.Flush();
            await stream.FlushAsync();
        }

        CollectionAssert.AreEqual("bc"u8.ToArray(), output.ToArray());
        CollectionAssert.AreEqual(SHA256.HashData("bc"u8), hash.GetHashAndReset());
        Assert.IsTrue(output.CanWrite, "Disposing the hashing stream closed the stream beneath.");
    }
}
