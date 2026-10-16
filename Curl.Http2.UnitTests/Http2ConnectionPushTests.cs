using Curl.Testing;
using static Curl.Http2.Hpack;
using static Curl.Http2.Http2FrameFactory;
using static Curl.Http2.Http2Test;

namespace Curl.Http2;

/// <summary>
/// Drives server push on <see cref="Http2Connection" /> (BL-2515, RFC 9113 sections 6.6 and
/// 8.4): ENABLE_PUSH in the preface, PUSH_PROMISE returned with its promised stream, the
/// promised stream's response, resetting a push, and the PUSH_PROMISE a client must refuse.
/// </summary>
[TestClass]
public sealed class Http2ConnectionPushTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    private static readonly byte[] PromisedRequest = FromHex("82 86 84 41 0f 7777772e6578616d706c652e636f6d");

    // The measured preface (Http2ConnectionTests.MeasuredPreface) with ENABLE_PUSH 1, as
    // curl's lib/http2.c populate_settings sends when the multi handle has a push callback.
    private const string PushEnabledPreface =
        "505249202a20485454502f322e300d0a0d0a534d0d0a0d0a" +
        "000012 04 00 00000000 0003 00000064 0004 00010000 0002 00000001" +
        "000004 08 00 00000000 3e7f0001";

    private const string PushDisabledPreface =
        "505249202a20485454502f322e300d0a0d0a534d0d0a0d0a" +
        "000012 04 00 00000000 0003 00000064 0004 00010000 0002 00000000" +
        "000004 08 00 00000000 3e7f0001";

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SendPrefaceAsync_PushEnabled_AdvertisesEnablePushOne()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect(isPushEnabled: true);
        diagnostics.Arrange("IsPushEnabled", connection.IsPushEnabled);

        await connection.SendPrefaceAsync(None);
        diagnostics.Bytes("written", peer.Written.ToArray());

        diagnostics.Diff("preface bytes", FromHex(PushEnabledPreface), peer.Written.ToArray());
        CollectionAssert.AreEqual(FromHex(PushEnabledPreface), peer.Written.ToArray());
        Assert.AreSame(Http2Connection.PushEnabledClientSettings, connection.Settings);
    }

    [TestMethod]
    public async Task SendPrefaceAsync_PushDisabled_AdvertisesEnablePushZeroAsBefore()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect(isPushEnabled: false);
        diagnostics.Arrange("IsPushEnabled", connection.IsPushEnabled);

        await connection.SendPrefaceAsync(None);
        diagnostics.Bytes("written", peer.Written.ToArray());

        diagnostics.Diff("preface bytes", FromHex(PushDisabledPreface), peer.Written.ToArray());
        CollectionAssert.AreEqual(FromHex(PushDisabledPreface), peer.Written.ToArray());
        Assert.AreSame(Http2Connection.ClientSettings, connection.Settings);
        Assert.IsFalse(new Http2Connection(new PeerStream([])).IsPushEnabled, "push is off by default");
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_PushPromise_ReturnsItThenThePromisedStreamsHeadersAndData()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(
            isPushEnabled: true,
            CreatePushPromise(1, 2, PromisedRequest, isEndHeaders: true),
            CreateHeaders(2, new byte[] { 0x88 }, isEndStream: false, isEndHeaders: true),
            CreateData(2, "pushed"u8.ToArray(), isEndStream: true));
        diagnostics.Arrange("peer frames", "PUSH_PROMISE stream 1 promising 2, HEADERS stream 2, DATA stream 2 END_STREAM");
        var parent = await OpenParentAsync(connection);

        var promise = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("push promise", promise);
        diagnostics.Act("pushed stream count", connection.PushedStreamCount);
        Assert.AreEqual(Http2FrameType.PushPromise, promise!.Type);
        Assert.AreEqual(parent, promise.StreamId);
        Assert.AreEqual(2, promise.PromisedStreamId);
        CollectionAssert.AreEqual(PromisedRequest, promise.Content.ToArray());
        Assert.IsFalse(promise.IsEndStream);
        Assert.AreEqual(1, connection.PushedStreamCount);
        Assert.AreEqual(1, connection.OpenStreamCount, "the promised stream is counted apart from client streams");

        var headers = await connection.ReadStreamFrameAsync(None);
        var data = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("pushed headers", headers);
        diagnostics.Act("pushed data", data);

        diagnostics.Assert("pushed headers stream", 2, headers!.StreamId);
        Assert.AreEqual(new Http2StreamFrame(Http2FrameType.Headers, 2, headers.Content, false), headers);
        CollectionAssert.AreEqual(new byte[] { 0x88 }, headers.Content.ToArray());
        Assert.AreEqual(0, headers.PromisedStreamId);
        Assert.AreEqual(Http2FrameType.Data, data!.Type);
        Assert.AreEqual(2, data.StreamId);
        CollectionAssert.AreEqual("pushed"u8.ToArray(), data.Content.ToArray());
        Assert.IsTrue(data.IsEndStream);
        Assert.AreEqual(0, connection.PushedStreamCount, "the push is forgotten once the server ends it");
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_PushPromiseContinued_ReturnsTheWholeHeaderBlock()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(
            isPushEnabled: true,
            CreatePushPromise(1, 2, PromisedRequest[..3], isEndHeaders: false, padLength: 2),
            CreateContinuation(1, PromisedRequest[3..], isEndHeaders: true),
            CreateHeaders(2, new byte[] { 0x88 }, isEndStream: true, isEndHeaders: true));
        diagnostics.Arrange("peer frames", "PUSH_PROMISE stream 1 promising 2 (padded, no END_HEADERS), CONTINUATION, HEADERS stream 2");
        _ = await OpenParentAsync(connection);

        var promise = await connection.ReadStreamFrameAsync(None);
        var headers = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("push promise", promise);
        diagnostics.Act("pushed headers", headers);

        diagnostics.Diff("header block", PromisedRequest, promise!.Content.ToArray());
        Assert.AreEqual(Http2FrameType.PushPromise, promise.Type);
        Assert.AreEqual(1, promise.StreamId);
        Assert.AreEqual(2, promise.PromisedStreamId);
        CollectionAssert.AreEqual(PromisedRequest, promise.Content.ToArray());
        Assert.AreEqual(Http2FrameType.Headers, headers!.Type);
        Assert.AreEqual(2, headers.StreamId);
        Assert.IsTrue(headers.IsEndStream);
    }

    [TestMethod]
    public async Task ResetStreamAsync_PromisedStream_WritesRstStreamAndDropsItsLaterFrames()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect(
            isPushEnabled: true,
            CreatePushPromise(1, 2, PromisedRequest, isEndHeaders: true),
            CreateData(2, "late"u8.ToArray(), isEndStream: false),
            CreateWindowUpdate(2, 5),
            CreateRstStream(2, Http2ErrorCode.Cancel),
            CreateHeaders(1, new byte[] { 0x88 }, isEndStream: true, isEndHeaders: true));
        diagnostics.Arrange("peer frames", "PUSH_PROMISE 1 -> 2, then DATA, WINDOW_UPDATE, RST_STREAM on 2, HEADERS on 1");
        _ = await OpenParentAsync(connection);
        _ = await connection.ReadStreamFrameAsync(None);
        var writtenBefore = (int)peer.Written.Length;

        await connection.ResetStreamAsync(2, Http2ErrorCode.Cancel, None);
        var resetBytes = peer.Written.ToArray()[writtenBefore..];
        var next = await connection.ReadStreamFrameAsync(None);
        diagnostics.Bytes("RST_STREAM written", resetBytes);
        diagnostics.Act("next stream frame", next);

        diagnostics.Diff("RST_STREAM bytes", FromHex("000004 03 00 00000002 00000008"), resetBytes);
        CollectionAssert.AreEqual(FromHex("000004 03 00 00000002 00000008"), resetBytes);
        Assert.AreEqual(Http2FrameType.Headers, next!.Type, "DATA on the reset push is dropped");
        Assert.AreEqual(1, next.StreamId);
        Assert.AreEqual(0, connection.PushedStreamCount);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_PushPromiseOnAStreamTheClientReset_IsStillReturnedAndReserved()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(isPushEnabled: true, CreatePushPromise(1, 2, PromisedRequest, isEndHeaders: true));
        diagnostics.Arrange("peer frames", "PUSH_PROMISE on stream 1 after the client reset it");
        var parent = await OpenParentAsync(connection);
        await connection.ResetStreamAsync(parent, Http2ErrorCode.Cancel, None);

        var promise = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("push promise", promise);

        diagnostics.Assert("promised stream", 2, promise!.PromisedStreamId);
        Assert.AreEqual(2, promise.PromisedStreamId, "the HPACK decoder must still see the block");
        Assert.AreEqual(1, connection.PushedStreamCount);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_GracefulGoAwayBelowAPromisedStream_DoesNotThrow()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(
            isPushEnabled: true,
            CreatePushPromise(1, 2, PromisedRequest, isEndHeaders: true),
            CreateGoAway(1, Http2ErrorCode.NoError, ReadOnlyMemory<byte>.Empty));
        diagnostics.Arrange("peer frames", "PUSH_PROMISE 1 -> 2, GOAWAY last stream 1 NO_ERROR");
        _ = await OpenParentAsync(connection);
        _ = await connection.ReadStreamFrameAsync(None);

        var end = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("after GOAWAY", end);

        Assert.IsNull(end, "GOAWAY's last stream counts client streams only");
        Assert.IsNotNull(connection.PeerGoAway);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_PushPromiseWithPushOff_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(isPushEnabled: false, CreatePushPromise(1, 2, PromisedRequest, isEndHeaders: true));
        diagnostics.Arrange("peer frames", "PUSH_PROMISE 1 -> 2 with push off");
        _ = await OpenParentAsync(connection);

        var error = await ProtocolErrorOf(connection);
        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
    }

    [TestMethod]
    [DataRow(3, 2, DisplayName = "on an unopened client stream")]
    [DataRow(2, 4, DisplayName = "on an even stream the client did not open")]
    [DataRow(1, 3, DisplayName = "promising an odd stream")]
    [DataRow(1, 0, DisplayName = "promising stream 0")]
    public async Task ReadStreamFrameAsync_InvalidPushPromise_IsAProtocolError(int parentStreamId, int promisedStreamId)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect(isPushEnabled: true, CreatePushPromise(parentStreamId, promisedStreamId, PromisedRequest, isEndHeaders: true));
        diagnostics.Arrange("PUSH_PROMISE", $"stream {parentStreamId} promising {promisedStreamId}");
        _ = await OpenParentAsync(connection);

        var error = await ProtocolErrorOf(connection);
        var written = (await peer.WrittenFrames())[^1];
        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);

        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
        AssertFrame(CreateGoAway(0, Http2ErrorCode.ProtocolError, ReadOnlyMemory<byte>.Empty), written);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_PushPromiseNotAboveAnEarlierPromise_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(
            isPushEnabled: true,
            CreatePushPromise(1, 4, PromisedRequest, isEndHeaders: true),
            CreatePushPromise(1, 2, PromisedRequest, isEndHeaders: true));
        diagnostics.Arrange("peer frames", "PUSH_PROMISE 1 -> 4, then 1 -> 2");
        _ = await OpenParentAsync(connection);
        _ = await connection.ReadStreamFrameAsync(None);

        var error = await ProtocolErrorOf(connection);
        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_PushPromiseAfterTheParentsEndStream_IsStreamClosed()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(
            isPushEnabled: true,
            CreateHeaders(1, new byte[] { 0x88 }, isEndStream: true, isEndHeaders: true),
            CreatePushPromise(1, 2, PromisedRequest, isEndHeaders: true));
        diagnostics.Arrange("peer frames", "HEADERS END_STREAM on 1, then PUSH_PROMISE 1 -> 2");
        var parent = connection.OpenStream();
        await connection.WriteHeadersAsync(parent, new byte[] { 0x82 }, isEndStream: false, None);
        _ = await connection.ReadStreamFrameAsync(None);

        var error = await ProtocolErrorOf(connection);
        diagnostics.Assert("error code", Http2ErrorCode.StreamClosed, error);
        Assert.AreEqual(Http2ErrorCode.StreamClosed, error);
    }

    [TestMethod]
    public async Task ResetStreamAsync_EvenStreamNotPromised_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(isPushEnabled: true, CreatePushPromise(1, 2, PromisedRequest, isEndHeaders: true));
        diagnostics.Arrange("promised stream", 2);
        _ = await OpenParentAsync(connection);
        _ = await connection.ReadStreamFrameAsync(None);

        var unpromised = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.ResetStreamAsync(4, Http2ErrorCode.Cancel, None));
        var zero = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.ResetStreamAsync(0, Http2ErrorCode.Cancel, None));
        diagnostics.Act("stream 4", unpromised.GetType().Name);
        diagnostics.Act("stream 0", zero.GetType().Name);

        diagnostics.Assert("stream 4", nameof(InvalidOperationException), unpromised.GetType().Name);
        Assert.IsInstanceOfType<InvalidOperationException>(zero);
    }

    private static async Task<int> OpenParentAsync(Http2Connection connection)
    {
        var streamId = connection.OpenStream();
        await connection.WriteHeadersAsync(streamId, new byte[] { 0x82 }, isEndStream: true, None);
        return streamId;
    }

    private static (Http2Connection Connection, PeerStream Peer) Connect(bool isPushEnabled, params Http2Frame[] fromPeer)
    {
        var peer = new PeerStream(Wire(fromPeer));
        return (new Http2Connection(peer) { IsPushEnabled = isPushEnabled }, peer);
    }

    private static async Task<Http2ErrorCode> ProtocolErrorOf(Http2Connection connection) =>
        (await Assert.ThrowsExactlyAsync<Http2ProtocolException>(() => connection.ReadStreamFrameAsync(None))).ErrorCode;
}
