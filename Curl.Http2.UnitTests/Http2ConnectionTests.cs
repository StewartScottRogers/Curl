using Curl.Testing;
using static Curl.Http2.Hpack;
using static Curl.Http2.Http2FrameFactory;
using static Curl.Http2.Http2Test;

namespace Curl.Http2;

/// <summary>
/// Drives <see cref="Http2Connection" /> through an in-memory peer: the preface curl sends,
/// frames split across reads, SETTINGS and PING answered, flow control in both directions,
/// and GOAWAY, RST_STREAM and protocol errors turned into typed failures.
/// </summary>
[TestClass]
public sealed class Http2ConnectionTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    // Measured 2026-09-28 with curl.se's Windows build, curl 8.18.0 (LibreSSL, nghttp2 1.68.0),
    // --http2-prior-knowledge http://127.0.0.1:18657/ through Record-CurlExchange.ps1: the
    // preface, SETTINGS (MAX_CONCURRENT_STREAMS 100, INITIAL_WINDOW_SIZE 65536, ENABLE_PUSH 0)
    // and a connection WINDOW_UPDATE of 1048510465. The same SETTINGS are the HTTP2-Settings
    // header curl sends for h2c (ADR-0141: AAMAAABkAAQAAQAAAAIAAAAA).
    private const string MeasuredPreface =
        "505249202a20485454502f322e300d0a0d0a534d0d0a0d0a" +
        "000012 04 00 00000000 0003 00000064 0004 00010000 0002 00000000" +
        "000004 08 00 00000000 3e7f0001";

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SendPrefaceAsync_WritesThePrefaceSettingsAndWindowUpdateCurlSends()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect();
        diagnostics.Arrange("peer frames", "none");

        await connection.SendPrefaceAsync(None);
        diagnostics.Act("bytes written by SendPrefaceAsync", peer.Written.Length);
        diagnostics.Bytes("written", peer.Written.ToArray());

        diagnostics.Diff("preface bytes", FromHex(MeasuredPreface), peer.Written.ToArray());
        diagnostics.Assert("connection receive window", 1048576000, connection.ConnectionReceiveWindow.Size);
        CollectionAssert.AreEqual(FromHex(MeasuredPreface), peer.Written.ToArray());
        Assert.AreEqual(1048576000, connection.ConnectionReceiveWindow.Size);
        CollectionAssert.AreEqual("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8.ToArray(), Http2Connection.ClientPreface.ToArray());
    }

    [TestMethod]
    public void New_NullStream_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("stream", null);

        var error = Assert.ThrowsExactly<ArgumentNullException>(() => new Http2Connection(null!));
        diagnostics.Act("exception", error.GetType().Name);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), error.GetType().Name);
    }

    [TestMethod]
    public void OpenStream_AllocatesOddIdentifiersInOrder()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect();
        diagnostics.Arrange("peer frames", "none");

        var first = connection.OpenStream();
        var second = connection.OpenStream();
        var third = connection.OpenStream();
        diagnostics.Act("stream identifiers", string.Join(", ", first, second, third));
        diagnostics.Act("open stream count", connection.OpenStreamCount);

        diagnostics.Assert("first identifier", 1, first);
        diagnostics.Assert("second identifier", 3, second);
        diagnostics.Assert("third identifier", 5, third);
        diagnostics.Assert("open stream count", 3, connection.OpenStreamCount);
        Assert.AreEqual(1, first);
        Assert.AreEqual(3, second);
        Assert.AreEqual(5, third);
        Assert.AreEqual(3, connection.OpenStreamCount);
    }

    [TestMethod]
    public void OpenStream_IdentifiersExhausted_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var peer = new PeerStream([]);
        var connection = new Http2Connection(peer, int.MaxValue, 65535, 65536);
        diagnostics.Arrange("next stream identifier", int.MaxValue);

        var last = connection.OpenStream();
        diagnostics.Act("identifier opened", last);
        diagnostics.Assert("identifier opened", int.MaxValue, last);
        Assert.AreEqual(int.MaxValue, last);
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => connection.OpenStream());
        diagnostics.Act("exception", error.GetType().Name);
        diagnostics.Assert("exception type", nameof(InvalidOperationException), error.GetType().Name);
    }

    [TestMethod]
    public async Task OpenStream_PeerConcurrencyLimitReached_ThrowsUntilAStreamCloses()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(
            CreateSettings([new(Http2SettingIdentifier.MaxConcurrentStreams, 1)]),
            CreateHeaders(1, new byte[] { 0x88 }, isEndStream: true, isEndHeaders: true));
        diagnostics.Arrange("peer frames", "SETTINGS MaxConcurrentStreams=1, then HEADERS stream 1 with END_STREAM");
        var streamId = connection.OpenStream();
        await connection.WriteHeadersAsync(streamId, new byte[] { 0x82 }, isEndStream: false, None);
        _ = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("open stream count after the peer ended stream 1 half", connection.OpenStreamCount);

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => connection.OpenStream());
        diagnostics.Act("exception at the limit", error.GetType().Name);
        diagnostics.Assert("exception type", nameof(InvalidOperationException), error.GetType().Name);

        _ = await connection.WriteDataAsync(streamId, ReadOnlyMemory<byte>.Empty, isEndStream: true, None);
        var next = connection.OpenStream();
        diagnostics.Act("identifier after the stream closed", next);
        diagnostics.Assert("identifier after the stream closed", 3, next);
        Assert.AreEqual(3, next);
    }

    [TestMethod]
    public async Task OpenUpgradedStream_PeerEndsTheStream_ForgetsItSoTheNextStreamFitsTheConcurrencyLimit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(
            CreateSettings([new(Http2SettingIdentifier.MaxConcurrentStreams, 1)]),
            CreateHeaders(1, new byte[] { 0x88 }, isEndStream: true, isEndHeaders: true));
        diagnostics.Arrange("peer frames", "SETTINGS MaxConcurrentStreams=1, then HEADERS stream 1 with END_STREAM");

        var upgraded = connection.OpenUpgradedStream();
        diagnostics.Act("upgraded stream identifier", upgraded);
        diagnostics.Assert("upgraded stream identifier", 1, upgraded);
        Assert.AreEqual(1, upgraded);
        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.WriteHeadersAsync(1, new byte[] { 0x82 }, isEndStream: true, None));
        diagnostics.Act("WriteHeadersAsync on the upgraded stream", error.GetType().Name);
        diagnostics.Assert("exception type", nameof(InvalidOperationException), error.GetType().Name);
        _ = await connection.ReadStreamFrameAsync(None);
        var openStreamCount = connection.OpenStreamCount;
        diagnostics.Act("open stream count", openStreamCount);

        diagnostics.Assert("open stream count", 0, openStreamCount);
        Assert.AreEqual(0, openStreamCount);
        var next = connection.OpenStream();
        diagnostics.Act("next identifier", next);
        diagnostics.Assert("next identifier", 3, next);
        Assert.AreEqual(3, next);
    }

    [TestMethod]
    public async Task WriteHeadersAsync_SmallBlock_SendsOneHeadersFrame()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect();
        var streamId = connection.OpenStream();
        diagnostics.Arrange("header block", "82 86 (END_STREAM)");

        await connection.WriteHeadersAsync(streamId, new byte[] { 0x82, 0x86 }, isEndStream: true, None);
        diagnostics.Bytes("written", peer.Written.ToArray());

        diagnostics.Diff("HEADERS frame bytes", FromHex("000002 01 05 00000001 8286"), peer.Written.ToArray());
        CollectionAssert.AreEqual(FromHex("000002 01 05 00000001 8286"), peer.Written.ToArray());
        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.WriteHeadersAsync(streamId, new byte[] { 0x82 }, isEndStream: true, None));
        diagnostics.Act("second WriteHeadersAsync", error.GetType().Name);
        diagnostics.Assert("exception type", nameof(InvalidOperationException), error.GetType().Name);
    }

    [TestMethod]
    public async Task WriteHeadersAsync_BlockLargerThanAFrame_ContinuesItInContinuationFrames()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect();
        var streamId = connection.OpenStream();
        var block = Enumerable.Range(0, 40000).Select(index => (byte)index).ToArray();
        diagnostics.Arrange("header block length", block.Length);

        await connection.WriteHeadersAsync(streamId, block, isEndStream: false, None);

        var frames = await peer.WrittenFrames();
        diagnostics.Act("frames written", string.Join(" | ", frames.Select(Describe)));
        diagnostics.Assert("frame count", 3, frames.Count);
        AssertFrameWithDiagnostics(diagnostics, "frame 0", CreateHeaders(1, block[..16384], isEndStream: false, isEndHeaders: false), frames[0]);
        AssertFrameWithDiagnostics(diagnostics, "frame 1", CreateContinuation(1, block[16384..32768], isEndHeaders: false), frames[1]);
        AssertFrameWithDiagnostics(diagnostics, "frame 2", CreateContinuation(1, block[32768..], isEndHeaders: true), frames[2]);
        Assert.HasCount(3, frames);
    }

    [TestMethod]
    public async Task WriteHeadersAsync_StreamNotOpen_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect();
        diagnostics.Arrange("stream 1", "never opened");

        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.WriteHeadersAsync(1, new byte[] { 0x82 }, isEndStream: true, None));
        diagnostics.Act("exception", error.GetType().Name);
        diagnostics.Assert("exception type", nameof(InvalidOperationException), error.GetType().Name);
    }

    [TestMethod]
    public async Task WriteDataAsync_BodyWithinTheWindows_SendsItAndEndsTheStream()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect();
        var streamId = await StartStreamAsync(connection, peer);
        diagnostics.Arrange("body", "body (END_STREAM) on stream " + streamId);

        var sent = await connection.WriteDataAsync(streamId, "body"u8.ToArray(), isEndStream: true, None);
        diagnostics.Act("bytes sent", sent);
        diagnostics.Bytes("written", peer.Written.ToArray());

        diagnostics.Assert("bytes sent", 4, sent);
        Assert.AreEqual(4, sent);
        diagnostics.Diff("DATA frame bytes", FromHex("000004 00 01 00000001 626f6479"), peer.Written.ToArray());
        CollectionAssert.AreEqual(FromHex("000004 00 01 00000001 626f6479"), peer.Written.ToArray());
        diagnostics.Assert("connection send window", 65531, connection.ConnectionSendWindow.Size);
        Assert.AreEqual(65531, connection.ConnectionSendWindow.Size);
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => connection.GetAvailableSendWindow(3));
        diagnostics.Act("GetAvailableSendWindow(3)", error.GetType().Name);
        diagnostics.Assert("exception type", nameof(InvalidOperationException), error.GetType().Name);
    }

    [TestMethod]
    public async Task WriteDataAsync_BodyLargerThanAFrame_SplitsItAtThePeersMaximumFrameSize()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect();
        var streamId = await StartStreamAsync(connection, peer);
        diagnostics.Arrange("body length", 20000);

        var sent = await connection.WriteDataAsync(streamId, new byte[20000], isEndStream: false, None);

        var frames = await peer.WrittenFrames();
        diagnostics.Act("bytes sent", sent);
        diagnostics.Act("frames written", string.Join(" | ", frames.Select(Describe)));
        diagnostics.Assert("bytes sent", 20000, sent);
        Assert.AreEqual(20000, sent);
        AssertFrameWithDiagnostics(diagnostics, "frame 0", CreateData(1, new byte[16384], isEndStream: false), frames[0]);
        AssertFrameWithDiagnostics(diagnostics, "frame 1", CreateData(1, new byte[3616], isEndStream: false), frames[1]);
    }

    [TestMethod]
    public async Task WriteDataAsync_BodyLargerThanTheWindow_SendsOnlyTheWindowWithoutEndStream()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect(
            CreateSettings([new(Http2SettingIdentifier.InitialWindowSize, 10), new(Http2SettingIdentifier.MaxFrameSize, 20000)]),
            CreateWindowUpdate(1, 5));
        diagnostics.Arrange("peer frames", "SETTINGS InitialWindowSize=10 MaxFrameSize=20000, WINDOW_UPDATE stream 1 +5");
        var streamId = await StartStreamAsync(connection, peer);
        _ = await ReadUntilEnd(connection);
        diagnostics.Arrange("body length", 20);

        var sent = await connection.WriteDataAsync(streamId, new byte[20], isEndStream: true, None);

        var frames = await peer.WrittenFrames();
        diagnostics.Act("bytes sent", sent);
        diagnostics.Act("last frame written", Describe(frames[^1]));
        diagnostics.Assert("bytes sent", 15, sent);
        Assert.AreEqual(15, sent);
        AssertFrameWithDiagnostics(diagnostics, "last frame", CreateData(1, new byte[15], isEndStream: false), frames[^1]);
        var available = connection.GetAvailableSendWindow(streamId);
        diagnostics.Assert("available stream window", 0, available);
        Assert.AreEqual(0, connection.GetAvailableSendWindow(streamId));
    }

    [TestMethod]
    public async Task WriteDataAsync_WindowExhausted_SendsNothingUntilAWindowUpdate()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect(
            CreateSettings([new(Http2SettingIdentifier.InitialWindowSize, 0)]),
            CreateHeaders(1, new byte[] { 0x88 }, isEndStream: false, isEndHeaders: true),
            CreateWindowUpdate(1, 3));
        diagnostics.Arrange("peer frames", "SETTINGS InitialWindowSize=0, HEADERS stream 1, WINDOW_UPDATE stream 1 +3");
        var streamId = await StartStreamAsync(connection, peer);
        _ = await connection.ReadStreamFrameAsync(None);
        peer.Written.SetLength(0);

        var sentBefore = await connection.WriteDataAsync(streamId, "abcde"u8.ToArray(), isEndStream: true, None);
        diagnostics.Act("bytes sent with no window", sentBefore);
        diagnostics.Act("bytes written with no window", peer.Written.Length);
        diagnostics.Assert("bytes sent with no window", 0, sentBefore);
        Assert.AreEqual(0, sentBefore);
        diagnostics.Assert("bytes written with no window", 0L, peer.Written.Length);
        Assert.AreEqual(0, peer.Written.Length);

        var updateFrame = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("frame after WINDOW_UPDATE", updateFrame);
        diagnostics.Assert("frame after WINDOW_UPDATE", null, updateFrame);
        Assert.IsNull(updateFrame);
        var sentAfter = await connection.WriteDataAsync(streamId, "abcde"u8.ToArray(), isEndStream: true, None);
        diagnostics.Act("bytes sent after WINDOW_UPDATE", sentAfter);
        diagnostics.Assert("bytes sent after WINDOW_UPDATE", 3, sentAfter);
        Assert.AreEqual(3, sentAfter);
        var written = (await peer.WrittenFrames()).Single();
        AssertFrameWithDiagnostics(diagnostics, "DATA frame", CreateData(1, "abc"u8.ToArray(), isEndStream: false), written);
    }

    [TestMethod]
    public async Task WriteDataAsync_EmptyEndOfBody_SendsAnEmptyDataFrameWithEndStream()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect();
        var streamId = await StartStreamAsync(connection, peer);
        diagnostics.Arrange("body", "empty, END_STREAM");

        var sent = await connection.WriteDataAsync(streamId, ReadOnlyMemory<byte>.Empty, isEndStream: true, None);
        diagnostics.Act("bytes sent", sent);
        diagnostics.Bytes("written", peer.Written.ToArray());

        diagnostics.Assert("bytes sent", 0, sent);
        Assert.AreEqual(0, sent);
        diagnostics.Diff("DATA frame bytes", FromHex("000000 00 01 00000001"), peer.Written.ToArray());
        CollectionAssert.AreEqual(FromHex("000000 00 01 00000001"), peer.Written.ToArray());
        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.WriteDataAsync(streamId, new byte[1], isEndStream: true, None));
        diagnostics.Act("WriteDataAsync after END_STREAM", error.GetType().Name);
        diagnostics.Assert("exception type", nameof(InvalidOperationException), error.GetType().Name);
    }

    [TestMethod]
    public void GetAvailableSendWindow_StreamNotOpen_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect();
        diagnostics.Arrange("stream 1", "never opened");

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => connection.GetAvailableSendWindow(1));
        diagnostics.Act("exception", error.GetType().Name);
        diagnostics.Assert("exception type", nameof(InvalidOperationException), error.GetType().Name);
    }

    [TestMethod]
    public async Task IncreaseStreamReceiveWindowAsync_SendsAWindowUpdateAndKeepsTheLargerWindowToppedUp()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect(CreateData(1, new byte[16384], isEndStream: false), CreateData(1, new byte[16384], isEndStream: false));
        diagnostics.Arrange("peer frames", "two DATA frames of 16384 bytes on stream 1");
        await connection.SendPrefaceAsync(None);
        var streamId = connection.OpenStream();
        await connection.IncreaseStreamReceiveWindowAsync(streamId, 65536, None);
        peer.Written.SetLength(0);

        _ = await connection.ReadStreamFrameAsync(None);
        _ = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("bytes written after reading two frames", peer.Written.Length);

        diagnostics.Assert("bytes written", 0L, peer.Written.Length);
        Assert.AreEqual(0, peer.Written.Length, "two 16 KiB frames use less than half of a 128 KiB window");
    }

    [TestMethod]
    public async Task IncreaseStreamReceiveWindowAsync_WritesTheIncrement()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect();
        var streamId = connection.OpenStream();
        diagnostics.Arrange("increment", 10420225);

        await connection.IncreaseStreamReceiveWindowAsync(streamId, 10420225, None);
        diagnostics.Bytes("written", peer.Written.ToArray());
        diagnostics.Act("written length", peer.Written.ToArray().Length);

        diagnostics.Diff("WINDOW_UPDATE bytes", FromHex("000004 08 00 00000001 009f0001"), peer.Written.ToArray());
        CollectionAssert.AreEqual(FromHex("000004 08 00 00000001 009f0001"), peer.Written.ToArray());
    }

    [TestMethod]
    public async Task IncreaseStreamReceiveWindowAsync_InvalidIncrement_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect();
        var streamId = connection.OpenStream();
        diagnostics.Arrange("increments", "0, int.MaxValue on stream 1; 1 on unopened stream 3");

        var zero = await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => connection.IncreaseStreamReceiveWindowAsync(streamId, 0, None));
        diagnostics.Act("increment 0", zero.GetType().Name);
        diagnostics.Assert("increment 0", nameof(ArgumentOutOfRangeException), zero.GetType().Name);
        var large = await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => connection.IncreaseStreamReceiveWindowAsync(streamId, int.MaxValue, None));
        diagnostics.Act("increment int.MaxValue", large.GetType().Name);
        diagnostics.Assert("increment int.MaxValue", nameof(ArgumentOutOfRangeException), large.GetType().Name);
        var unopened = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.IncreaseStreamReceiveWindowAsync(3, 1, None));
        diagnostics.Act("unopened stream 3", unopened.GetType().Name);
        diagnostics.Assert("unopened stream 3", nameof(InvalidOperationException), unopened.GetType().Name);
    }

    [TestMethod]
    public async Task ResetStreamAsync_WritesRstStreamAndIgnoresTheStreamsLaterFrames()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect(
            CreateData(1, "late"u8.ToArray(), isEndStream: false),
            CreateWindowUpdate(1, 5),
            CreateRstStream(1, Http2ErrorCode.Cancel),
            CreateHeaders(1, new byte[] { 0x88 }, isEndStream: true, isEndHeaders: true));
        diagnostics.Arrange("peer frames", "DATA, WINDOW_UPDATE, RST_STREAM, HEADERS on stream 1");
        var streamId = connection.OpenStream();

        await connection.ResetStreamAsync(streamId, Http2ErrorCode.Cancel, None);
        var frame = await connection.ReadStreamFrameAsync(None);
        diagnostics.Bytes("written", peer.Written.ToArray());
        diagnostics.Act("stream frame read", frame);

        diagnostics.Diff("RST_STREAM bytes", FromHex("000004 03 00 00000001 00000008"), peer.Written.ToArray());
        CollectionAssert.AreEqual(FromHex("000004 03 00 00000001 00000008"), peer.Written.ToArray());
        diagnostics.Assert("frame type", Http2FrameType.Headers, frame!.Type);
        Assert.AreEqual(Http2FrameType.Headers, frame!.Type, "a header block is returned for the HPACK decoder even on a reset stream");
        diagnostics.Assert("connection receive window", 65531, connection.ConnectionReceiveWindow.Size);
        Assert.AreEqual(65531, connection.ConnectionReceiveWindow.Size, "DATA on a reset stream still counts against the connection");
    }

    [TestMethod]
    public async Task SendPingAsync_WritesAPing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect();
        diagnostics.Arrange("opaque data", 1);

        await connection.SendPingAsync(1, None);
        diagnostics.Bytes("written", peer.Written.ToArray());
        diagnostics.Act("written length", peer.Written.ToArray().Length);

        diagnostics.Diff("PING bytes", FromHex("000008 06 00 00000000 0000000000000001"), peer.Written.ToArray());
        CollectionAssert.AreEqual(FromHex("000008 06 00 00000000 0000000000000001"), peer.Written.ToArray());
    }

    [TestMethod]
    public async Task SendGoAwayAsync_WritesGoAwayWithLastStreamZero()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect();
        diagnostics.Arrange("error code", Http2ErrorCode.NoError);

        await connection.SendGoAwayAsync(Http2ErrorCode.NoError, None);
        diagnostics.Bytes("written", peer.Written.ToArray());
        diagnostics.Act("IsGoAwaySent", connection.IsGoAwaySent);

        diagnostics.Diff("GOAWAY bytes", FromHex("000008 07 00 00000000 00000000 00000000"), peer.Written.ToArray());
        CollectionAssert.AreEqual(FromHex("000008 07 00 00000000 00000000 00000000"), peer.Written.ToArray());
        diagnostics.Assert("IsGoAwaySent", true, connection.IsGoAwaySent);
        Assert.IsTrue(connection.IsGoAwaySent);
    }

    [TestMethod]
    public async Task SendGoAwayAsync_WithDebugData_WritesItAfterTheErrorCode()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect();
        diagnostics.Arrange("error code", Http2ErrorCode.NoError);
        diagnostics.Arrange("debug data", "shutdown\\0");

        await connection.SendGoAwayAsync(Http2ErrorCode.NoError, "shutdown\0"u8.ToArray(), None);
        diagnostics.Bytes("written", peer.Written.ToArray());
        diagnostics.Act("IsGoAwaySent", connection.IsGoAwaySent);

        diagnostics.Diff("GOAWAY bytes", FromHex("000011 07 00 00000000 00000000 00000000 73687574646f776e00"), peer.Written.ToArray());
        CollectionAssert.AreEqual(FromHex("000011 07 00 00000000 00000000 00000000 73687574646f776e00"), peer.Written.ToArray());
        diagnostics.Assert("IsGoAwaySent", true, connection.IsGoAwaySent);
        Assert.IsTrue(connection.IsGoAwaySent);
    }

    [TestMethod]
    public void IsGoAwaySent_NothingSent_IsFalse()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect();
        diagnostics.Arrange("frames sent", "none");

        var isGoAwaySent = connection.IsGoAwaySent;
        diagnostics.Act("IsGoAwaySent", isGoAwaySent);

        diagnostics.Assert("IsGoAwaySent", false, isGoAwaySent);
        Assert.IsFalse(isGoAwaySent);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_WholeResponseArrivingOneByteAtATime_IsReadFrameByFrame()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var fromPeer = Wire(
            CreateSettings([new(Http2SettingIdentifier.MaxFrameSize, 32768)]),
            CreateSettingsAcknowledgement(),
            CreateHeaders(1, new byte[] { 0x88 }, isEndStream: false, isEndHeaders: true),
            CreateData(1, "hello"u8.ToArray(), isEndStream: false, padLength: 3),
            CreateHeaders(1, new byte[] { 0x40 }, isEndStream: true, isEndHeaders: true));
        diagnostics.Arrange("read size", 1);
        diagnostics.Bytes("wire from peer", fromPeer);
        using var peer = new PeerStream(fromPeer, readSize: 1);
        var connection = new Http2Connection(peer);
        var streamId = connection.OpenStream();

        var headers = await connection.ReadStreamFrameAsync(None);
        var data = await connection.ReadStreamFrameAsync(None);
        var trailers = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("headers", headers);
        diagnostics.Act("data", data);
        diagnostics.Act("trailers", trailers);

        diagnostics.Assert("headers frame", new Http2StreamFrame(Http2FrameType.Headers, streamId, headers!.Content, false), headers);
        Assert.AreEqual(new Http2StreamFrame(Http2FrameType.Headers, streamId, headers!.Content, false), headers);
        diagnostics.Bytes("headers content", headers.Content.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x88 }, headers.Content.ToArray());
        diagnostics.Bytes("data content", data!.Content.ToArray());
        CollectionAssert.AreEqual("hello"u8.ToArray(), data!.Content.ToArray());
        diagnostics.Assert("data IsEndStream", false, data.IsEndStream);
        Assert.IsFalse(data.IsEndStream);
        diagnostics.Assert("trailers IsEndStream", true, trailers!.IsEndStream);
        Assert.IsTrue(trailers!.IsEndStream);
        var afterEnd = await connection.ReadStreamFrameAsync(None);
        diagnostics.Assert("frame after the end", null, afterEnd);
        Assert.IsNull(afterEnd);
        diagnostics.Assert("IsPeerSettingsReceived", true, connection.IsPeerSettingsReceived);
        Assert.IsTrue(connection.IsPeerSettingsReceived);
        diagnostics.Assert("IsClientSettingsAcknowledged", true, connection.IsClientSettingsAcknowledged);
        Assert.IsTrue(connection.IsClientSettingsAcknowledged);
        diagnostics.Assert("peer MaxFrameSize", 32768, connection.PeerSettings.MaxFrameSize);
        Assert.AreEqual(32768, connection.PeerSettings.MaxFrameSize);
        var written = (await peer.WrittenFrames())[0];
        AssertFrameWithDiagnostics(diagnostics, "first frame written", CreateSettingsAcknowledgement(), written);
    }

    [TestMethod]
    public async Task ReadFrameAsync_WindowUpdate_AppliesItAndCompletesNoStreamFrame()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateWindowUpdate(0, 1000), CreateData(1, "x"u8.ToArray(), isEndStream: false));
        diagnostics.Arrange("peer frames", "WINDOW_UPDATE stream 0 +1000, DATA stream 1 'x'");
        _ = connection.OpenStream();

        var afterWindowUpdate = await connection.ReadFrameAsync(None);
        var afterData = await connection.ReadFrameAsync(None);
        diagnostics.Act("after WINDOW_UPDATE", afterWindowUpdate);
        diagnostics.Act("after DATA", afterData);

        diagnostics.Assert("after WINDOW_UPDATE", null, afterWindowUpdate);
        Assert.IsNull(afterWindowUpdate);
        diagnostics.Assert("IsClosedByPeer", false, connection.IsClosedByPeer);
        Assert.IsFalse(connection.IsClosedByPeer);
        diagnostics.Assert("connection send window", Http2Settings.DefaultInitialWindowSize + 1000, connection.ConnectionSendWindow.Size);
        Assert.AreEqual(Http2Settings.DefaultInitialWindowSize + 1000, connection.ConnectionSendWindow.Size);
        diagnostics.Bytes("data content", afterData!.Content.ToArray());
        CollectionAssert.AreEqual("x"u8.ToArray(), afterData!.Content.ToArray());
    }

    [TestMethod]
    public async Task ReadFrameAsync_PeerClosedBetweenFrames_ReturnsNullAndTellsItClosed()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect();
        diagnostics.Arrange("peer frames", "none, then close");

        var frame = await connection.ReadFrameAsync(None);
        diagnostics.Act("frame", frame);
        diagnostics.Act("IsClosedByPeer", connection.IsClosedByPeer);

        diagnostics.Assert("frame", null, frame);
        Assert.IsNull(frame);
        diagnostics.Assert("IsClosedByPeer", true, connection.IsClosedByPeer);
        Assert.IsTrue(connection.IsClosedByPeer);
        var streamFrame = await connection.ReadStreamFrameAsync(None);
        diagnostics.Assert("stream frame after close", null, streamFrame);
        Assert.IsNull(streamFrame);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_DataWithEndStream_EndsTheStream()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateData(1, "x"u8.ToArray(), isEndStream: true), CreateData(1, "y"u8.ToArray(), isEndStream: false));
        diagnostics.Arrange("peer frames", "DATA 'x' END_STREAM, DATA 'y' on stream 1");
        _ = connection.OpenStream();

        var frame = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("first frame", frame);

        diagnostics.Assert("IsEndStream", true, frame!.IsEndStream);
        Assert.IsTrue(frame!.IsEndStream);
        var error = await ProtocolErrorOf(connection);
        diagnostics.Act("error code on the next frame", error);
        diagnostics.Assert("error code", Http2ErrorCode.StreamClosed, error);
        Assert.AreEqual(Http2ErrorCode.StreamClosed, error);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_HalfTheStreamWindowUsed_TopsItUpWithAWindowUpdate()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect(CreateData(1, new byte[16384], isEndStream: false), CreateData(1, new byte[16384], isEndStream: false));
        diagnostics.Arrange("peer frames", "two DATA frames of 16384 bytes on stream 1");
        await connection.SendPrefaceAsync(None);
        _ = connection.OpenStream();
        peer.Written.SetLength(0);

        _ = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("bytes written after one frame", peer.Written.Length);
        diagnostics.Assert("bytes written after one frame", 0L, peer.Written.Length);
        Assert.AreEqual(0, peer.Written.Length);
        _ = await connection.ReadStreamFrameAsync(None);

        var written = (await peer.WrittenFrames()).Single();
        AssertFrameWithDiagnostics(diagnostics, "WINDOW_UPDATE", CreateWindowUpdate(1, 32768), written);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_HalfTheConnectionWindowUsed_TopsItUpWithAWindowUpdate()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect(CreateData(1, new byte[16384], isEndStream: false), CreateData(1, new byte[16384], isEndStream: true));
        diagnostics.Arrange("peer frames", "DATA 16384 bytes, DATA 16384 bytes END_STREAM on stream 1");
        _ = connection.OpenStream();

        _ = await connection.ReadStreamFrameAsync(None);
        _ = await connection.ReadStreamFrameAsync(None);

        var written = (await peer.WrittenFrames()).Single();
        AssertFrameWithDiagnostics(diagnostics, "WINDOW_UPDATE", CreateWindowUpdate(0, 32768), written);
        diagnostics.Assert("connection receive window", 65535, connection.ConnectionReceiveWindow.Size);
        Assert.AreEqual(65535, connection.ConnectionReceiveWindow.Size);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_DataOverrunningTheStreamWindow_IsAFlowControlError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var peer = new PeerStream(Wire(CreateData(1, new byte[11], isEndStream: false)));
        var connection = new Http2Connection(peer, 1, 65535, 10);
        diagnostics.Arrange("peer frames", "DATA 11 bytes on stream 1; stream window 10");
        _ = connection.OpenStream();

        var error = await ProtocolErrorOf(connection);
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.FlowControlError, error);
        Assert.AreEqual(Http2ErrorCode.FlowControlError, error);
        var written = (await peer.WrittenFrames())[^1];
        AssertFrameWithDiagnostics(diagnostics, "last frame written", CreateGoAway(0, Http2ErrorCode.FlowControlError, ReadOnlyMemory<byte>.Empty), written);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_DataOverrunningTheConnectionWindow_IsAFlowControlError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var peer = new PeerStream(Wire(CreateData(1, new byte[11], isEndStream: false)));
        var connection = new Http2Connection(peer, 1, 10, 65536);
        diagnostics.Arrange("peer frames", "DATA 11 bytes on stream 1; connection window 10");
        _ = connection.OpenStream();

        var error = await ProtocolErrorOf(connection);
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.FlowControlError, error);
        Assert.AreEqual(Http2ErrorCode.FlowControlError, error);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_DataOnStreamZero_SendsGoAwayWithProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect(new Http2Frame(Http2FrameType.Data, 0, 0, new byte[1]));
        diagnostics.Arrange("peer frames", "DATA on stream 0, 1 byte");

        var error = await ProtocolErrorOf(connection);
        diagnostics.Act("error code", error);
        diagnostics.Bytes("written", peer.Written.ToArray());

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
        diagnostics.Diff("GOAWAY bytes", FromHex("000008 07 00 00000000 00000000 00000001"), peer.Written.ToArray());
        CollectionAssert.AreEqual(FromHex("000008 07 00 00000000 00000000 00000001"), peer.Written.ToArray());
    }

    [TestMethod]
    [DataRow(3, DisplayName = "an odd stream not yet opened")]
    [DataRow(2, DisplayName = "a server-initiated stream")]
    public async Task ReadStreamFrameAsync_DataOnAnIdleStream_IsAProtocolError(int streamId)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateData(streamId, new byte[1], isEndStream: false));
        diagnostics.Arrange("peer frames", "DATA on idle stream " + streamId);
        _ = connection.OpenStream();

        var error = await ProtocolErrorOf(connection);
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_HeadersWithContinuations_ReturnsTheJoinedBlock()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(
            CreateHeaders(1, new byte[] { 0x88 }, isEndStream: true, isEndHeaders: false),
            CreateContinuation(1, new byte[] { 0x40 }, isEndHeaders: false),
            CreateContinuation(1, new byte[] { 0x41 }, isEndHeaders: true));
        diagnostics.Arrange("peer frames", "HEADERS 88 END_STREAM, CONTINUATION 40, CONTINUATION 41 END_HEADERS");
        _ = connection.OpenStream();

        var frame = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("frame", frame);
        diagnostics.Bytes("joined block", frame!.Content.ToArray());

        CollectionAssert.AreEqual(new byte[] { 0x88, 0x40, 0x41 }, frame!.Content.ToArray());
        diagnostics.Assert("IsEndStream", true, frame.IsEndStream);
        Assert.IsTrue(frame.IsEndStream);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_ContinuedHeadersOnAResetStream_AreStillReturned()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(
            CreateHeaders(1, new byte[] { 0x88 }, isEndStream: false, isEndHeaders: false),
            CreateContinuation(1, new byte[] { 0x40 }, isEndHeaders: true));
        diagnostics.Arrange("peer frames", "HEADERS 88, CONTINUATION 40 END_HEADERS on a reset stream 1");
        var streamId = connection.OpenStream();
        await connection.ResetStreamAsync(streamId, Http2ErrorCode.Cancel, None);

        var frame = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("frame", frame);
        diagnostics.Bytes("joined block", frame!.Content.ToArray());

        diagnostics.Diff("joined block", new byte[] { 0x88, 0x40 }, frame!.Content.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x88, 0x40 }, frame!.Content.ToArray());
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_HeaderBlockInterruptedByAnotherFrame_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateHeaders(1, new byte[] { 0x88 }, isEndStream: false, isEndHeaders: false), CreatePing(0, isAcknowledgement: false));
        diagnostics.Arrange("peer frames", "HEADERS 88 without END_HEADERS, then PING");
        _ = connection.OpenStream();

        var exception = await Assert.ThrowsExactlyAsync<Http2ProtocolException>(() => connection.ReadStreamFrameAsync(None));
        diagnostics.Act("error code", exception.ErrorCode);
        diagnostics.Act("message", exception.Message);

        diagnostics.Assert("message", "HTTP/2 ProtocolError: Ping on stream 0 interrupted stream 1's header block.", exception.Message);
        Assert.AreEqual("HTTP/2 ProtocolError: Ping on stream 0 interrupted stream 1's header block.", exception.Message);
        diagnostics.Assert("IsGoAwaySent", true, connection.IsGoAwaySent);
        Assert.IsTrue(connection.IsGoAwaySent);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_ContinuationOnAnotherStream_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateHeaders(1, new byte[] { 0x88 }, isEndStream: false, isEndHeaders: false), CreateContinuation(3, new byte[] { 0x40 }, isEndHeaders: true));
        diagnostics.Arrange("peer frames", "HEADERS stream 1 without END_HEADERS, CONTINUATION stream 3");
        _ = connection.OpenStream();
        _ = connection.OpenStream();

        var error = await ProtocolErrorOf(connection);
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_FloodOfEmptyContinuations_IsEnhanceYourCalm()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var continuations = Enumerable.Range(0, Http2Connection.MaximumContinuationFrames + 1).Select(_ => CreateContinuation(1, ReadOnlyMemory<byte>.Empty, isEndHeaders: false));
        var (connection, _) = Connect([CreateHeaders(1, new byte[1], isEndStream: false, isEndHeaders: false), .. continuations]);
        diagnostics.Arrange("empty CONTINUATION frames", Http2Connection.MaximumContinuationFrames + 1);
        _ = connection.OpenStream();

        var error = await ProtocolErrorOf(connection);
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.EnhanceYourCalm, error);
        Assert.AreEqual(Http2ErrorCode.EnhanceYourCalm, error);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_ConnectionClosedInAHeaderBlock_ThrowsEndOfStream()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateHeaders(1, new byte[] { 0x88 }, isEndStream: false, isEndHeaders: false));
        diagnostics.Arrange("peer frames", "HEADERS 88 without END_HEADERS, then close");
        _ = connection.OpenStream();

        var error = await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => connection.ReadStreamFrameAsync(None));
        diagnostics.Act("exception", error.GetType().Name);
        diagnostics.Assert("exception type", nameof(EndOfStreamException), error.GetType().Name);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_ContinuationWithoutHeaders_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateContinuation(1, new byte[] { 0x40 }, isEndHeaders: true));
        diagnostics.Arrange("peer frames", "CONTINUATION stream 1 with no HEADERS");
        _ = connection.OpenStream();

        var error = await ProtocolErrorOf(connection);
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_PushPromise_IsAProtocolErrorBecausePushIsDisabled()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreatePushPromise(1, 2, new byte[] { 0x82 }, isEndHeaders: true));
        diagnostics.Arrange("peer frames", "PUSH_PROMISE stream 1 promising stream 2");
        _ = connection.OpenStream();

        var error = await ProtocolErrorOf(connection);
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_HeadersAfterEndStream_IsStreamClosed()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(
            CreateHeaders(1, new byte[] { 0x88 }, isEndStream: true, isEndHeaders: true),
            CreateHeaders(1, new byte[] { 0x88 }, isEndStream: true, isEndHeaders: true));
        diagnostics.Arrange("peer frames", "HEADERS END_STREAM twice on stream 1");
        _ = connection.OpenStream();
        _ = await connection.ReadStreamFrameAsync(None);

        var error = await ProtocolErrorOf(connection);
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.StreamClosed, error);
        Assert.AreEqual(Http2ErrorCode.StreamClosed, error);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_PriorityAndUnknownFrames_AreIgnored()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect(
            CreatePriority(1, new Http2Priority(0, false, 16)),
            new Http2Frame((Http2FrameType)0xfa, 0, 0, new byte[3]),
            CreateData(1, "x"u8.ToArray(), isEndStream: true));
        diagnostics.Arrange("peer frames", "PRIORITY stream 1, unknown type 0xfa, DATA 'x' END_STREAM");
        _ = connection.OpenStream();

        var frame = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("frame", frame);
        diagnostics.Act("bytes written", peer.Written.Length);

        diagnostics.Assert("frame type", Http2FrameType.Data, frame!.Type);
        Assert.AreEqual(Http2FrameType.Data, frame!.Type);
        diagnostics.Assert("bytes written", 0L, peer.Written.Length);
        Assert.AreEqual(0, peer.Written.Length);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_MalformedPriority_IsAFrameSizeError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(new Http2Frame(Http2FrameType.Priority, 0, 1, new byte[4]));
        diagnostics.Arrange("peer frames", "PRIORITY stream 1 with a 4-byte payload");

        var error = await ProtocolErrorOf(connection);
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.FrameSizeError, error);
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, error);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_FrameOverOurMaximumFrameSize_IsAFrameSizeError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect(CreateData(1, new byte[16385], isEndStream: false));
        diagnostics.Arrange("peer frames", "DATA stream 1 with 16385 bytes");
        _ = connection.OpenStream();

        var error = await ProtocolErrorOf(connection);
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.FrameSizeError, error);
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, error);
        var written = (await peer.WrittenFrames()).Single();
        AssertFrameWithDiagnostics(diagnostics, "frame written", CreateGoAway(0, Http2ErrorCode.FrameSizeError, ReadOnlyMemory<byte>.Empty), written);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_RstStreamOnAnOpenStream_ThrowsStreamResetWithoutGoAway()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect(CreateRstStream(1, Http2ErrorCode.RefusedStream), CreateData(1, "x"u8.ToArray(), isEndStream: true));
        diagnostics.Arrange("peer frames", "RST_STREAM RefusedStream stream 1, DATA 'x'");
        var streamId = connection.OpenStream();

        var exception = await Assert.ThrowsExactlyAsync<Http2StreamResetException>(() => connection.ReadStreamFrameAsync(None));
        diagnostics.Act("stream id", exception.StreamId);
        diagnostics.Act("error code", exception.ErrorCode);
        diagnostics.Act("message", exception.Message);

        diagnostics.Assert("stream id", streamId, exception.StreamId);
        Assert.AreEqual(streamId, exception.StreamId);
        diagnostics.Assert("error code", Http2ErrorCode.RefusedStream, exception.ErrorCode);
        Assert.AreEqual(Http2ErrorCode.RefusedStream, exception.ErrorCode);
        diagnostics.Assert("message", "HTTP/2 stream 1 was reset by the peer: RefusedStream.", exception.Message);
        Assert.AreEqual("HTTP/2 stream 1 was reset by the peer: RefusedStream.", exception.Message);
        diagnostics.Assert("bytes written", 0L, peer.Written.Length);
        Assert.AreEqual(0, peer.Written.Length);
        var afterReset = await connection.ReadStreamFrameAsync(None);
        diagnostics.Assert("frame after the reset", null, afterReset);
        Assert.IsNull(afterReset, "DATA on the reset stream is ignored");
        diagnostics.Assert("open stream count", 0, connection.OpenStreamCount);
        Assert.AreEqual(0, connection.OpenStreamCount);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_RstStreamOnAnIdleStream_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateRstStream(1, Http2ErrorCode.Cancel));
        diagnostics.Arrange("peer frames", "RST_STREAM Cancel on idle stream 1");

        var error = await ProtocolErrorOf(connection);
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_InitialWindowSizeChange_AdjustsOpenStreamsSendWindows()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect(CreateSettings([new(Http2SettingIdentifier.InitialWindowSize, 100)]));
        diagnostics.Arrange("peer frames", "SETTINGS InitialWindowSize=100 after 65000 bytes were sent");
        var streamId = await StartStreamAsync(connection, peer);
        _ = await connection.WriteDataAsync(streamId, new byte[65000], isEndStream: false, None);

        _ = await connection.ReadStreamFrameAsync(None);
        var available = connection.GetAvailableSendWindow(streamId);
        diagnostics.Act("connection send window", connection.ConnectionSendWindow.Size);
        diagnostics.Act("available stream window", available);

        diagnostics.Assert("connection send window", 535, connection.ConnectionSendWindow.Size);
        Assert.AreEqual(535, connection.ConnectionSendWindow.Size, "the connection window is not changed by SETTINGS");
        diagnostics.Assert("available stream window", 0, available);
        Assert.AreEqual(0, connection.GetAvailableSendWindow(streamId), "65535 - 65000 + (100 - 65535) is negative");
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_InitialWindowSizeOverflowingAStreamWindow_IsAFlowControlError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateWindowUpdate(1, int.MaxValue - 65535), CreateSettings([new(Http2SettingIdentifier.InitialWindowSize, 65536)]));
        diagnostics.Arrange("peer frames", "WINDOW_UPDATE stream 1 to the maximum, then SETTINGS InitialWindowSize=65536");
        _ = connection.OpenStream();

        var error = await ProtocolErrorOf(connection);
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.FlowControlError, error);
        Assert.AreEqual(Http2ErrorCode.FlowControlError, error);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_InvalidSetting_SendsGoAwayWithProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect(CreateSettings([new(Http2SettingIdentifier.EnablePush, 2)]));
        diagnostics.Arrange("peer frames", "SETTINGS EnablePush=2");

        var error = await ProtocolErrorOf(connection);
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
        var written = (await peer.WrittenFrames()).Single();
        AssertFrameWithDiagnostics(diagnostics, "frame written", CreateGoAway(0, Http2ErrorCode.ProtocolError, ReadOnlyMemory<byte>.Empty), written);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_SettingsOfMoreThan32Entries_SendsGoAwayWithEnhanceYourCalmAndNoAcknowledgement()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var settings = Enumerable.Repeat(new Http2Setting(Http2SettingIdentifier.InitialWindowSize, 1000), Http2Connection.MaximumSettingsEntries + 1).ToArray();
        var (connection, peer) = Connect(CreateSettings(settings));
        diagnostics.Arrange("settings entries", settings.Length);

        diagnostics.Assert("SETTINGS payload length", 198, settings.Length * 6);
        Assert.AreEqual(198, settings.Length * 6);
        var error = await ProtocolErrorOf(connection);
        diagnostics.Act("error code", error);
        diagnostics.Assert("error code", Http2ErrorCode.EnhanceYourCalm, error);
        Assert.AreEqual(Http2ErrorCode.EnhanceYourCalm, error);
        var written = (await peer.WrittenFrames()).Single();
        AssertFrameWithDiagnostics(diagnostics, "frame written", CreateGoAway(0, Http2ErrorCode.EnhanceYourCalm, ReadOnlyMemory<byte>.Empty), written);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_SettingsOfMoreThan32Entries_AppliesNoneOfThem()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var settings = Enumerable.Repeat(new Http2Setting(Http2SettingIdentifier.InitialWindowSize, 1000), Http2Connection.MaximumSettingsEntries + 1).ToArray();
        var (connection, _) = Connect(CreateSettings(settings));
        diagnostics.Arrange("settings entries", settings.Length);

        var exception = await Assert.ThrowsExactlyAsync<Http2ProtocolException>(() => connection.ReadStreamFrameAsync(None));
        diagnostics.Act("error code", exception.ErrorCode);
        diagnostics.Act("message", exception.Message);

        diagnostics.Assert("message contains", "SETTINGS: too many setting entries", exception.Message);
        StringAssert.Contains(exception.Message, "SETTINGS: too many setting entries");
        diagnostics.Assert("peer InitialWindowSize", Http2Settings.DefaultInitialWindowSize, connection.PeerSettings.InitialWindowSize);
        Assert.AreEqual(Http2Settings.DefaultInitialWindowSize, connection.PeerSettings.InitialWindowSize);
        diagnostics.Assert("IsPeerSettingsReceived", false, connection.IsPeerSettingsReceived);
        Assert.IsFalse(connection.IsPeerSettingsReceived);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_SettingsOfExactly32Entries_IsAppliedAndAcknowledged()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var settings = Enumerable.Repeat(new Http2Setting(Http2SettingIdentifier.InitialWindowSize, 1000), Http2Connection.MaximumSettingsEntries).ToArray();
        var (connection, peer) = Connect(CreateSettings(settings));
        diagnostics.Arrange("settings entries", settings.Length);

        var frame = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("stream frame", frame);

        diagnostics.Assert("stream frame", null, frame);
        Assert.IsNull(frame);
        diagnostics.Assert("IsPeerSettingsReceived", true, connection.IsPeerSettingsReceived);
        Assert.IsTrue(connection.IsPeerSettingsReceived);
        diagnostics.Assert("peer InitialWindowSize", 1000, connection.PeerSettings.InitialWindowSize);
        Assert.AreEqual(1000, connection.PeerSettings.InitialWindowSize);
        var written = (await peer.WrittenFrames()).Single();
        AssertFrameWithDiagnostics(diagnostics, "frame written", CreateSettingsAcknowledgement(), written);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_Ping_IsAnsweredWithTheSameOpaqueData()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect(CreatePing(0xdeadbeef, isAcknowledgement: false), CreatePing(0x1, isAcknowledgement: true));
        diagnostics.Arrange("peer frames", "PING 0xdeadbeef, PING 0x1 ACK");

        var frame = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("stream frame", frame);

        diagnostics.Assert("stream frame", null, frame);
        Assert.IsNull(frame);
        var written = (await peer.WrittenFrames()).Single();
        AssertFrameWithDiagnostics(diagnostics, "PING answer", CreatePing(0xdeadbeef, isAcknowledgement: true), written);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_GoAwayWithAnError_ThrowsGoAway()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateGoAway(0, Http2ErrorCode.EnhanceYourCalm, "slow down"u8.ToArray()));
        diagnostics.Arrange("peer frames", "GOAWAY last stream 0 EnhanceYourCalm 'slow down'");

        var exception = await Assert.ThrowsExactlyAsync<Http2GoAwayException>(() => connection.ReadStreamFrameAsync(None));
        diagnostics.Act("error code", exception.GoAway.ErrorCode);
        diagnostics.Act("message", exception.Message);

        diagnostics.Assert("error code", Http2ErrorCode.EnhanceYourCalm, exception.GoAway.ErrorCode);
        Assert.AreEqual(Http2ErrorCode.EnhanceYourCalm, exception.GoAway.ErrorCode);
        diagnostics.Assert("message", "HTTP/2 GOAWAY from the peer: EnhanceYourCalm, last stream 0.", exception.Message);
        Assert.AreEqual("HTTP/2 GOAWAY from the peer: EnhanceYourCalm, last stream 0.", exception.Message);
        Assert.AreSame(exception.GoAway, connection.PeerGoAway);
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => connection.OpenStream());
        diagnostics.Act("OpenStream after GOAWAY", error.GetType().Name);
        diagnostics.Assert("exception type", nameof(InvalidOperationException), error.GetType().Name);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_GracefulGoAwayLeavingAnOpenStreamUnprocessed_ThrowsGoAway()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateGoAway(1, Http2ErrorCode.NoError, ReadOnlyMemory<byte>.Empty));
        diagnostics.Arrange("peer frames", "GOAWAY last stream 1 NoError; streams 1 and 3 open");
        _ = connection.OpenStream();
        _ = connection.OpenStream();

        var exception = await Assert.ThrowsExactlyAsync<Http2GoAwayException>(() => connection.ReadStreamFrameAsync(None));
        diagnostics.Act("last stream id", exception.GoAway.LastStreamId);

        diagnostics.Assert("last stream id", 1, exception.GoAway.LastStreamId);
        Assert.AreEqual(1, exception.GoAway.LastStreamId);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_GracefulGoAwayCoveringEveryOpenStream_LetsThemFinish()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(
            CreateHeaders(3, new byte[] { 0x88 }, isEndStream: true, isEndHeaders: true),
            CreateGoAway(1, Http2ErrorCode.NoError, ReadOnlyMemory<byte>.Empty),
            CreateData(1, "x"u8.ToArray(), isEndStream: true));
        diagnostics.Arrange("peer frames", "HEADERS stream 3 END_STREAM, GOAWAY last stream 1, DATA stream 1 END_STREAM");
        _ = connection.OpenStream();
        var closedStreamId = connection.OpenStream();
        await connection.WriteHeadersAsync(closedStreamId, new byte[] { 0x82 }, isEndStream: true, None);
        _ = await connection.ReadStreamFrameAsync(None);

        var frame = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("frame", frame);

        diagnostics.Assert("frame stream id", 1, frame!.StreamId);
        Assert.AreEqual(1, frame!.StreamId);
        diagnostics.Assert("PeerGoAway is set", true, connection.PeerGoAway is not null);
        Assert.IsNotNull(connection.PeerGoAway);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_WindowUpdates_GrowTheSendWindows()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateWindowUpdate(0, 100), CreateWindowUpdate(1, 200));
        diagnostics.Arrange("peer frames", "WINDOW_UPDATE stream 0 +100, WINDOW_UPDATE stream 1 +200");
        var streamId = connection.OpenStream();

        _ = await connection.ReadStreamFrameAsync(None);
        var available = connection.GetAvailableSendWindow(streamId);
        diagnostics.Act("connection send window", connection.ConnectionSendWindow.Size);
        diagnostics.Act("available stream window", available);

        diagnostics.Assert("connection send window", 65635, connection.ConnectionSendWindow.Size);
        Assert.AreEqual(65635, connection.ConnectionSendWindow.Size);
        diagnostics.Assert("available stream window", 65635, available);
        Assert.AreEqual(65635, connection.GetAvailableSendWindow(streamId));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_WindowUpdateOnAResetStream_IsIgnored()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateWindowUpdate(1, 100));
        diagnostics.Arrange("peer frames", "WINDOW_UPDATE stream 1 +100 on a reset stream");
        var streamId = connection.OpenStream();
        await connection.ResetStreamAsync(streamId, Http2ErrorCode.Cancel, None);

        var frame = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("stream frame", frame);

        diagnostics.Assert("stream frame", null, frame);
        Assert.IsNull(frame);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_WindowUpdateOnAnIdleStream_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateWindowUpdate(1, 100));
        diagnostics.Arrange("peer frames", "WINDOW_UPDATE stream 1 +100 on an idle stream");

        var error = await ProtocolErrorOf(connection);
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_WindowUpdateOverflowingTheConnectionWindow_IsAFlowControlError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateWindowUpdate(0, int.MaxValue));
        diagnostics.Arrange("peer frames", "WINDOW_UPDATE stream 0 +int.MaxValue");

        var exception = await Assert.ThrowsExactlyAsync<Http2ProtocolException>(() => connection.ReadStreamFrameAsync(None));
        diagnostics.Act("error code", exception.ErrorCode);
        diagnostics.Act("message", exception.Message);

        diagnostics.Assert("error code", Http2ErrorCode.FlowControlError, exception.ErrorCode);
        Assert.AreEqual(Http2ErrorCode.FlowControlError, exception.ErrorCode);
        diagnostics.Assert("message", "HTTP/2 FlowControlError: WINDOW_UPDATE grew stream 0's window past 2^31 - 1.", exception.Message);
        Assert.AreEqual("HTTP/2 FlowControlError: WINDOW_UPDATE grew stream 0's window past 2^31 - 1.", exception.Message);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_WindowUpdateOverflowingAStreamWindow_IsAFlowControlError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateWindowUpdate(1, int.MaxValue));
        diagnostics.Arrange("peer frames", "WINDOW_UPDATE stream 1 +int.MaxValue");
        _ = connection.OpenStream();

        var error = await ProtocolErrorOf(connection);
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.FlowControlError, error);
        Assert.AreEqual(Http2ErrorCode.FlowControlError, error);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_EightContinuations_AreAccepted()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var continuations = Enumerable.Range(0, Http2Connection.MaximumContinuationFrames).Select(index => CreateContinuation(1, new byte[] { 0x40 }, index == Http2Connection.MaximumContinuationFrames - 1));
        var (connection, _) = Connect([CreateHeaders(1, new byte[] { 0x88 }, isEndStream: false, isEndHeaders: false), .. continuations]);
        diagnostics.Arrange("CONTINUATION frames", Http2Connection.MaximumContinuationFrames);
        _ = connection.OpenStream();

        var frame = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("frame", frame);
        diagnostics.Bytes("joined block", frame!.Content.ToArray());

        diagnostics.Assert("joined block length", 9, frame!.Content.Length);
        Assert.AreEqual(9, frame!.Content.Length);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_RstStreamNoErrorAfterTheResponseEnded_KeepsTheResponse()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect(
            CreateHeaders(1, new byte[] { 0x88 }, isEndStream: true, isEndHeaders: true),
            CreateRstStream(1, Http2ErrorCode.NoError));
        diagnostics.Arrange("peer frames", "HEADERS END_STREAM stream 1, RST_STREAM NoError stream 1");
        _ = await StartStreamAsync(connection, peer);

        var response = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("response", response);

        diagnostics.Assert("response IsEndStream", true, response!.IsEndStream);
        Assert.IsTrue(response!.IsEndStream);
        var afterReset = await connection.ReadStreamFrameAsync(None);
        diagnostics.Assert("frame after the reset", null, afterReset);
        Assert.IsNull(afterReset);
        diagnostics.Assert("open stream count", 0, connection.OpenStreamCount);
        Assert.AreEqual(0, connection.OpenStreamCount);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_RstStreamNoErrorBeforeTheResponseEnded_ThrowsStreamReset()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateRstStream(1, Http2ErrorCode.NoError));
        diagnostics.Arrange("peer frames", "RST_STREAM NoError stream 1 before the response ended");
        _ = connection.OpenStream();

        var exception = await Assert.ThrowsExactlyAsync<Http2StreamResetException>(() => connection.ReadStreamFrameAsync(None));
        diagnostics.Act("error code", exception.ErrorCode);

        diagnostics.Assert("error code", Http2ErrorCode.NoError, exception.ErrorCode);
        Assert.AreEqual(Http2ErrorCode.NoError, exception.ErrorCode);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_ServerEnablingPush_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateSettings([new(Http2SettingIdentifier.EnablePush, 1)]));
        diagnostics.Arrange("peer frames", "SETTINGS EnablePush=1");

        var error = await ProtocolErrorOf(connection);
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_AfterAProtocolError_EveryCallThrows()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateContinuation(1, new byte[] { 0x40 }, isEndHeaders: true));
        diagnostics.Arrange("peer frames", "CONTINUATION stream 1 with no HEADERS");
        var streamId = connection.OpenStream();
        var protocolError = await Assert.ThrowsExactlyAsync<Http2ProtocolException>(() => connection.ReadStreamFrameAsync(None));
        diagnostics.Act("first error code", protocolError.ErrorCode);

        var failure = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.ReadStreamFrameAsync(None));
        diagnostics.Act("second call failure", failure.GetType().Name);

        diagnostics.Assert("inner exception is the protocol error", true, ReferenceEquals(protocolError, failure.InnerException));
        Assert.AreSame(protocolError, failure.InnerException);
        var openError = Assert.ThrowsExactly<InvalidOperationException>(() => connection.OpenStream());
        diagnostics.Assert("OpenStream exception type", nameof(InvalidOperationException), openError.GetType().Name);
        var writeError = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.WriteHeadersAsync(streamId, new byte[] { 0x82 }, isEndStream: true, None));
        diagnostics.Assert("WriteHeadersAsync exception type", nameof(InvalidOperationException), writeError.GetType().Name);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_GoAwayCannotBeWritten_StillThrowsTheProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var peer = new PeerStream(Wire(CreateContinuation(1, new byte[] { 0x40 }, isEndHeaders: true)), failWrites: true);
        var connection = new Http2Connection(peer);
        diagnostics.Arrange("peer", "CONTINUATION with no HEADERS; every write fails");

        var exception = await Assert.ThrowsExactlyAsync<Http2ProtocolException>(() => connection.ReadStreamFrameAsync(None));
        diagnostics.Act("error code", exception.ErrorCode);

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, exception.ErrorCode);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, exception.ErrorCode);
    }

    [TestMethod]
    public async Task WriteDataAsync_BeforeHeaders_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect();
        var streamId = connection.OpenStream();
        diagnostics.Arrange("stream", "opened, no HEADERS sent yet");

        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.WriteDataAsync(streamId, new byte[1], isEndStream: true, None));
        diagnostics.Act("exception", error.GetType().Name);
        diagnostics.Assert("exception type", nameof(InvalidOperationException), error.GetType().Name);
    }

    [TestMethod]
    public async Task WriteHeadersAsync_StreamStartedOutOfOrder_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect();
        var first = connection.OpenStream();
        var second = connection.OpenStream();
        diagnostics.Arrange("streams", string.Join(", ", first, second));
        await connection.WriteHeadersAsync(second, new byte[] { 0x82 }, isEndStream: false, None);

        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.WriteHeadersAsync(first, new byte[] { 0x82 }, isEndStream: false, None));
        diagnostics.Act("HEADERS on the older stream", error.GetType().Name);
        diagnostics.Assert("exception type", nameof(InvalidOperationException), error.GetType().Name);
        await connection.WriteHeadersAsync(second, new byte[] { 0x40 }, isEndStream: true, None);
    }

    [TestMethod]
    public async Task WriteDataAsync_WriteFails_LeavesTheWindowsAndStreamUnchanged()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var peer = new PeerStream([], failWrites: true);
        var connection = new Http2Connection(peer);
        var streamId = connection.OpenStream();
        diagnostics.Arrange("peer", "every write fails");
        var headersError = await Assert.ThrowsExactlyAsync<IOException>(() => connection.WriteHeadersAsync(streamId, new byte[] { 0x82 }, isEndStream: false, None));
        diagnostics.Act("WriteHeadersAsync", headersError.GetType().Name);

        var dataError = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.WriteDataAsync(streamId, new byte[5], isEndStream: true, None));
        diagnostics.Act("WriteDataAsync", dataError.GetType().Name);
        diagnostics.Act("connection send window", connection.ConnectionSendWindow.Size);
        diagnostics.Act("open stream count", connection.OpenStreamCount);

        diagnostics.Assert("connection send window", 65535, connection.ConnectionSendWindow.Size);
        Assert.AreEqual(65535, connection.ConnectionSendWindow.Size);
        diagnostics.Assert("open stream count", 1, connection.OpenStreamCount);
        Assert.AreEqual(1, connection.OpenStreamCount);
    }

    [TestMethod]
    public async Task ResetStreamAsync_IdleStream_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect();
        diagnostics.Arrange("stream 1", "idle");

        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.ResetStreamAsync(1, Http2ErrorCode.Cancel, None));
        diagnostics.Act("exception", error.GetType().Name);
        diagnostics.Act("bytes written", peer.Written.Length);

        diagnostics.Assert("exception type", nameof(InvalidOperationException), error.GetType().Name);
        diagnostics.Assert("bytes written", 0L, peer.Written.Length);
        Assert.AreEqual(0, peer.Written.Length);
    }

    private static string Describe(Http2Frame frame) =>
        $"{frame.Type} flags=0x{frame.Flags:x2} stream={frame.StreamId} payload={frame.Payload.Length}";

    private static void AssertFrameWithDiagnostics(TestDiagnostics diagnostics, string label, Http2Frame expected, Http2Frame actual)
    {
        diagnostics.Act(label + " (actual)", Describe(actual));
        diagnostics.Bytes(label + " payload", actual.Payload.Span);
        diagnostics.Assert(label, Describe(expected), Describe(actual));
        AssertFrame(expected, actual);
    }

    private static async Task<int> StartStreamAsync(Http2Connection connection, PeerStream peer)
    {
        var streamId = connection.OpenStream();
        await connection.WriteHeadersAsync(streamId, new byte[] { 0x82 }, isEndStream: false, None);
        peer.Written.SetLength(0);
        return streamId;
    }

    private static (Http2Connection Connection, PeerStream Peer) Connect(params Http2Frame[] fromPeer)
    {
        var peer = new PeerStream(Wire(fromPeer));
        return (new Http2Connection(peer), peer);
    }

    private static async Task<Http2StreamFrame?> ReadUntilEnd(Http2Connection connection)
    {
        Http2StreamFrame? last = null;
        while (await connection.ReadStreamFrameAsync(None) is { } frame)
        {
            last = frame;
        }

        return last;
    }

    private static async Task<Http2ErrorCode> ProtocolErrorOf(Http2Connection connection) =>
        (await Assert.ThrowsExactlyAsync<Http2ProtocolException>(() => connection.ReadStreamFrameAsync(None))).ErrorCode;
}
