namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Pins <see cref="PageantAgentStream" />: a request frame is exchanged only once it is
/// whole, and the stream is neither seekable nor sized.
/// </summary>
[TestClass]
public sealed class PageantAgentStreamTests
{
    [TestMethod]
    public void Write_FrameInPieces_ExchangesItOnceWhole()
    {
        List<byte[]> frames = [];
        using PageantAgentStream stream = new(frame =>
        {
            frames.Add(frame);
            return [0, 0, 0, 1, 5];
        });

        stream.Write([0, 0], 0, 2);
        stream.Write([0, 2, 11], 0, 3);
        int before = frames.Count;
        stream.Write([12], 0, 1);
        byte[] answer = new byte[8];
        int read = stream.Read(answer, 0, answer.Length);

        Assert.AreEqual(0, before);
        Assert.HasCount(1, frames);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 2, 11, 12 }, frames[0]);
        Assert.AreEqual(5, read);
    }

    [TestMethod]
    public void Read_NothingExchanged_ReadsNothing()
    {
        using PageantAgentStream stream = new(_ => []);

        int read = stream.Read(new byte[4], 0, 4);

        Assert.AreEqual(0, read);
    }

    [TestMethod]
    public void Members_OfAStreamThatOnlyReadsAndWrites_AnswerAsSuch()
    {
        using PageantAgentStream stream = new(_ => []);

        stream.Flush();

        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanWrite);
        Assert.IsFalse(stream.CanSeek);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }
}
