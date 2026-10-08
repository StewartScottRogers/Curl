using Curl.Testing;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Pins <see cref="PageantAgentStream" />: a request frame is exchanged only once it is
/// whole, and the stream is neither seekable nor sized.
/// </summary>
[TestClass]
public sealed class PageantAgentStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Write_FrameInPieces_ExchangesItOnceWhole()
    {
        List<byte[]> frames = [];
        using PageantAgentStream stream = new(frame =>
        {
            frames.Add(frame);
            return [0, 0, 0, 1, 5];
        });
        Diagnostics.Arrange("writes", "00 00, then 00 02 0b, then 0c; the agent answers 00 00 00 01 05");

        stream.Write([0, 0], 0, 2);
        stream.Write([0, 2, 11], 0, 3);
        int before = frames.Count;
        stream.Write([12], 0, 1);
        byte[] answer = new byte[8];
        int read = stream.Read(answer, 0, answer.Length);

        Diagnostics.Act("frames exchanged before the last piece", before);
        Diagnostics.Act("frames exchanged", frames.Count);
        Diagnostics.Act("bytes read", read);
        Diagnostics.Assert("frames exchanged before the last piece", 0, before);
        Diagnostics.Assert("frames exchanged", 1, frames.Count);
        if (frames.Count > 0)
        {
            Diagnostics.Diff("frame", new byte[] { 0, 0, 0, 2, 11, 12 }, frames[0]);
        }

        Diagnostics.Assert("bytes read", 5, read);
        Assert.AreEqual(0, before);
        Assert.HasCount(1, frames);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 2, 11, 12 }, frames[0]);
        Assert.AreEqual(5, read);
    }

    [TestMethod]
    public void Read_NothingExchanged_ReadsNothing()
    {
        using PageantAgentStream stream = new(_ => []);
        Diagnostics.Arrange("writes", "none");

        int read = stream.Read(new byte[4], 0, 4);

        Diagnostics.Act("bytes read", read);
        Diagnostics.Assert("bytes read", 0, read);
        Assert.AreEqual(0, read);
    }

    [TestMethod]
    public void Members_OfAStreamThatOnlyReadsAndWrites_AnswerAsSuch()
    {
        using PageantAgentStream stream = new(_ => []);
        Diagnostics.Arrange("stream", "a Pageant stream whose agent answers nothing");

        stream.Flush();

        string abilities = $"read {stream.CanRead}, write {stream.CanWrite}, seek {stream.CanSeek}";
        Diagnostics.Act("abilities", abilities);
        Diagnostics.Assert("abilities", "read True, write True, seek False", abilities);
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
