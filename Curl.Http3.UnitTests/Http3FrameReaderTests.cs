using Curl.Testing;
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
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Constructor_InvalidArguments_AreRejected()
    {
        Diagnostics.Arrange("constructor arguments", "null stream; payload limit -1; payload limit Array.MaxLength + 1");

        var nullStream = Assert.ThrowsExactly<ArgumentNullException>(() => new Http3FrameReader(null!, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Http3FrameReader(Stream.Null, -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Http3FrameReader(Stream.Null, (long)Array.MaxLength + 1));

        Diagnostics.Act("null stream's parameter", nullStream.ParamName);
        Diagnostics.Assert("exceptions", "ArgumentNullException, ArgumentOutOfRangeException x2", "as expected");
    }

    [TestMethod]
    public async Task ReadFrameAsync_UnknownAndGreaseTypes_AreSkipped()
    {
        // 0x21 is a grease type carrying 5000 bytes, more than one skip chunk; 0x0a and 0x52ce are unknown.
        var grease = "21 5388 " + new string('0', 10000);
        var stream = StreamOf(grease + " 0a 02 abcd 07 01 04 8000 52ce 00 07 01 00");
        Diagnostics.Arrange("stream", "grease 0x21 (5000 zero bytes), unknown 0x0a (2 bytes), GOAWAY 4, unknown 0x52ce (0 bytes), GOAWAY 0");
        Diagnostics.Bytes("stream", stream.ToArray());

        var frames = await ReadAllFramesAsync(stream);

        Diagnostics.Act("frames", string.Join(", ", frames));
        var ids = frames.Cast<Http3GoawayFrame>().Select(frame => frame.Id).ToArray();
        Diagnostics.Assert("GOAWAY IDs", "4, 0", string.Join(", ", ids));
        CollectionAssert.AreEqual(new long[] { 4, 0 }, ids);
    }

    [TestMethod]
    [DataRow("02 00", DisplayName = "PRIORITY")]
    [DataRow("06 00", DisplayName = "PING")]
    [DataRow("08 00", DisplayName = "WINDOW_UPDATE")]
    [DataRow("09 00", DisplayName = "CONTINUATION")]
    public async Task ReadFrameAsync_Http2FrameType_IsFrameUnexpected(string hex)
    {
        Diagnostics.Arrange("stream", hex);

        var error = await ErrorOfAsync(() => ReadAllFramesAsync(StreamOf(hex)));

        Diagnostics.Act("connection error", error);
        Diagnostics.Assert("connection error", Http3ErrorCode.FrameUnexpected, error);
        Assert.AreEqual(Http3ErrorCode.FrameUnexpected, error);
    }

    [TestMethod]
    public async Task ReadFrameAsync_StreamEndingBetweenFrames_GivesNull()
    {
        Diagnostics.Arrange("stream", "07 01 00 (one GOAWAY), payload limit 16");
        Http3FrameReader reader = new(StreamOf("07 01 00"), 16);

        var first = await reader.ReadFrameAsync(CancellationToken.None);
        Diagnostics.Act("first frame", first);
        Assert.IsNotNull(first);
        Assert.IsFalse(reader.HasReachedEndOfStream);
        var second = await reader.ReadFrameAsync(CancellationToken.None);
        Diagnostics.Act("second frame", second);
        Diagnostics.Assert("end of stream", true, reader.HasReachedEndOfStream);
        Assert.IsNull(second);
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
        Diagnostics.Arrange("stream", hex);
        Http3FrameReader reader = new(StreamOf(hex), 16);

        var error = await ErrorOfAsync(async () => await reader.ReadFrameAsync(CancellationToken.None));

        Diagnostics.Act("connection error", error);
        Diagnostics.Assert("connection error", Http3ErrorCode.FrameError, error);
        Assert.AreEqual(Http3ErrorCode.FrameError, error);
        Diagnostics.Assert("end of stream", true, reader.HasReachedEndOfStream);
        Assert.IsTrue(reader.HasReachedEndOfStream);
    }

    [TestMethod]
    public async Task ReadFrameAsync_PayloadOverTheLimit_IsExcessiveLoad()
    {
        Diagnostics.Arrange("stream", "00 03 616263 (DATA, 3 bytes), payload limit 2");
        Http3FrameReader reader = new(StreamOf("00 03 616263"), 2);

        var error = await ErrorOfAsync(async () => await reader.ReadFrameAsync(CancellationToken.None));

        Diagnostics.Act("connection error", error);
        Diagnostics.Assert("connection error", Http3ErrorCode.ExcessiveLoad, error);
        Assert.AreEqual(Http3ErrorCode.ExcessiveLoad, error);
    }

    [TestMethod]
    public async Task ReadFrameAsync_PayloadAtTheLimit_IsRead()
    {
        Diagnostics.Arrange("stream", "00 02 6162 (DATA, 2 bytes), payload limit 2");
        Http3FrameReader reader = new(StreamOf("00 02 6162"), 2);

        var frame = await reader.ReadFrameAsync(CancellationToken.None);

        Diagnostics.Act("frame", frame);
        Diagnostics.Assert("frame type", nameof(Http3DataFrame), frame?.GetType().Name);
        Assert.IsInstanceOfType<Http3DataFrame>(frame);
    }

    [TestMethod]
    public async Task ReadFrameOrDataAsync_DataFrameOverTheLimit_IsReadInFullAcrossSeveralCalls()
    {
        // nghttp3 never buffers a DATA payload (BL-838): ten bytes pass a two-byte limit, three at a time.
        Diagnostics.Arrange("stream", "00 0a 30313233343536373839 (DATA, 10 bytes), payload limit 2, buffer 3");
        Http3FrameReader reader = new(StreamOf("00 0a 30313233343536373839"), 2);
        byte[] buffer = new byte[3];
        MemoryStream payload = new();
        List<int> counts = [];

        while (await reader.ReadFrameOrDataAsync(buffer, CancellationToken.None) is { IsEndOfStream: false } read)
        {
            Assert.IsNull(read.Frame);
            counts.Add(read.DataLength);
            payload.Write(buffer, 0, read.DataLength);
        }

        Diagnostics.Act("data lengths", string.Join(", ", counts));
        Diagnostics.Bytes("payload", payload.ToArray());
        Diagnostics.Act("payload length", payload.Length);
        Diagnostics.Assert("data lengths", "3, 3, 3, 1", string.Join(", ", counts));
        CollectionAssert.AreEqual(new[] { 3, 3, 3, 1 }, counts);
        Diagnostics.Diff("payload", "0123456789", System.Text.Encoding.ASCII.GetString(payload.ToArray()));
        Assert.AreEqual("0123456789", System.Text.Encoding.ASCII.GetString(payload.ToArray()));
        Assert.IsTrue(reader.HasReachedEndOfStream);
    }

    [TestMethod]
    public async Task ReadFrameOrDataAsync_HeadersFrameOverTheLimit_IsExcessiveLoad()
    {
        Diagnostics.Arrange("stream", "01 03 616263 (HEADERS, 3 bytes), payload limit 2");
        Http3FrameReader reader = new(StreamOf("01 03 616263"), 2);

        var error = await ErrorOfAsync(async () => await reader.ReadFrameOrDataAsync(new byte[16], CancellationToken.None));

        Diagnostics.Act("connection error", error);
        Diagnostics.Assert("connection error", Http3ErrorCode.ExcessiveLoad, error);
        Assert.AreEqual(Http3ErrorCode.ExcessiveLoad, error);
    }

    [TestMethod]
    public async Task ReadFrameOrDataAsync_StreamEndingInsideADataPayload_IsFrameError()
    {
        Diagnostics.Arrange("stream", "00 05 6865 (DATA of 5 bytes, 2 present), payload limit 2");
        Http3FrameReader reader = new(StreamOf("00 05 6865"), 2);
        byte[] buffer = new byte[16];

        var first = await reader.ReadFrameOrDataAsync(buffer, CancellationToken.None);
        Diagnostics.Act("first data length", first.DataLength);
        Assert.AreEqual(2, first.DataLength);
        var error = await ErrorOfAsync(async () => await reader.ReadFrameOrDataAsync(buffer, CancellationToken.None));
        Diagnostics.Act("connection error", error);
        Diagnostics.Assert("connection error", Http3ErrorCode.FrameError, error);
        Assert.AreEqual(Http3ErrorCode.FrameError, error);
        Assert.IsTrue(reader.HasReachedEndOfStream);
    }

    [TestMethod]
    public async Task ReadFrameOrDataAsync_ZeroLengthDataFrameThenHeaders_GivesTheHeadersFrame()
    {
        Diagnostics.Arrange("stream", "00 00 (empty DATA) 01 02 0000 (HEADERS)");
        Http3FrameReader reader = new(StreamOf("00 00 01 02 0000"), 16);

        Http3FrameOrData read = await reader.ReadFrameOrDataAsync(new byte[16], CancellationToken.None);

        Diagnostics.Act("frame", read.Frame);
        Diagnostics.Act("data length", read.DataLength);
        Diagnostics.Assert("frame type", nameof(Http3HeadersFrame), read.Frame?.GetType().Name);
        Assert.IsInstanceOfType<Http3HeadersFrame>(read.Frame);
        Assert.AreEqual(0, read.DataLength);
        Assert.IsFalse(read.IsEndOfStream);
        Assert.IsTrue((await reader.ReadFrameOrDataAsync(new byte[16], CancellationToken.None)).IsEndOfStream);
    }

    [TestMethod]
    public async Task ReadFrameOrDataAsync_UnknownAndGreaseTypesBetweenDataFrames_AreSkipped()
    {
        Diagnostics.Arrange("stream", "DATA 'a', grease 0x21 (2 bytes), unknown 0x0a (0 bytes), DATA 'b'");
        Http3FrameReader reader = new(StreamOf("00 01 61 21 02 ffff 0a 00 00 01 62"), 16);
        byte[] buffer = new byte[16];
        MemoryStream payload = new();

        while (await reader.ReadFrameOrDataAsync(buffer, CancellationToken.None) is { IsEndOfStream: false } read)
        {
            payload.Write(buffer, 0, read.DataLength);
        }

        Diagnostics.Bytes("payload", payload.ToArray());
        Diagnostics.Act("payload length", payload.Length);
        Diagnostics.Diff("payload", "ab", System.Text.Encoding.ASCII.GetString(payload.ToArray()));
        Assert.AreEqual("ab", System.Text.Encoding.ASCII.GetString(payload.ToArray()));
    }

    [TestMethod]
    [DataRow("00 01 61 02 00", DisplayName = "PRIORITY")]
    [DataRow("00 01 61 06 00", DisplayName = "PING")]
    [DataRow("00 01 61 08 00", DisplayName = "WINDOW_UPDATE")]
    [DataRow("00 01 61 09 00", DisplayName = "CONTINUATION")]
    public async Task ReadFrameOrDataAsync_Http2FrameTypeAfterData_IsFrameUnexpected(string hex)
    {
        Diagnostics.Arrange("stream", hex);
        Http3FrameReader reader = new(StreamOf(hex), 16);
        byte[] buffer = new byte[16];

        var first = await reader.ReadFrameOrDataAsync(buffer, CancellationToken.None);
        Diagnostics.Act("first data length", first.DataLength);
        Assert.AreEqual(1, first.DataLength);
        var error = await ErrorOfAsync(async () => await reader.ReadFrameOrDataAsync(buffer, CancellationToken.None));
        Diagnostics.Act("connection error", error);
        Diagnostics.Assert("connection error", Http3ErrorCode.FrameUnexpected, error);
        Assert.AreEqual(Http3ErrorCode.FrameUnexpected, error);
    }

    [TestMethod]
    public void ReadFrameOrDataAsync_EmptyBuffer_IsRejected()
    {
        Diagnostics.Arrange("buffer", "Memory<byte>.Empty");
        Http3FrameReader reader = new(StreamOf("00 01 61"), 16);

        var failure = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => reader.ReadFrameOrDataAsync(Memory<byte>.Empty, CancellationToken.None));

        Diagnostics.Act("parameter", failure.ParamName);
        Diagnostics.Assert("exception", nameof(ArgumentOutOfRangeException), failure.GetType().Name);
    }
}
