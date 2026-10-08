using Curl.Testing;
using static Curl.Http2.Hpack;
using static Curl.Http2.Http2FrameFactory;
using static Curl.Http2.Http2Test;

namespace Curl.Http2;

/// <summary>
/// Adversarial black-box attacks on <see cref="Http2Connection" /> through an in-memory peer
/// (BL-1499, by the method in Documentation/Wiki/Adversarial-Testing.md): WINDOW_UPDATE of 0
/// and past 2^31 - 1, frames on stream 0 and on server-chosen even streams, CONTINUATION
/// floods at the limit and one past it, frames that interrupt a header block, SETTINGS at
/// the entry limit, RST_STREAM and GOAWAY in the middle of a response, a whole response one
/// byte per read, and calls after the connection failed. The oracle is RFC 9113 and the
/// connection's documented exceptions.
/// </summary>
[TestClass]
public sealed class Http2ConnectionAdversarialTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    private static readonly byte[] StatusOk = FromHex("88");

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadFrameAsync_ConnectionWindowUpdateExactlyToTheMaximum_IsAccepted()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateWindowUpdate(0, int.MaxValue - Http2Settings.DefaultInitialWindowSize));
        diagnostics.Arrange("increment", int.MaxValue - Http2Settings.DefaultInitialWindowSize);

        _ = await connection.ReadFrameAsync(None);
        diagnostics.Act("connection send window", connection.ConnectionSendWindow.Size);

        diagnostics.Assert("connection send window", (long)int.MaxValue, connection.ConnectionSendWindow.Size);
        Assert.AreEqual(int.MaxValue, connection.ConnectionSendWindow.Size);
    }

    [TestMethod]
    public async Task ReadFrameAsync_ConnectionWindowUpdateOnePastTheMaximum_FailsWithFlowControlErrorAndSendsGoAway()
    {
        await AssertConnectionErrorAsync(Http2ErrorCode.FlowControlError, _ => { }, CreateWindowUpdate(0, int.MaxValue - Http2Settings.DefaultInitialWindowSize + 1));
    }

    [TestMethod]
    public async Task ReadFrameAsync_StreamWindowUpdatePastTheMaximum_FailsWithFlowControlError()
    {
        await AssertConnectionErrorAsync(Http2ErrorCode.FlowControlError, OpenOneStream, CreateWindowUpdate(1, int.MaxValue));
    }

    [TestMethod]
    [DataRow(0, DisplayName = "connection")]
    [DataRow(1, DisplayName = "open stream")]
    public async Task ReadFrameAsync_WindowUpdateOfZero_FailsWithProtocolError(int streamId)
    {
        await AssertConnectionErrorAsync(Http2ErrorCode.ProtocolError, OpenOneStream, new Http2Frame(Http2FrameType.WindowUpdate, 0, streamId, new byte[4]));
    }

    [TestMethod]
    [DataRow(2, DisplayName = "server-chosen even stream")]
    [DataRow(3, DisplayName = "odd stream not yet opened")]
    public async Task ReadFrameAsync_WindowUpdateOnAnIdleStream_FailsWithProtocolError(int streamId)
    {
        await AssertConnectionErrorAsync(Http2ErrorCode.ProtocolError, OpenOneStream, CreateWindowUpdate(streamId, 1));
    }

    [TestMethod]
    public async Task ReadFrameAsync_WindowUpdateOnAStreamThisEndpointReset_IsIgnored()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect(CreateWindowUpdate(1, 100));
        var streamId = connection.OpenStream();
        await connection.ResetStreamAsync(streamId, Http2ErrorCode.Cancel, None);
        diagnostics.Arrange("reset stream", streamId);

        var frame = await connection.ReadFrameAsync(None);
        diagnostics.Act("frame", frame);

        diagnostics.Assert("frames written", 1, (await peer.WrittenFrames()).Count);
        Assert.IsNull(frame);
        Assert.HasCount(1, await peer.WrittenFrames());
    }

    [TestMethod]
    public async Task ReadFrameAsync_DataOnStreamZero_FailsWithProtocolError()
    {
        await AssertConnectionErrorAsync(Http2ErrorCode.ProtocolError, OpenOneStream, new Http2Frame(Http2FrameType.Data, 0, 0, new byte[1]));
    }

    [TestMethod]
    [DataRow(2, DisplayName = "server-chosen even stream")]
    [DataRow(3, DisplayName = "odd stream not yet opened")]
    public async Task ReadFrameAsync_HeadersOnAnIdleStream_FailsWithProtocolError(int streamId)
    {
        await AssertConnectionErrorAsync(Http2ErrorCode.ProtocolError, OpenOneStream, CreateHeaders(streamId, StatusOk, isEndStream: true, isEndHeaders: true));
    }

    [TestMethod]
    public async Task ReadFrameAsync_ContinuationWithNoHeaderBlockOpen_FailsWithProtocolError()
    {
        await AssertConnectionErrorAsync(Http2ErrorCode.ProtocolError, OpenOneStream, CreateContinuation(1, StatusOk, isEndHeaders: true));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_HeaderBlockOfExactlyTheMostContinuationFrames_ReturnsTheJoinedBlock()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(HeaderBlockWithContinuations(Http2Connection.MaximumContinuationFrames));
        _ = connection.OpenStream();
        diagnostics.Arrange("continuation frames", Http2Connection.MaximumContinuationFrames);

        var frame = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("block length", frame?.Content.Length);

        diagnostics.Assert("block length", 1 + Http2Connection.MaximumContinuationFrames, frame?.Content.Length);
        Assert.AreEqual(1 + Http2Connection.MaximumContinuationFrames, frame?.Content.Length);
        Assert.IsTrue(frame!.IsEndStream);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_HeaderBlockOfOneContinuationFrameTooMany_FailsWithEnhanceYourCalm()
    {
        await AssertConnectionErrorAsync(Http2ErrorCode.EnhanceYourCalm, OpenOneStream, HeaderBlockWithContinuations(Http2Connection.MaximumContinuationFrames + 1));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_EmptyContinuationFloodWithoutEndHeaders_FailsWithEnhanceYourCalm()
    {
        Http2Frame[] flood =
        [
            CreateHeaders(1, StatusOk, isEndStream: false, isEndHeaders: false),
            .. Enumerable.Repeat(CreateContinuation(1, ReadOnlyMemory<byte>.Empty, isEndHeaders: false), 1000),
        ];
        await AssertConnectionErrorAsync(Http2ErrorCode.EnhanceYourCalm, OpenOneStream, flood);
    }

    [TestMethod]
    [DataRow(Http2FrameType.Continuation, 3, DisplayName = "CONTINUATION on another stream")]
    [DataRow(Http2FrameType.Data, 1, DisplayName = "DATA on the same stream")]
    [DataRow(Http2FrameType.Ping, 0, DisplayName = "PING")]
    [DataRow(Http2FrameType.Settings, 0, DisplayName = "SETTINGS")]
    public async Task ReadStreamFrameAsync_FrameInterruptingAHeaderBlock_FailsWithProtocolError(Http2FrameType type, int streamId)
    {
        var payloadLength = type == Http2FrameType.Ping ? 8 : 0;
        await AssertConnectionErrorAsync(
            Http2ErrorCode.ProtocolError,
            connection => _ = (connection.OpenStream(), connection.OpenStream()),
            CreateHeaders(1, StatusOk, isEndStream: false, isEndHeaders: false),
            new Http2Frame(type, 0, streamId, new byte[payloadLength]));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_ConnectionClosedInsideAHeaderBlock_ThrowsEndOfStream()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateHeaders(1, StatusOk, isEndStream: false, isEndHeaders: false));
        _ = connection.OpenStream();
        diagnostics.Arrange("peer frames", "HEADERS without END_HEADERS, then end of stream");

        var error = await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => connection.ReadStreamFrameAsync(None));
        diagnostics.Act("exception", error.GetType().Name);

        diagnostics.Assert("closed by peer", true, connection.IsClosedByPeer);
        Assert.IsTrue(connection.IsClosedByPeer);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_DataExactlyAtTheMaximumFrameSize_ReturnsIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateData(1, new byte[Http2FrameCodec.DefaultMaximumFrameSize], isEndStream: true));
        _ = connection.OpenStream();
        diagnostics.Arrange("data length", Http2FrameCodec.DefaultMaximumFrameSize);

        var frame = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("content length", frame?.Content.Length);

        diagnostics.Assert("content length", Http2FrameCodec.DefaultMaximumFrameSize, frame?.Content.Length);
        Assert.AreEqual(Http2FrameCodec.DefaultMaximumFrameSize, frame?.Content.Length);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_DataOnePastTheMaximumFrameSize_FailsWithFrameSizeError()
    {
        await AssertConnectionErrorAsync(Http2ErrorCode.FrameSizeError, OpenOneStream, CreateData(1, new byte[Http2FrameCodec.DefaultMaximumFrameSize + 1], isEndStream: true));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_DataAfterTheStreamEnded_FailsWithStreamClosed()
    {
        await AssertConnectionErrorAsync(
            Http2ErrorCode.StreamClosed,
            OpenOneStream,
            CreateData(1, new byte[1], isEndStream: true),
            CreateData(1, new byte[1], isEndStream: false));
    }

    [TestMethod]
    public async Task ReadFrameAsync_SettingsWithExactlyTheMostEntries_IsAppliedAndAcknowledged()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var settings = Enumerable.Repeat(new Http2Setting(Http2SettingIdentifier.MaxConcurrentStreams, 7), Http2Connection.MaximumSettingsEntries).ToArray();
        var (connection, peer) = Connect(CreateSettings(settings));
        diagnostics.Arrange("entries", settings.Length);

        _ = await connection.ReadFrameAsync(None);
        diagnostics.Act("max concurrent streams", connection.PeerSettings.MaxConcurrentStreams);

        diagnostics.Assert("max concurrent streams", 7u, connection.PeerSettings.MaxConcurrentStreams);
        Assert.AreEqual(7u, connection.PeerSettings.MaxConcurrentStreams);
        AssertFrame(CreateSettingsAcknowledgement(), (await peer.WrittenFrames()).Single());
    }

    [TestMethod]
    public async Task ReadFrameAsync_SettingsWithOneEntryTooMany_FailsWithEnhanceYourCalmBeforeApplyingAny()
    {
        var settings = Enumerable.Repeat(new Http2Setting(Http2SettingIdentifier.MaxConcurrentStreams, 7), Http2Connection.MaximumSettingsEntries + 1).ToArray();
        var connection = await AssertConnectionErrorAsync(Http2ErrorCode.EnhanceYourCalm, _ => { }, CreateSettings(settings));

        Assert.IsNull(connection.PeerSettings.MaxConcurrentStreams);
    }

    [TestMethod]
    [DataRow(Http2SettingIdentifier.MaxFrameSize, 16383u, Http2ErrorCode.ProtocolError)]
    [DataRow(Http2SettingIdentifier.InitialWindowSize, 2147483648u, Http2ErrorCode.FlowControlError)]
    [DataRow(Http2SettingIdentifier.EnablePush, 1u, Http2ErrorCode.ProtocolError)]
    public async Task ReadFrameAsync_SettingOutsideItsRange_FailsTheConnection(Http2SettingIdentifier identifier, uint value, Http2ErrorCode expected)
    {
        await AssertConnectionErrorAsync(expected, _ => { }, CreateSettings([new Http2Setting(identifier, value)]));
    }

    [TestMethod]
    public async Task ReadFrameAsync_InitialWindowSizeGrowingAStreamWindowPastTheMaximum_FailsWithFlowControlError()
    {
        await AssertConnectionErrorAsync(
            Http2ErrorCode.FlowControlError,
            OpenOneStream,
            CreateSettings([new Http2Setting(Http2SettingIdentifier.InitialWindowSize, 0)]),
            CreateWindowUpdate(1, int.MaxValue),
            CreateSettings([new Http2Setting(Http2SettingIdentifier.InitialWindowSize, 1)]));
    }

    [TestMethod]
    public async Task ReadFrameAsync_InitialWindowSizeOfZero_LeavesNothingToSend()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateSettings([new Http2Setting(Http2SettingIdentifier.InitialWindowSize, 0)]));
        var streamId = connection.OpenStream();
        diagnostics.Arrange("initial window size", 0);

        _ = await connection.ReadFrameAsync(None);
        diagnostics.Act("available send window", connection.GetAvailableSendWindow(streamId));

        diagnostics.Assert("available send window", 0, connection.GetAvailableSendWindow(streamId));
        Assert.AreEqual(0, connection.GetAvailableSendWindow(streamId));
    }

    [TestMethod]
    public async Task ReadFrameAsync_MaxConcurrentStreamsOfZero_RefusesTheNextOpenStream()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreateSettings([new Http2Setting(Http2SettingIdentifier.MaxConcurrentStreams, 0)]));
        diagnostics.Arrange("max concurrent streams", 0);

        _ = await connection.ReadFrameAsync(None);
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => connection.OpenStream());
        diagnostics.Act("exception", error.GetType().Name);

        diagnostics.Assert("open streams", 0, connection.OpenStreamCount);
        Assert.AreEqual(0, connection.OpenStreamCount);
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_RstStreamInTheMiddleOfAResponse_ThrowsStreamResetAndForgetsTheStream()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(
            CreateHeaders(1, StatusOk, isEndStream: false, isEndHeaders: true),
            CreateData(1, new byte[10], isEndStream: false),
            CreateRstStream(1, Http2ErrorCode.InternalError),
            CreateData(1, new byte[10], isEndStream: true));
        _ = connection.OpenStream();
        diagnostics.Arrange("peer frames", "HEADERS, DATA, RST_STREAM INTERNAL_ERROR, DATA");

        _ = await connection.ReadStreamFrameAsync(None);
        _ = await connection.ReadStreamFrameAsync(None);
        var error = await Assert.ThrowsExactlyAsync<Http2StreamResetException>(() => connection.ReadStreamFrameAsync(None));
        var afterReset = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("error code", error.ErrorCode);

        diagnostics.Assert("error code", Http2ErrorCode.InternalError, error.ErrorCode);
        Assert.AreEqual(Http2ErrorCode.InternalError, error.ErrorCode);
        Assert.AreEqual(1, error.StreamId);
        Assert.AreEqual(0, connection.OpenStreamCount);
        Assert.IsNull(afterReset);
    }

    [TestMethod]
    public async Task ReadFrameAsync_RstStreamOnAnIdleStream_FailsWithProtocolError()
    {
        await AssertConnectionErrorAsync(Http2ErrorCode.ProtocolError, OpenOneStream, CreateRstStream(5, Http2ErrorCode.Cancel));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_GoAwayLeavingTheOpenStreamUnprocessed_ThrowsGoAwayAndRefusesNewStreams()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(
            CreateHeaders(1, StatusOk, isEndStream: false, isEndHeaders: true),
            CreateGoAway(0, Http2ErrorCode.NoError, ReadOnlyMemory<byte>.Empty));
        _ = connection.OpenStream();
        _ = connection.OpenStream();
        diagnostics.Arrange("peer frames", "HEADERS on 1, GOAWAY last stream 0 NO_ERROR");

        _ = await connection.ReadStreamFrameAsync(None);
        var error = await Assert.ThrowsExactlyAsync<Http2GoAwayException>(() => connection.ReadStreamFrameAsync(None));
        diagnostics.Act("last stream", error.GoAway.LastStreamId);

        diagnostics.Assert("last stream", 0, error.GoAway.LastStreamId);
        Assert.AreEqual(0, error.GoAway.LastStreamId);
        _ = Assert.ThrowsExactly<InvalidOperationException>(() => connection.OpenStream());
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_GracefulGoAwayCoveringTheOpenStream_LetsTheResponseFinish()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(
            CreateHeaders(1, StatusOk, isEndStream: false, isEndHeaders: true),
            CreateGoAway(1, Http2ErrorCode.NoError, ReadOnlyMemory<byte>.Empty),
            CreateData(1, new byte[3], isEndStream: true));
        _ = connection.OpenStream();
        diagnostics.Arrange("peer frames", "HEADERS, GOAWAY last stream 1 NO_ERROR, DATA END_STREAM");

        _ = await connection.ReadStreamFrameAsync(None);
        var data = await connection.ReadStreamFrameAsync(None);
        diagnostics.Act("data", data);

        diagnostics.Assert("end stream", true, data?.IsEndStream);
        Assert.IsTrue(data?.IsEndStream);
        Assert.AreEqual(1, connection.PeerGoAway?.LastStreamId);
        _ = Assert.ThrowsExactly<InvalidOperationException>(() => connection.OpenStream());
    }

    [TestMethod]
    public async Task ReadFrameAsync_AfterAProtocolError_RefusesEveryFurtherCall()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = await AssertConnectionErrorAsync(Http2ErrorCode.ProtocolError, OpenOneStream, CreateData(1, new byte[1], isEndStream: true), CreateContinuation(1, StatusOk, isEndHeaders: true));
        diagnostics.Arrange("connection", "failed with PROTOCOL_ERROR");

        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.ReadFrameAsync(None));
        diagnostics.Act("exception", error.GetType().Name);

        diagnostics.Assert("inner exception", nameof(Http2ProtocolException), error.InnerException?.GetType().Name);
        Assert.IsInstanceOfType<Http2ProtocolException>(error.InnerException);
        _ = Assert.ThrowsExactly<InvalidOperationException>(() => connection.OpenStream());
        _ = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.ReadStreamFrameAsync(None));
    }

    [TestMethod]
    public async Task ReadFrameAsync_PingAcknowledgement_IsNotAnswered()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect(CreatePing(42, isAcknowledgement: true));
        diagnostics.Arrange("peer frames", "PING ACK");

        var frame = await connection.ReadFrameAsync(None);
        diagnostics.Act("bytes written", peer.Written.Length);

        diagnostics.Assert("bytes written", 0L, peer.Written.Length);
        Assert.IsNull(frame);
        Assert.AreEqual(0, peer.Written.Length);
    }

    [TestMethod]
    [DataRow(0xFAu, 0, DisplayName = "unknown type on the connection")]
    [DataRow(0xFAu, 2, DisplayName = "unknown type on an idle even stream")]
    [DataRow((uint)Http2FrameType.Priority, 2, DisplayName = "PRIORITY on an idle even stream")]
    public async Task ReadFrameAsync_FrameTheEndpointMayIgnore_IsIgnoredWithoutAnswer(uint type, int streamId)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var payloadLength = type == (uint)Http2FrameType.Priority ? 5 : 3;
        var (connection, peer) = Connect(new Http2Frame((Http2FrameType)type, 0xFF & ~Http2FrameFlags.Padded, streamId, new byte[payloadLength]));
        diagnostics.Arrange("frame", $"type {type} stream {streamId}");

        var frame = await connection.ReadFrameAsync(None);
        diagnostics.Act("bytes written", peer.Written.Length);

        diagnostics.Assert("bytes written", 0L, peer.Written.Length);
        Assert.IsNull(frame);
        Assert.AreEqual(0, peer.Written.Length);
    }

    [TestMethod]
    public async Task ReadFrameAsync_PriorityOnWhichAStreamDependsOnItself_FailsWithProtocolError()
    {
        await AssertConnectionErrorAsync(Http2ErrorCode.ProtocolError, OpenOneStream, CreatePriority(1, new Http2Priority(1, false, 16)));
    }

    [TestMethod]
    public async Task ReadFrameAsync_PushPromise_FailsWithProtocolError()
    {
        await AssertConnectionErrorAsync(Http2ErrorCode.ProtocolError, OpenOneStream, CreatePushPromise(1, 2, StatusOk, isEndHeaders: true));
    }

    [TestMethod]
    public async Task ReadStreamFrameAsync_WholeResponseOneBytePerRead_ReturnsTheSameStreamFrames()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Http2Frame[] response =
        [
            CreateSettings([new Http2Setting(Http2SettingIdentifier.MaxConcurrentStreams, 100)]),
            CreateHeaders(1, FromHex("88"), isEndStream: false, isEndHeaders: false),
            CreateContinuation(1, FromHex("5F 03 61 62 63"), isEndHeaders: true),
            CreatePing(9, isAcknowledgement: false),
            CreateData(1, "hello"u8.ToArray(), isEndStream: false, padLength: 7),
            CreateData(1, "world"u8.ToArray(), isEndStream: true),
        ];
        diagnostics.Arrange("read size", 1);

        var whole = await ReadAllStreamFramesAsync(new PeerStream(Wire(response)));
        var trickled = await ReadAllStreamFramesAsync(new PeerStream(Wire(response), readSize: 1));
        diagnostics.Act("stream frames", trickled.Count);

        diagnostics.Assert("stream frames", whole.Count, trickled.Count);
        Assert.HasCount(3, trickled);
        CollectionAssert.AreEqual(whole.Select(Describe).ToArray(), trickled.Select(Describe).ToArray());
    }

    [TestMethod]
    public async Task ReadFrameAsync_CancelledBeforeTheCall_ThrowsCancellationAndTheNextReadStillWorks()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, _) = Connect(CreatePing(1, isAcknowledgement: true));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        diagnostics.Arrange("token", "cancelled");

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => connection.ReadFrameAsync(cancellation.Token));
        var frame = await connection.ReadFrameAsync(None);
        diagnostics.Act("closed by peer after second read", connection.IsClosedByPeer);

        diagnostics.Assert("closed by peer", false, connection.IsClosedByPeer);
        Assert.IsNull(frame);
        Assert.IsFalse(connection.IsClosedByPeer);
    }

    private static void OpenOneStream(Http2Connection connection) => _ = connection.OpenStream();

    private static Http2Frame[] HeaderBlockWithContinuations(int continuationCount) =>
    [
        CreateHeaders(1, StatusOk, isEndStream: true, isEndHeaders: false),
        .. Enumerable.Range(1, continuationCount).Select(index => CreateContinuation(1, StatusOk, isEndHeaders: index == continuationCount)),
    ];

    private static (Http2Connection Connection, PeerStream Peer) Connect(params Http2Frame[] fromPeer)
    {
        var peer = new PeerStream(Wire(fromPeer));
        return (new Http2Connection(peer), peer);
    }

    private static async Task<List<Http2StreamFrame>> ReadAllStreamFramesAsync(PeerStream peer)
    {
        var connection = new Http2Connection(peer);
        _ = connection.OpenStream();
        var frames = new List<Http2StreamFrame>();
        while (await connection.ReadStreamFrameAsync(None) is { } frame)
        {
            frames.Add(frame);
        }

        return frames;
    }

    private static string Describe(Http2StreamFrame frame) =>
        $"{frame.Type} {frame.StreamId} {Convert.ToHexString(frame.Content.Span)} {frame.IsEndStream}";

    /// <summary>
    /// Reads frames until the connection fails, and asserts it failed with
    /// <paramref name="expected" /> and sent GOAWAY carrying that code as its last frame.
    /// </summary>
    private async Task<Http2Connection> AssertConnectionErrorAsync(Http2ErrorCode expected, Action<Http2Connection> arrange, params Http2Frame[] fromPeer)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, peer) = Connect(fromPeer);
        arrange(connection);
        diagnostics.Arrange("peer frames", string.Join(", ", fromPeer.Select(frame => $"{frame.Type}@{frame.StreamId}")));

        var error = await Assert.ThrowsExactlyAsync<Http2ProtocolException>(async () =>
        {
            while (!connection.IsClosedByPeer)
            {
                _ = await connection.ReadFrameAsync(None);
            }
        });
        var goAway = Http2FramePayloadParser.ParseGoAway((await peer.WrittenFrames())[^1]);
        diagnostics.Act("error code", error.ErrorCode);

        diagnostics.Assert("error code", expected, error.ErrorCode);
        Assert.AreEqual(expected, error.ErrorCode);
        Assert.AreEqual(expected, goAway.ErrorCode);
        return connection;
    }
}
