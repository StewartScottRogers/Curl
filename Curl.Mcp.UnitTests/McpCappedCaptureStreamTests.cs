namespace Curl.Mcp;

[TestClass]
public sealed class McpCappedCaptureStreamTests
{
    [TestMethod]
    public void Write_PastTheCap_KeepsTheCapAndCountsEveryByte()
    {
        using McpCappedCaptureStream stream = new(new McpResponseCap(4));

        stream.Write("abc"u8);
        stream.Write("defgh"u8.ToArray(), 1, 3);
        stream.Write("ijk"u8);

        McpReturnedBytes returned = stream.ToReturnedBytes();
        Assert.AreEqual(9, stream.TotalBytes);
        Assert.AreEqual("abce", returned.Content);
        Assert.AreEqual(9, returned.TotalBytes);
        Assert.IsTrue(returned.IsTruncated);
    }

    [TestMethod]
    public void Write_ACutThatSplitsACharacter_BacksOffToItsStart()
    {
        using McpCappedCaptureStream stream = new(new McpResponseCap(4));

        stream.Write("abcé!"u8);

        McpReturnedBytes returned = stream.ToReturnedBytes();
        Assert.AreEqual("abc", returned.Content);
        Assert.AreEqual(6, returned.TotalBytes);
    }

    [TestMethod]
    public async Task WriteAsync_BothOverloads_Capture()
    {
        using McpCappedCaptureStream stream = new(new McpResponseCap(int.MaxValue));

        await stream.WriteAsync("ab"u8.ToArray(), CancellationToken.None);
        await stream.WriteAsync("xcdx"u8.ToArray(), 1, 2, CancellationToken.None);
        await stream.FlushAsync();
        stream.Flush();

        Assert.AreEqual("abcd", stream.ToReturnedBytes().Content);
        Assert.IsFalse(stream.ToReturnedBytes().IsTruncated);
    }

    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using McpCappedCaptureStream stream = new(new McpResponseCap(4));

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => stream.Write(new byte[1], 0, 2));
    }
}
