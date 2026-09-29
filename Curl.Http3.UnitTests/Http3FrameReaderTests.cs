using static Curl.Http3.Http3;

namespace Curl.Http3;

/// <summary>
/// Pins <see cref="Http3FrameReader" />: unknown and grease frame types skipped (RFC 9114
/// section 9), HTTP/2's frame types refused (section 7.2.8), and a stream ending inside a
/// frame (section 7.1).
/// </summary>
[TestClass]
public sealed class Http3FrameReaderTests
{
    [TestMethod]
    public void Constructor_InvalidArguments_AreRejected()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3FrameReader(null!, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Http3FrameReader(Stream.Null, -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Http3FrameReader(Stream.Null, (long)Array.MaxLength + 1));
    }

    [TestMethod]
    public async Task ReadFrameAsync_UnknownAndGreaseTypes_AreSkipped()
    {
        // 0x21 is a grease type carrying 5000 bytes, more than one skip chunk; 0x0a and 0x52ce are unknown.
        var grease = "21 5388 " + new string('0', 10000);
        var stream = StreamOf(grease + " 0a 02 abcd 07 01 04 8000 52ce 00 07 01 00");

        var frames = await ReadAllFramesAsync(stream);

        CollectionAssert.AreEqual(new long[] { 4, 0 }, frames.Cast<Http3GoawayFrame>().Select(frame => frame.Id).ToArray());
    }

    [TestMethod]
    [DataRow("02 00", DisplayName = "PRIORITY")]
    [DataRow("06 00", DisplayName = "PING")]
    [DataRow("08 00", DisplayName = "WINDOW_UPDATE")]
    [DataRow("09 00", DisplayName = "CONTINUATION")]
    public async Task ReadFrameAsync_Http2FrameType_IsFrameUnexpected(string hex) =>
        Assert.AreEqual(Http3ErrorCode.FrameUnexpected, await ErrorOfAsync(() => ReadAllFramesAsync(StreamOf(hex))));

    [TestMethod]
    public async Task ReadFrameAsync_StreamEndingBetweenFrames_GivesNull()
    {
        Http3FrameReader reader = new(StreamOf("07 01 00"), 16);

        Assert.IsNotNull(await reader.ReadFrameAsync(CancellationToken.None));
        Assert.IsFalse(reader.HasReachedEndOfStream);
        Assert.IsNull(await reader.ReadFrameAsync(CancellationToken.None));
        Assert.IsTrue(reader.HasReachedEndOfStream);
    }

    [TestMethod]
    [DataRow("40", DisplayName = "inside the type")]
    [DataRow("07", DisplayName = "before the length")]
    [DataRow("07 40", DisplayName = "inside the length")]
    [DataRow("00 05 6865", DisplayName = "inside the payload")]
    [DataRow("21 05 6865", DisplayName = "inside a skipped payload")]
    public async Task ReadFrameAsync_StreamEndingInsideAFrame_IsFrameError(string hex)
    {
        Http3FrameReader reader = new(StreamOf(hex), 16);

        Assert.AreEqual(Http3ErrorCode.FrameError, await ErrorOfAsync(async () => await reader.ReadFrameAsync(CancellationToken.None)));
        Assert.IsTrue(reader.HasReachedEndOfStream);
    }

    [TestMethod]
    public async Task ReadFrameAsync_PayloadOverTheLimit_IsExcessiveLoad()
    {
        Http3FrameReader reader = new(StreamOf("00 03 616263"), 2);

        Assert.AreEqual(Http3ErrorCode.ExcessiveLoad, await ErrorOfAsync(async () => await reader.ReadFrameAsync(CancellationToken.None)));
    }

    [TestMethod]
    public async Task ReadFrameAsync_PayloadAtTheLimit_IsRead()
    {
        Http3FrameReader reader = new(StreamOf("00 02 6162"), 2);

        Assert.IsInstanceOfType<Http3DataFrame>(await reader.ReadFrameAsync(CancellationToken.None));
    }
}
