using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins <see cref="Http2PushedResponse" /> as the read-only, asynchronous-only stream a push sink
/// is given (BL-2516); reading it is pinned by <see cref="Http2SessionPushTests" />.
/// </summary>
[TestClass]
public sealed class Http2PushedResponseTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Content_IsTheResponseItselfReadableOnly()
    {
        Http2PushedResponse response = Promised(4);
        Diagnostics.Arrange("promised stream", 4);

        Stream content = response.Content;

        Diagnostics.Act("can read, seek, write", string.Join(", ", content.CanRead, content.CanSeek, content.CanWrite));
        Diagnostics.Assert("stream id", 4, response.StreamId);
        Assert.AreSame(response, content);
        Assert.AreEqual(4, response.StreamId);
        Assert.IsTrue(content.CanRead);
        Assert.IsFalse(content.CanSeek);
        Assert.IsFalse(content.CanWrite);
        content.Flush();
    }

    [TestMethod]
    public void SynchronousAndPositionalMembers_ThrowNotSupported()
    {
        Http2PushedResponse response = Promised(2);
        byte[] buffer = new byte[1];
        Diagnostics.Arrange("promised stream", 2);

        Diagnostics.Act("members", "Length, Position, Read, Write, Seek, SetLength");

        Diagnostics.Assert("each member", nameof(NotSupportedException), "thrown");
        _ = Assert.ThrowsExactly<NotSupportedException>(() => response.Length);
        _ = Assert.ThrowsExactly<NotSupportedException>(() => response.Position);
        _ = Assert.ThrowsExactly<NotSupportedException>(() => response.Position = 0);
        _ = Assert.ThrowsExactly<NotSupportedException>(() => response.Read(buffer, 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => response.Write(buffer, 0, 1));
        _ = Assert.ThrowsExactly<NotSupportedException>(() => response.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => response.SetLength(0));
    }

    private static Http2PushedResponse Promised(int streamId)
    {
        Http2StreamConnection stream = new(new Http2Session(new ScriptedConnection([], 65536), isPushEnabled: true), string.Empty, bodyLength: 0);
        stream.AdoptPromised(streamId);
        return new Http2PushedResponse(stream);
    }
}
