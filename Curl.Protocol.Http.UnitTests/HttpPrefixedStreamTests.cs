using System.Text;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpPrefixedStream" />: the prefix is read first, in as many reads as the
/// buffers take, then the rest of the stream, through every read overload; it neither seeks
/// nor writes.
/// </summary>
[TestClass]
public sealed class HttpPrefixedStreamTests
{
    [TestMethod]
    public async Task ReadAsync_ReadsThePrefixThenTheRest()
    {
        using HttpPrefixedStream stream = new("abc"u8.ToArray(), new MemoryStream("de"u8.ToArray()));
        byte[] buffer = new byte[2];

        Assert.AreEqual(2, await stream.ReadAsync(buffer.AsMemory(), CancellationToken.None));
        Assert.AreEqual("ab", Encoding.Latin1.GetString(buffer));
        Assert.AreEqual(1, await stream.ReadAsync(buffer, 0, 2, CancellationToken.None));
        Assert.AreEqual((byte)'c', buffer[0]);
        Assert.AreEqual(2, await stream.ReadAsync(buffer.AsMemory(), CancellationToken.None));
        Assert.AreEqual("de", Encoding.Latin1.GetString(buffer));
        Assert.AreEqual(0, await stream.ReadAsync(buffer.AsMemory(), CancellationToken.None));
    }

    [TestMethod]
    public void Read_ReadsThePrefixThenTheRest()
    {
        using HttpPrefixedStream stream = new("ab"u8.ToArray(), new MemoryStream("cd"u8.ToArray()));
        byte[] buffer = new byte[4];

        Assert.AreEqual(2, stream.Read(buffer, 0, 4));
        Assert.AreEqual(2, stream.Read(buffer, 2, 2));
        Assert.AreEqual("abcd", Encoding.Latin1.GetString(buffer));
        Assert.AreEqual(0, stream.Read(buffer.AsSpan()));
    }

    [TestMethod]
    public void Members_ReadOnlyAndForwardOnly()
    {
        using HttpPrefixedStream stream = new(ReadOnlyMemory<byte>.Empty, new MemoryStream());

        stream.Flush();

        Assert.IsTrue(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsFalse(stream.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write([1], 0, 1));
    }
}
