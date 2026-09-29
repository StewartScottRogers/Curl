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

    [TestMethod]
    public async Task SendPrefaceAsync_WritesThePrefaceSettingsAndWindowUpdateCurlSends()
    {
        var (connection, peer) = Connect();

        await connection.SendPrefaceAsync(None);

        CollectionAssert.AreEqual(FromHex(MeasuredPreface), peer.Written.ToArray());
        Assert.AreEqual(1048576000, connection.ConnectionReceiveWindow.Size);
        CollectionAssert.AreEqual("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8.ToArray(), Http2Connection.ClientPreface.ToArray());
    }

    [TestMethod]
    public void New_NullStream_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http2Connection(null!));

    [TestMethod]
    public void OpenStream_AllocatesOddIdentifiersInOrder()
    {
        var (connection, _) = Connect();

        Assert.AreEqual(1, connection.OpenStream());
        Assert.AreEqual(3, connection.OpenStream());
        Assert.AreEqual(5, connection.OpenStream());
        Assert.AreEqual(3, connection.OpenStreamCount);
    }

    [TestMethod]
    public void OpenStream_IdentifiersExhausted_Throws()
    {
        using var peer = new PeerStream([]);
        var connection = new Http2Connection(peer, int.MaxValue, 65535, 65536);

        Assert.AreEqual(int.MaxValue, connection.OpenStream());
        Assert.ThrowsExactly<InvalidOperationException>(() => connection.OpenStream());
    }

    [TestMethod]
    public async Task OpenStream_PeerConcurrencyLimitReached_ThrowsUntilAStreamCloses()
    {
        var (connection, _) = Connect(
            CreateSettings([new(Http2SettingIdentifier.MaxConcurrentStreams, 1)]),
            CreateHeaders(1, new byte[] { 0x88 }, isEndStream: true, isEndHeaders: true));
        var streamId = connection.OpenStream();
        await connection.WriteHeadersAsync(streamId, new byte[] { 0x82 }, isEndStream: false, None);
        _ = await connection.ReadStreamFrameAsync(None);

        Assert.ThrowsExactly<InvalidOperationException>(() => connection.OpenStream());

        _ = await connection.WriteDataAsync(streamId, ReadOnlyMemory<byte>.Empty, isEndStream: true, None);
        Assert.AreEqual(3, connection.OpenStream());
    }

    [TestMethod]
    public async Task WriteHeadersAsync_SmallBlock_SendsOneHeadersFrame()
    {
        var (connection, peer) = Connect();
        var streamId = connection.OpenStream();

        await connection.WriteHeadersAsync(streamId, new byte[] { 0x82, 0x86 }, isEndStream: true, None);

        CollectionAssert.AreEqual(FromHex("000002 01 05 00000001 8286"), peer.Written.ToArray());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.WriteHeadersAsync(streamId, new byte[] { 0x82 }, isEndStream: true, None));
    }

    [TestMethod]
    public async Task WriteHeadersAsync_BlockLargerThanAFrame_ContinuesItInContinuationFrames()
    {
        var (connection, peer) = Connect();
        var streamId = connection.OpenStream();
        var block = Enumerable.Range(0, 40000).Select(index => (byte)index).ToArray();

        await connection.WriteHeadersAsync(streamId, block, isEndStream: false, None);

        var frames = await peer.WrittenFrames();
        AssertFrame(CreateHeaders(1, block[..16384], isEndStream: false, isEndHeaders: false), frames[0]);
        AssertFrame(CreateContinuation(1, block[16384..32768], isEndHeaders: false), frames[1]);
        AssertFrame(CreateContinuation(1, block[32768..], isEndHeaders: true), frames[2]);
        Assert.HasCount(3, frames);
    }

    [TestMethod]
    public async Task WriteHeadersAsync_StreamNotOpen_Throws()
    {
        var (connection, _) = Connect();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.WriteHeadersAsync(1, new byte[] { 0x82 }, isEndStream: true, None));
    }

    [TestMethod]
    public async Task WriteDataAsync_BodyWithinTheWindows_SendsItAndEndsTheStream()
    {
        var (connection, peer) = Connect();
        var streamId = await StartStreamAsync(connection, peer);

        var sent = await connection.WriteDataAsync(streamId, "body"u8.ToArray(), isEndStream: true, None);

        Assert.AreEqual(4, sent);
        CollectionAssert.AreEqual(FromHex("000004 00 01 00000001 626f6479"), peer.Written.ToArray());
        Assert.AreEqual(65531, connection.ConnectionSendWindow.Size);
        Assert.ThrowsExactly<InvalidOperationException>(() => connection.GetAvailableSendWindow(3));
    }

    [TestMethod]
    public async Task WriteDataAsync_BodyLargerThanAFrame_SplitsItAtThePeersMaximumFrameSize()
    {
        var (connection, peer) = Connect();
        var streamId = await StartStreamAsync(connection, peer);

        var sent = await connection.WriteDataAsync(streamId, new byte[20000], isEndStream: false, None);

        var frames = await peer.WrittenFrames();
        Assert.AreEqual(20000, sent);
        AssertFrame(CreateData(1, new byte[16384], isEndStream: false), frames[0]);
        AssertFrame(CreateData(1, new byte[3616], isEndStream: false), frames[1]);
    }

    [TestMethod]
    public async Task WriteDataAsync_BodyLargerThanTheWindow_SendsOnlyTheWindowWithoutEndStream()
    {
        var (connection, peer) = Connect(
            CreateSettings([new(Http2SettingIdentifier.InitialWindowSize, 10), new(Http2SettingIdentifier.MaxFrameSize, 20000)]),
            CreateWindowUpdate(1, 5));
        var streamId = await StartStreamAsync(connection, peer);
        _ = await ReadUntilEnd(connection);

        var sent = await connection.WriteDataAsync(streamId, new byte[20], isEndStream: true, None);

        var frames = await peer.WrittenFrames();
        Assert.AreEqual(15, sent);
        AssertFrame(CreateData(1, new byte[15], isEndStream: false), frames[^1]);
        Assert.AreEqual(0, connection.GetAvailableSendWindow(streamId));
    }

    [TestMethod]
    public async Task WriteDataAsync_WindowExhausted_SendsNothingUntilAWindowUpdate()
    {
        var (connection, peer) = Connect(
            CreateSettings([new(Http2SettingIdentifier.InitialWindowSize, 0)]),
            CreateHeaders(1, new byte[] { 0x88 }, isEndStream: false, isEndHeaders: true),
            CreateWindowUpdate(1, 3));
        var streamId = await StartStreamAsync(connection, peer);
        _ = await connection.ReadStreamFrameAsync(None);
        peer.Written.SetLength(0);

        Assert.AreEqual(0, await connection.WriteDataAsync(streamId, "abcde"u8.ToArray(), isEndStream: true, None));
        Assert.AreEqual(0, peer.Written.Length);

        Assert.IsNull(await connection.ReadStreamFrameAsync(None));
        Assert.AreEqual(3, await connection.WriteDataAsync(streamId, "abcde"u8.ToArray(), isEndStream: true, None));
        AssertFrame(CreateData(1, "abc"u8.ToArray(), isEndStream: false), (await peer.WrittenFrames()).Single());
    }

    [TestMethod]
    public async Task WriteDataAsync_EmptyEndOfBody_SendsAnEmptyDataFrameWithEndStream()
    {
        var (connection, peer) = Connect();
        var streamId = await StartStreamAsync(connection, peer);

        var sent = await connection.WriteDataAsync(streamId, ReadOnlyMemory<byte>.Empty, isEndStream: true, None);

        Assert.AreEqual(0, sent);
        CollectionAssert.AreEqual(FromHex("000000 00 01 00000001"), peer.Written.ToArray());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.WriteDataAsync(streamId, new byte[1], isEndStream: true, None));
    }

    [TestMethod]
    public void GetAvailableSendWindow_StreamNotOpen_Throws()
    {
        var (connection, _) = Connect();

        Assert.ThrowsExactly<InvalidOperationException>(() => connection.GetAvailableSendWindow(1));
    }

    [TestMethod]
    public async Task IncreaseStreamReceiveWindowAsync_SendsAWindowUpdateAndKeepsTheLargerWindowToppedUp()
    {
        var (connection, peer) = Connect(CreateData(1, new byte[16384], isEndStream: false), CreateData(1, new byte[16384], isEndStream: false));
        await connection.SendPrefaceAsync(None);
        var streamId = connection.OpenStream();
        await connection.IncreaseStreamReceiveWindowAsync(streamId, 65536, None);
        peer.Written.SetLength(0);

        _ = await connection.ReadStreamFrameAsync(None);
        _ = await connection.ReadStreamFrameAsync(None);

        Assert.AreEqual(0, peer.Written.Length, "two 16 KiB frames use less than half of a 128 KiB window");
    }

    [TestMethod]
    public async Task IncreaseStreamReceiveWindowAsync_WritesTheIncrement()
    {
        var (connection, peer) = Connect();
        var streamId = connection.OpenStream();

        await connection.IncreaseStreamReceiveWindowAsync(streamId, 10420225, None);

        CollectionAssert.AreEqual(FromHex("000004 08 00 00000001 009f0001"), peer.Written.ToArray());
    }

    [TestMethod]
    public async Task IncreaseStreamReceiveWindowAsync_InvalidIncrement_Throws()
    {
        var (connection, _) = Connect();
        var streamId = connection.OpenStream();

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => connection.IncreaseStreamReceiveWindowAsync(streamId, 0, None));
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => connection.IncreaseStreamReceiveWindowAsync(streamId, int.MaxValue, None));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.IncreaseStreamReceiveWindowAsync(3, 1, None));
    }

    [TestMethod]
    public async Task ResetStreamAsync_WritesRstStreamAndIgnoresTheStreamsLaterFrames()
    {
        var (connection, peer) = Connect(
            CreateData(1, "late"u8.ToArray(), isEndStream: false),
            CreateWindowUpdate(1, 5),
            CreateRstStream(1, Http2ErrorCode.Cancel),
            CreateHeaders(1, new byte[] { 0x88 }, isEndStream: true, isEndHeaders: true));
        var streamId = connection.OpenStream();

        await connection.ResetStreamAsync(streamId, Http2ErrorCode.Cancel, None);
        var frame = await connection.ReadStreamFrameAsync(None);

        CollectionAssert.AreEqual(FromHex("000004 03 00 00000001 00000008"), peer.Written.ToArray());
        Assert.AreEqual(Http2FrameType.Headers, frame!.Type, "a header block is returned for the HPACK decoder even on a reset stream");
        Assert.AreEqual(65531, connection.ConnectionReceiveWindow.Size, "DATA on a reset stream still counts against the connection");
    }

    [TestMethod]
    public async Task SendPingAsync_WritesAPing()
    {
        var (connection, peer) = Connect();

        await connection.SendPingAsync(1, None);

        CollectionAssert.AreEqual(FromHex("000008 06 00 00000000 0000000000000001"), peer.Written.ToArray());
    }

    [TestMethod]
    public async Task SendGoAwayAsync_WritesGoAwayWithLastStreamZero()
    {
        var (connection, peer) = Connect();

        await connection.SendGoAwayAsync(Http2ErrorCode.NoError, None);

        CollectionAssert.AreEqual(FromHex("000008 07 00 00000000 00000000 00000000"), peer.Written.ToArray());
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_WholeResponseArrivingOneByteAtATime_IsReadFrameByFrame()
    {
        var fromPeer = Wire(
            CreateSettings([new(Http2SettingIdentifier.MaxFrameSize, 32768)]),
            CreateSettingsAcknowledgement(),
            CreateHeaders(1, new byte[] { 0x88 }, isEndStream: false, isEndHeaders: true),
            CreateData(1, "hello"u8.ToArray(), isEndStream: false, padLength: 3),
            CreateHeaders(1, new byte[] { 0x40 }, isEndStream: true, isEndHeaders: true));
        using var peer = new PeerStream(fromPeer, readSize: 1);
        var connection = new Http2Connection(peer);
        var streamId = connection.OpenStream();

        var headers = await connection.ReadStreamFrameAsync(None);
        var data = await connection.ReadStreamFrameAsync(None);
        var trailers = await connection.ReadStreamFrameAsync(None);

        Assert.AreEqual(new Http2StreamFrame(Http2FrameType.Headers, streamId, headers!.Content, false), headers);
        CollectionAssert.AreEqual(new byte[] { 0x88 }, headers.Content.ToArray());
        CollectionAssert.AreEqual("hello"u8.ToArray(), data!.Content.ToArray());
        Assert.IsFalse(data.IsEndStream);
        Assert.IsTrue(trailers!.IsEndStream);
        Assert.IsNull(await connection.ReadStreamFrameAsync(None));
        Assert.IsTrue(connection.IsPeerSettingsReceived);
        Assert.IsTrue(connection.IsClientSettingsAcknowledged);
        Assert.AreEqual(32768, connection.PeerSettings.MaxFrameSize);
        AssertFrame(CreateSettingsAcknowledgement(), (await peer.WrittenFrames())[0]);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_DataWithEndStream_EndsTheStream()
    {
        var (connection, _) = Connect(CreateData(1, "x"u8.ToArray(), isEndStream: true), CreateData(1, "y"u8.ToArray(), isEndStream: false));
        _ = connection.OpenStream();

        var frame = await connection.ReadStreamFrameAsync(None);

        Assert.IsTrue(frame!.IsEndStream);
        Assert.AreEqual(Http2ErrorCode.StreamClosed, await ProtocolErrorOf(connection));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_HalfTheStreamWindowUsed_TopsItUpWithAWindowUpdate()
    {
        var (connection, peer) = Connect(CreateData(1, new byte[16384], isEndStream: false), CreateData(1, new byte[16384], isEndStream: false));
        await connection.SendPrefaceAsync(None);
        _ = connection.OpenStream();
        peer.Written.SetLength(0);

        _ = await connection.ReadStreamFrameAsync(None);
        Assert.AreEqual(0, peer.Written.Length);
        _ = await connection.ReadStreamFrameAsync(None);

        AssertFrame(CreateWindowUpdate(1, 32768), (await peer.WrittenFrames()).Single());
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_HalfTheConnectionWindowUsed_TopsItUpWithAWindowUpdate()
    {
        var (connection, peer) = Connect(CreateData(1, new byte[16384], isEndStream: false), CreateData(1, new byte[16384], isEndStream: true));
        _ = connection.OpenStream();

        _ = await connection.ReadStreamFrameAsync(None);
        _ = await connection.ReadStreamFrameAsync(None);

        AssertFrame(CreateWindowUpdate(0, 32768), (await peer.WrittenFrames()).Single());
        Assert.AreEqual(65535, connection.ConnectionReceiveWindow.Size);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_DataOverrunningTheStreamWindow_IsAFlowControlError()
    {
        using var peer = new PeerStream(Wire(CreateData(1, new byte[11], isEndStream: false)));
        var connection = new Http2Connection(peer, 1, 65535, 10);
        _ = connection.OpenStream();

        Assert.AreEqual(Http2ErrorCode.FlowControlError, await ProtocolErrorOf(connection));
        AssertFrame(CreateGoAway(0, Http2ErrorCode.FlowControlError, ReadOnlyMemory<byte>.Empty), (await peer.WrittenFrames())[^1]);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_DataOverrunningTheConnectionWindow_IsAFlowControlError()
    {
        using var peer = new PeerStream(Wire(CreateData(1, new byte[11], isEndStream: false)));
        var connection = new Http2Connection(peer, 1, 10, 65536);
        _ = connection.OpenStream();

        Assert.AreEqual(Http2ErrorCode.FlowControlError, await ProtocolErrorOf(connection));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_DataOnStreamZero_SendsGoAwayWithProtocolError()
    {
        var (connection, peer) = Connect(new Http2Frame(Http2FrameType.Data, 0, 0, new byte[1]));

        Assert.AreEqual(Http2ErrorCode.ProtocolError, await ProtocolErrorOf(connection));
        CollectionAssert.AreEqual(FromHex("000008 07 00 00000000 00000000 00000001"), peer.Written.ToArray());
    }

    [TestMethod]
    [DataRow(3, DisplayName = "an odd stream not yet opened")]
    [DataRow(2, DisplayName = "a server-initiated stream")]
    public async Task ReadStreamFrameAsync_DataOnAnIdleStream_IsAProtocolError(int streamId)
    {
        var (connection, _) = Connect(CreateData(streamId, new byte[1], isEndStream: false));
        _ = connection.OpenStream();

        Assert.AreEqual(Http2ErrorCode.ProtocolError, await ProtocolErrorOf(connection));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_HeadersWithContinuations_ReturnsTheJoinedBlock()
    {
        var (connection, _) = Connect(
            CreateHeaders(1, new byte[] { 0x88 }, isEndStream: true, isEndHeaders: false),
            CreateContinuation(1, new byte[] { 0x40 }, isEndHeaders: false),
            CreateContinuation(1, new byte[] { 0x41 }, isEndHeaders: true));
        _ = connection.OpenStream();

        var frame = await connection.ReadStreamFrameAsync(None);

        CollectionAssert.AreEqual(new byte[] { 0x88, 0x40, 0x41 }, frame!.Content.ToArray());
        Assert.IsTrue(frame.IsEndStream);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_ContinuedHeadersOnAResetStream_AreStillReturned()
    {
        var (connection, _) = Connect(
            CreateHeaders(1, new byte[] { 0x88 }, isEndStream: false, isEndHeaders: false),
            CreateContinuation(1, new byte[] { 0x40 }, isEndHeaders: true));
        var streamId = connection.OpenStream();
        await connection.ResetStreamAsync(streamId, Http2ErrorCode.Cancel, None);

        var frame = await connection.ReadStreamFrameAsync(None);

        CollectionAssert.AreEqual(new byte[] { 0x88, 0x40 }, frame!.Content.ToArray());
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_HeaderBlockInterruptedByAnotherFrame_IsAProtocolError()
    {
        var (connection, _) = Connect(CreateHeaders(1, new byte[] { 0x88 }, isEndStream: false, isEndHeaders: false), CreatePing(0, isAcknowledgement: false));
        _ = connection.OpenStream();

        var exception = await Assert.ThrowsExactlyAsync<Http2ProtocolException>(() => connection.ReadStreamFrameAsync(None));

        Assert.AreEqual("HTTP/2 ProtocolError: Ping on stream 0 interrupted stream 1's header block.", exception.Message);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_ContinuationOnAnotherStream_IsAProtocolError()
    {
        var (connection, _) = Connect(CreateHeaders(1, new byte[] { 0x88 }, isEndStream: false, isEndHeaders: false), CreateContinuation(3, new byte[] { 0x40 }, isEndHeaders: true));
        _ = connection.OpenStream();
        _ = connection.OpenStream();

        Assert.AreEqual(Http2ErrorCode.ProtocolError, await ProtocolErrorOf(connection));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_FloodOfEmptyContinuations_IsEnhanceYourCalm()
    {
        var continuations = Enumerable.Range(0, Http2Connection.MaximumContinuationFrames + 1).Select(_ => CreateContinuation(1, ReadOnlyMemory<byte>.Empty, isEndHeaders: false));
        var (connection, _) = Connect([CreateHeaders(1, new byte[1], isEndStream: false, isEndHeaders: false), .. continuations]);
        _ = connection.OpenStream();

        Assert.AreEqual(Http2ErrorCode.EnhanceYourCalm, await ProtocolErrorOf(connection));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_ConnectionClosedInAHeaderBlock_ThrowsEndOfStream()
    {
        var (connection, _) = Connect(CreateHeaders(1, new byte[] { 0x88 }, isEndStream: false, isEndHeaders: false));
        _ = connection.OpenStream();

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => connection.ReadStreamFrameAsync(None));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_ContinuationWithoutHeaders_IsAProtocolError()
    {
        var (connection, _) = Connect(CreateContinuation(1, new byte[] { 0x40 }, isEndHeaders: true));
        _ = connection.OpenStream();

        Assert.AreEqual(Http2ErrorCode.ProtocolError, await ProtocolErrorOf(connection));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_PushPromise_IsAProtocolErrorBecausePushIsDisabled()
    {
        var (connection, _) = Connect(CreatePushPromise(1, 2, new byte[] { 0x82 }, isEndHeaders: true));
        _ = connection.OpenStream();

        Assert.AreEqual(Http2ErrorCode.ProtocolError, await ProtocolErrorOf(connection));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_HeadersAfterEndStream_IsStreamClosed()
    {
        var (connection, _) = Connect(
            CreateHeaders(1, new byte[] { 0x88 }, isEndStream: true, isEndHeaders: true),
            CreateHeaders(1, new byte[] { 0x88 }, isEndStream: true, isEndHeaders: true));
        _ = connection.OpenStream();
        _ = await connection.ReadStreamFrameAsync(None);

        Assert.AreEqual(Http2ErrorCode.StreamClosed, await ProtocolErrorOf(connection));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_PriorityAndUnknownFrames_AreIgnored()
    {
        var (connection, peer) = Connect(
            CreatePriority(1, new Http2Priority(0, false, 16)),
            new Http2Frame((Http2FrameType)0xfa, 0, 0, new byte[3]),
            CreateData(1, "x"u8.ToArray(), isEndStream: true));
        _ = connection.OpenStream();

        var frame = await connection.ReadStreamFrameAsync(None);

        Assert.AreEqual(Http2FrameType.Data, frame!.Type);
        Assert.AreEqual(0, peer.Written.Length);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_MalformedPriority_IsAFrameSizeError()
    {
        var (connection, _) = Connect(new Http2Frame(Http2FrameType.Priority, 0, 1, new byte[4]));

        Assert.AreEqual(Http2ErrorCode.FrameSizeError, await ProtocolErrorOf(connection));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_FrameOverOurMaximumFrameSize_IsAFrameSizeError()
    {
        var (connection, peer) = Connect(CreateData(1, new byte[16385], isEndStream: false));
        _ = connection.OpenStream();

        Assert.AreEqual(Http2ErrorCode.FrameSizeError, await ProtocolErrorOf(connection));
        AssertFrame(CreateGoAway(0, Http2ErrorCode.FrameSizeError, ReadOnlyMemory<byte>.Empty), (await peer.WrittenFrames()).Single());
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_RstStreamOnAnOpenStream_ThrowsStreamResetWithoutGoAway()
    {
        var (connection, peer) = Connect(CreateRstStream(1, Http2ErrorCode.RefusedStream), CreateData(1, "x"u8.ToArray(), isEndStream: true));
        var streamId = connection.OpenStream();

        var exception = await Assert.ThrowsExactlyAsync<Http2StreamResetException>(() => connection.ReadStreamFrameAsync(None));

        Assert.AreEqual(streamId, exception.StreamId);
        Assert.AreEqual(Http2ErrorCode.RefusedStream, exception.ErrorCode);
        Assert.AreEqual("HTTP/2 stream 1 was reset by the peer: RefusedStream.", exception.Message);
        Assert.AreEqual(0, peer.Written.Length);
        Assert.IsNull(await connection.ReadStreamFrameAsync(None), "DATA on the reset stream is ignored");
        Assert.AreEqual(0, connection.OpenStreamCount);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_RstStreamOnAnIdleStream_IsAProtocolError()
    {
        var (connection, _) = Connect(CreateRstStream(1, Http2ErrorCode.Cancel));

        Assert.AreEqual(Http2ErrorCode.ProtocolError, await ProtocolErrorOf(connection));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_InitialWindowSizeChange_AdjustsOpenStreamsSendWindows()
    {
        var (connection, peer) = Connect(CreateSettings([new(Http2SettingIdentifier.InitialWindowSize, 100)]));
        var streamId = await StartStreamAsync(connection, peer);
        _ = await connection.WriteDataAsync(streamId, new byte[65000], isEndStream: false, None);

        _ = await connection.ReadStreamFrameAsync(None);

        Assert.AreEqual(535, connection.ConnectionSendWindow.Size, "the connection window is not changed by SETTINGS");
        Assert.AreEqual(0, connection.GetAvailableSendWindow(streamId), "65535 - 65000 + (100 - 65535) is negative");
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_InitialWindowSizeOverflowingAStreamWindow_IsAFlowControlError()
    {
        var (connection, _) = Connect(CreateWindowUpdate(1, int.MaxValue - 65535), CreateSettings([new(Http2SettingIdentifier.InitialWindowSize, 65536)]));
        _ = connection.OpenStream();

        Assert.AreEqual(Http2ErrorCode.FlowControlError, await ProtocolErrorOf(connection));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_InvalidSetting_SendsGoAwayWithProtocolError()
    {
        var (connection, peer) = Connect(CreateSettings([new(Http2SettingIdentifier.EnablePush, 2)]));

        Assert.AreEqual(Http2ErrorCode.ProtocolError, await ProtocolErrorOf(connection));
        AssertFrame(CreateGoAway(0, Http2ErrorCode.ProtocolError, ReadOnlyMemory<byte>.Empty), (await peer.WrittenFrames()).Single());
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_Ping_IsAnsweredWithTheSameOpaqueData()
    {
        var (connection, peer) = Connect(CreatePing(0xdeadbeef, isAcknowledgement: false), CreatePing(0x1, isAcknowledgement: true));

        Assert.IsNull(await connection.ReadStreamFrameAsync(None));

        AssertFrame(CreatePing(0xdeadbeef, isAcknowledgement: true), (await peer.WrittenFrames()).Single());
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_GoAwayWithAnError_ThrowsGoAway()
    {
        var (connection, _) = Connect(CreateGoAway(0, Http2ErrorCode.EnhanceYourCalm, "slow down"u8.ToArray()));

        var exception = await Assert.ThrowsExactlyAsync<Http2GoAwayException>(() => connection.ReadStreamFrameAsync(None));

        Assert.AreEqual(Http2ErrorCode.EnhanceYourCalm, exception.GoAway.ErrorCode);
        Assert.AreEqual("HTTP/2 GOAWAY from the peer: EnhanceYourCalm, last stream 0.", exception.Message);
        Assert.AreSame(exception.GoAway, connection.PeerGoAway);
        Assert.ThrowsExactly<InvalidOperationException>(() => connection.OpenStream());
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_GracefulGoAwayLeavingAnOpenStreamUnprocessed_ThrowsGoAway()
    {
        var (connection, _) = Connect(CreateGoAway(1, Http2ErrorCode.NoError, ReadOnlyMemory<byte>.Empty));
        _ = connection.OpenStream();
        _ = connection.OpenStream();

        var exception = await Assert.ThrowsExactlyAsync<Http2GoAwayException>(() => connection.ReadStreamFrameAsync(None));

        Assert.AreEqual(1, exception.GoAway.LastStreamId);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_GracefulGoAwayCoveringEveryOpenStream_LetsThemFinish()
    {
        var (connection, _) = Connect(
            CreateHeaders(3, new byte[] { 0x88 }, isEndStream: true, isEndHeaders: true),
            CreateGoAway(1, Http2ErrorCode.NoError, ReadOnlyMemory<byte>.Empty),
            CreateData(1, "x"u8.ToArray(), isEndStream: true));
        _ = connection.OpenStream();
        var closedStreamId = connection.OpenStream();
        await connection.WriteHeadersAsync(closedStreamId, new byte[] { 0x82 }, isEndStream: true, None);
        _ = await connection.ReadStreamFrameAsync(None);

        var frame = await connection.ReadStreamFrameAsync(None);

        Assert.AreEqual(1, frame!.StreamId);
        Assert.IsNotNull(connection.PeerGoAway);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_WindowUpdates_GrowTheSendWindows()
    {
        var (connection, _) = Connect(CreateWindowUpdate(0, 100), CreateWindowUpdate(1, 200));
        var streamId = connection.OpenStream();

        _ = await connection.ReadStreamFrameAsync(None);

        Assert.AreEqual(65635, connection.ConnectionSendWindow.Size);
        Assert.AreEqual(65635, connection.GetAvailableSendWindow(streamId));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_WindowUpdateOnAResetStream_IsIgnored()
    {
        var (connection, _) = Connect(CreateWindowUpdate(1, 100));
        var streamId = connection.OpenStream();
        await connection.ResetStreamAsync(streamId, Http2ErrorCode.Cancel, None);

        Assert.IsNull(await connection.ReadStreamFrameAsync(None));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_WindowUpdateOnAnIdleStream_IsAProtocolError()
    {
        var (connection, _) = Connect(CreateWindowUpdate(1, 100));

        Assert.AreEqual(Http2ErrorCode.ProtocolError, await ProtocolErrorOf(connection));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_WindowUpdateOverflowingTheConnectionWindow_IsAFlowControlError()
    {
        var (connection, _) = Connect(CreateWindowUpdate(0, int.MaxValue));

        var exception = await Assert.ThrowsExactlyAsync<Http2ProtocolException>(() => connection.ReadStreamFrameAsync(None));

        Assert.AreEqual(Http2ErrorCode.FlowControlError, exception.ErrorCode);
        Assert.AreEqual("HTTP/2 FlowControlError: WINDOW_UPDATE grew stream 0's window past 2^31 - 1.", exception.Message);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_WindowUpdateOverflowingAStreamWindow_IsAFlowControlError()
    {
        var (connection, _) = Connect(CreateWindowUpdate(1, int.MaxValue));
        _ = connection.OpenStream();

        Assert.AreEqual(Http2ErrorCode.FlowControlError, await ProtocolErrorOf(connection));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_EightContinuations_AreAccepted()
    {
        var continuations = Enumerable.Range(0, Http2Connection.MaximumContinuationFrames).Select(index => CreateContinuation(1, new byte[] { 0x40 }, index == Http2Connection.MaximumContinuationFrames - 1));
        var (connection, _) = Connect([CreateHeaders(1, new byte[] { 0x88 }, isEndStream: false, isEndHeaders: false), .. continuations]);
        _ = connection.OpenStream();

        var frame = await connection.ReadStreamFrameAsync(None);

        Assert.AreEqual(9, frame!.Content.Length);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_RstStreamNoErrorAfterTheResponseEnded_KeepsTheResponse()
    {
        var (connection, peer) = Connect(
            CreateHeaders(1, new byte[] { 0x88 }, isEndStream: true, isEndHeaders: true),
            CreateRstStream(1, Http2ErrorCode.NoError));
        _ = await StartStreamAsync(connection, peer);

        var response = await connection.ReadStreamFrameAsync(None);

        Assert.IsTrue(response!.IsEndStream);
        Assert.IsNull(await connection.ReadStreamFrameAsync(None));
        Assert.AreEqual(0, connection.OpenStreamCount);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_RstStreamNoErrorBeforeTheResponseEnded_ThrowsStreamReset()
    {
        var (connection, _) = Connect(CreateRstStream(1, Http2ErrorCode.NoError));
        _ = connection.OpenStream();

        var exception = await Assert.ThrowsExactlyAsync<Http2StreamResetException>(() => connection.ReadStreamFrameAsync(None));

        Assert.AreEqual(Http2ErrorCode.NoError, exception.ErrorCode);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_ServerEnablingPush_IsAProtocolError()
    {
        var (connection, _) = Connect(CreateSettings([new(Http2SettingIdentifier.EnablePush, 1)]));

        Assert.AreEqual(Http2ErrorCode.ProtocolError, await ProtocolErrorOf(connection));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_AfterAProtocolError_EveryCallThrows()
    {
        var (connection, _) = Connect(CreateContinuation(1, new byte[] { 0x40 }, isEndHeaders: true));
        var streamId = connection.OpenStream();
        var protocolError = await Assert.ThrowsExactlyAsync<Http2ProtocolException>(() => connection.ReadStreamFrameAsync(None));

        var failure = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.ReadStreamFrameAsync(None));

        Assert.AreSame(protocolError, failure.InnerException);
        Assert.ThrowsExactly<InvalidOperationException>(() => connection.OpenStream());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.WriteHeadersAsync(streamId, new byte[] { 0x82 }, isEndStream: true, None));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_GoAwayCannotBeWritten_StillThrowsTheProtocolError()
    {
        using var peer = new PeerStream(Wire(CreateContinuation(1, new byte[] { 0x40 }, isEndHeaders: true)), failWrites: true);
        var connection = new Http2Connection(peer);

        var exception = await Assert.ThrowsExactlyAsync<Http2ProtocolException>(() => connection.ReadStreamFrameAsync(None));

        Assert.AreEqual(Http2ErrorCode.ProtocolError, exception.ErrorCode);
    }

    [TestMethod]
    public async Task WriteDataAsync_BeforeHeaders_Throws()
    {
        var (connection, _) = Connect();
        var streamId = connection.OpenStream();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.WriteDataAsync(streamId, new byte[1], isEndStream: true, None));
    }

    [TestMethod]
    public async Task WriteHeadersAsync_StreamStartedOutOfOrder_Throws()
    {
        var (connection, _) = Connect();
        var first = connection.OpenStream();
        var second = connection.OpenStream();
        await connection.WriteHeadersAsync(second, new byte[] { 0x82 }, isEndStream: false, None);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.WriteHeadersAsync(first, new byte[] { 0x82 }, isEndStream: false, None));
        await connection.WriteHeadersAsync(second, new byte[] { 0x40 }, isEndStream: true, None);
    }

    [TestMethod]
    public async Task WriteDataAsync_WriteFails_LeavesTheWindowsAndStreamUnchanged()
    {
        using var peer = new PeerStream([], failWrites: true);
        var connection = new Http2Connection(peer);
        var streamId = connection.OpenStream();
        await Assert.ThrowsExactlyAsync<IOException>(() => connection.WriteHeadersAsync(streamId, new byte[] { 0x82 }, isEndStream: false, None));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.WriteDataAsync(streamId, new byte[5], isEndStream: true, None));

        Assert.AreEqual(65535, connection.ConnectionSendWindow.Size);
        Assert.AreEqual(1, connection.OpenStreamCount);
    }

    [TestMethod]
    public async Task ResetStreamAsync_IdleStream_Throws()
    {
        var (connection, peer) = Connect();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.ResetStreamAsync(1, Http2ErrorCode.Cancel, None));
        Assert.AreEqual(0, peer.Written.Length);
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
