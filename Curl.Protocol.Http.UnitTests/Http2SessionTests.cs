using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins what <see cref="Http2Session" /> gives an h2c upgrade request, and that its closing
/// GOAWAY tolerates a connection already gone.
/// </summary>
[TestClass]
public sealed class Http2SessionTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void UpgradeSettings_IsTheMeasuredHttp2SettingsValue()
    {
        // curl --http2 -v http://127.0.0.1:48716/ sent HTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA (BL-716 Notes).
        Diagnostics.Arrange("measured HTTP2-Settings header from real curl", "AAMAAABkAAQAAQAAAAIAAAAA");
        string upgradeSettings = Http2Session.UpgradeSettings;
        Diagnostics.Act("upgrade settings value", upgradeSettings);

        Diagnostics.Assert("upgrade settings value", "AAMAAABkAAQAAQAAAAIAAAAA", upgradeSettings);
        Assert.AreEqual("AAMAAABkAAQAAQAAAAIAAAAA", Http2Session.UpgradeSettings);
    }

    [TestMethod]
    public async Task ShutDownAsync_ConnectionThatFailsTheGoAway_CompletesWithoutThrowing()
    {
        Diagnostics.Arrange("connection failure on send", "IOException reset, from the first write");
        FailingSendConnection connection = new(new IOException("reset"), writesBeforeFailure: null);
        Http2Session session = new(connection);
        _ = await session.StartStreamAsync((Http2StreamConnection)session.CreateStream("http", 0, ignoresBody: false), [new(":method", "GET")], isEndStream: true, CancellationToken.None);

        await session.ShutDownAsync(CancellationToken.None);

        Diagnostics.Act("GOAWAY sent", session.Frames.IsGoAwaySent);
        Diagnostics.Assert("GOAWAY sent", true, session.Frames.IsGoAwaySent);
        Assert.IsTrue(session.Frames.IsGoAwaySent, "the GOAWAY was written; the flush after it failed");
    }

    [TestMethod]
    public async Task ReadAsync_TwoStreamsWhoseResponsesInterleave_EachReadsItsOwn()
    {
        HpackEncoder server = new();
        byte[] scripted = Frames(
            Headers(server, 3, isEndStream: false),
            Http2FrameFactory.CreateData(3, "three"u8.ToArray(), isEndStream: false),
            Headers(server, 1, isEndStream: false),
            Http2FrameFactory.CreateData(1, "one"u8.ToArray(), isEndStream: true),
            Http2FrameFactory.CreateData(3, "!"u8.ToArray(), isEndStream: true));
        Diagnostics.Arrange("server frames", "SETTINGS, HEADERS 3, DATA 3 three, HEADERS 1, DATA 1 one end, DATA 3 ! end");
        Diagnostics.Bytes("scripted server bytes", scripted);
        ScriptedConnection connection = new(scripted, 65536);
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1");
        Http2StreamConnection second = await StartAsync(session, "/2");

        string firstResponse = await ReadToEndAsync(first);
        string secondResponse = await ReadToEndAsync(second);

        Diagnostics.Act("stream ids", string.Join(", ", first.StreamId, second.StreamId));
        Diagnostics.Act("first response", Visible(firstResponse));
        Diagnostics.Act("second response", Visible(secondResponse));
        Diagnostics.Assert("second response", Visible("HTTP/2 200 \r\n\r\nthree!"), Visible(secondResponse));
        Assert.AreEqual(1, first.StreamId);
        Assert.AreEqual(3, second.StreamId);
        Assert.AreEqual("HTTP/2 200 \r\n\r\none", firstResponse);
        Assert.AreEqual("HTTP/2 200 \r\n\r\nthree!", secondResponse);
    }

    [TestMethod]
    public async Task ReadAsync_WhenAnotherStreamWasReset_FailsThatStreamOnlyWithExit92()
    {
        HpackEncoder server = new();
        byte[] scripted = Frames(
            Http2FrameFactory.CreateRstStream(3, Http2ErrorCode.Cancel),
            Headers(server, 1, isEndStream: true));
        Diagnostics.Arrange("server frames", "SETTINGS, RST_STREAM 3 CANCEL, HEADERS 1 end");
        Diagnostics.Bytes("scripted server bytes", scripted);
        ScriptedConnection connection = new(scripted, 65536);
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1");
        Http2StreamConnection second = await StartAsync(session, "/2");

        string firstResponse = await ReadToEndAsync(first);
        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(() => ReadToEndAsync(second));

        Diagnostics.Act("first response", Visible(firstResponse));
        Diagnostics.Act("second stream failure", failure.Message);
        Diagnostics.Assert("second stream exit code", CurlExitCode.Http2Stream, failure.ExitCode);
        Assert.AreEqual("HTTP/2 200 \r\n\r\n", firstResponse);
        Assert.AreEqual(CurlExitCode.Http2Stream, failure.ExitCode);
    }

    [TestMethod]
    public async Task ReadAsync_WhenAResetComesForAStreamWhoseResponseEnded_DropsIt()
    {
        HpackEncoder server = new();
        byte[] scripted = Frames(
            Headers(server, 1, isEndStream: true),
            Http2FrameFactory.CreateRstStream(1, Http2ErrorCode.Cancel),
            Headers(server, 3, isEndStream: true));
        Diagnostics.Arrange("server frames", "SETTINGS, HEADERS 1 end, RST_STREAM 1 CANCEL, HEADERS 3 end");
        Diagnostics.Bytes("scripted server bytes", scripted);
        ScriptedConnection connection = new(scripted, 65536);
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1", bodyLength: null);
        Http2StreamConnection second = await StartAsync(session, "/2");

        string firstResponse = await ReadToEndAsync(first);
        string secondResponse = await ReadToEndAsync(second);

        Diagnostics.Act("first response", Visible(firstResponse));
        Diagnostics.Act("second response", Visible(secondResponse));
        Diagnostics.Assert("second response", Visible("HTTP/2 200 \r\n\r\n"), Visible(secondResponse));
        Assert.AreEqual("HTTP/2 200 \r\n\r\n", firstResponse);
        Assert.AreEqual("HTTP/2 200 \r\n\r\n", secondResponse);
    }

    [TestMethod]
    public async Task ReadAsync_WhenAStreamWasResetByThisClient_DropsItsLaterHeaders()
    {
        HpackEncoder server = new();
        byte[] scripted = Frames(
            Headers(server, 1, isEndStream: true),
            Headers(server, 3, isEndStream: true));
        Diagnostics.Arrange("server frames", "SETTINGS, HEADERS 1 end, HEADERS 3 end; client resets stream 1");
        Diagnostics.Bytes("scripted server bytes", scripted);
        ScriptedConnection connection = new(scripted, 65536);
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1");
        Http2StreamConnection second = await StartAsync(session, "/2");
        await session.ResetStreamAsync(1, Http2ErrorCode.Cancel, CancellationToken.None);

        string secondResponse = await ReadToEndAsync(second);

        Diagnostics.Act("second response", Visible(secondResponse));
        Diagnostics.Act("reset stream received", first.HasReceived);
        Diagnostics.Assert("second response", Visible("HTTP/2 200 \r\n\r\n"), Visible(secondResponse));
        Assert.AreEqual("HTTP/2 200 \r\n\r\n", secondResponse);
        Assert.IsFalse(first.HasReceived);
    }

    [TestMethod]
    public async Task ReadAsync_AfterTheConnectionFailed_FailsEveryStreamTheSameWay()
    {
        byte[] scripted = Frames(
            Http2FrameFactory.CreateGoAway(0, Http2ErrorCode.ProtocolError, ReadOnlyMemory<byte>.Empty));
        Diagnostics.Arrange("server frames", "SETTINGS, GOAWAY 0 PROTOCOL_ERROR");
        Diagnostics.Bytes("scripted server bytes", scripted);
        ScriptedConnection connection = new(scripted, 65536);
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1");
        Http2StreamConnection second = await StartAsync(session, "/2");

        HttpTransferException firstFailure = await Assert.ThrowsExactlyAsync<HttpTransferException>(() => ReadToEndAsync(first));
        HttpTransferException secondFailure = await Assert.ThrowsExactlyAsync<HttpTransferException>(() => ReadToEndAsync(second));

        Diagnostics.Act("first stream failure", firstFailure.Message);
        Diagnostics.Act("second stream failure", secondFailure.Message);
        Diagnostics.Assert("exit codes", string.Join(", ", CurlExitCode.RecvError, CurlExitCode.RecvError), string.Join(", ", firstFailure.ExitCode, secondFailure.ExitCode));
        Assert.AreEqual(CurlExitCode.RecvError, firstFailure.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, secondFailure.ExitCode);
    }

    [TestMethod]
    public async Task ReadAsync_AfterAHeaderBlockDidNotDecode_FailsEveryStreamWithExit16()
    {
        byte[] scripted = Frames(
            Http2FrameFactory.CreateHeaders(3, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, isEndStream: true, isEndHeaders: true));
        Diagnostics.Arrange("server frames", "SETTINGS, HEADERS 3 with an undecodable header block ff ff ff ff ff ff");
        Diagnostics.Bytes("scripted server bytes", scripted);
        ScriptedConnection connection = new(scripted, 65536);
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1");
        Http2StreamConnection second = await StartAsync(session, "/2");

        HttpTransferException firstFailure = await Assert.ThrowsExactlyAsync<HttpTransferException>(() => ReadToEndAsync(first));
        HttpTransferException secondFailure = await Assert.ThrowsExactlyAsync<HttpTransferException>(() => ReadToEndAsync(second));

        Diagnostics.Act("first stream failure", firstFailure.Message);
        Diagnostics.Act("second stream failure", secondFailure.Message);
        Diagnostics.Assert("exit codes", string.Join(", ", CurlExitCode.Http2, CurlExitCode.Http2), string.Join(", ", firstFailure.ExitCode, secondFailure.ExitCode));
        Assert.AreEqual(CurlExitCode.Http2, firstFailure.ExitCode);
        Assert.AreEqual(CurlExitCode.Http2, secondFailure.ExitCode);
    }

    [TestMethod]
    public async Task ReceiveAsync_WhenTheReceiverAlreadyHasAFrame_ReadsNoOther()
    {
        HpackEncoder server = new();
        byte[] scripted = [.. Http2FrameCodec.Serialize(Headers(server, 1, isEndStream: true)), .. Http2FrameCodec.Serialize(Headers(server, 3, isEndStream: true))];
        Diagnostics.Arrange("server frames", "HEADERS 1 end, HEADERS 3 end, with no SETTINGS");
        Diagnostics.Bytes("scripted server bytes", scripted);
        ScriptedConnection connection = new(scripted, 65536);
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1");
        Http2StreamConnection second = await StartAsync(session, "/2");

        await session.ReceiveAsync(first, CancellationToken.None);
        await session.ReceiveAsync(first, CancellationToken.None);

        Diagnostics.Act("received by stream 1 and stream 3", string.Join(", ", first.HasReceived, second.HasReceived));
        Diagnostics.Assert("stream 3 received", false, second.HasReceived);
        Assert.IsTrue(first.HasReceived);
        Assert.IsFalse(second.HasReceived);
    }

    [TestMethod]
    public async Task ReceiveAsync_TwoStreamsWaitingForOneFrame_OneReadsItAndTheOtherReturns()
    {
        HpackEncoder server = new();
        PushedBytesConnection connection = new();
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1");
        Http2StreamConnection second = await StartAsync(session, "/2");
        Task firstWait = session.ReceiveAsync(first, CancellationToken.None).AsTask();
        Task secondWait = session.ReceiveAsync(second, CancellationToken.None).AsTask();
        byte[] pushed = Http2FrameCodec.Serialize(Headers(server, 1, isEndStream: true));
        Diagnostics.Arrange("frame pushed while both streams wait", "HEADERS 1 end");
        Diagnostics.Bytes("pushed bytes", pushed);

        connection.Push(pushed);
        await Task.WhenAll(firstWait, secondWait);

        Diagnostics.Act("received by stream 1 and stream 3", string.Join(", ", first.HasReceived, second.HasReceived));
        Diagnostics.Act("connection reads", connection.ReadCount);
        Diagnostics.Assert("connection reads", 1, connection.ReadCount);
        Assert.IsTrue(first.HasReceived);
        Assert.IsFalse(second.HasReceived);
        Assert.AreEqual(1, connection.ReadCount);
    }

    [TestMethod]
    public async Task ReceiveAsync_TwoStreamsWaitingWhenTheConnectionFails_BothSeeTheFailure()
    {
        PushedBytesConnection connection = new();
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1");
        Http2StreamConnection second = await StartAsync(session, "/2");
        Task firstWait = session.ReceiveAsync(first, CancellationToken.None).AsTask();
        Task secondWait = session.ReceiveAsync(second, CancellationToken.None).AsTask();
        byte[] pushed = Http2FrameCodec.Serialize(Http2FrameFactory.CreateGoAway(0, Http2ErrorCode.InternalError, ReadOnlyMemory<byte>.Empty));
        Diagnostics.Arrange("frame pushed while both streams wait", "GOAWAY 0 INTERNAL_ERROR");
        Diagnostics.Bytes("pushed bytes", pushed);

        connection.Push(pushed);

        Http2GoAwayException firstFailure = await Assert.ThrowsExactlyAsync<Http2GoAwayException>(() => firstWait);
        Http2GoAwayException secondFailure = await Assert.ThrowsExactlyAsync<Http2GoAwayException>(() => secondWait);
        Diagnostics.Act("first stream failure", firstFailure.Message);
        Diagnostics.Act("second stream failure", secondFailure.Message);
        Diagnostics.Assert("failure types", string.Join(", ", nameof(Http2GoAwayException), nameof(Http2GoAwayException)), string.Join(", ", firstFailure.GetType().Name, secondFailure.GetType().Name));
    }

    [TestMethod]
    public async Task WriteAsync_WhenThePeersStreamsAreAllOpen_WaitsForOneToCloseBeforeOpeningAnother()
    {
        HpackEncoder server = new();
        byte[] scripted = Frames(
            [new Http2Setting(Http2SettingIdentifier.MaxConcurrentStreams, 1)],
            Headers(server, 1, isEndStream: true),
            Headers(server, 3, isEndStream: true));
        Diagnostics.Arrange("server frames", "SETTINGS MAX_CONCURRENT_STREAMS 1, HEADERS 1 end, HEADERS 3 end");
        Diagnostics.Bytes("scripted server bytes", scripted);
        ScriptedConnection connection = new(scripted, 65536);
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1");
        await session.ReceiveAsync(first, CancellationToken.None);
        Diagnostics.Act("concurrent transfer limit after SETTINGS", session.ConcurrentTransferLimit);
        Assert.AreEqual(1, session.ConcurrentTransferLimit);

        Http2StreamConnection second = await StartAsync(session, "/2");

        Diagnostics.Act("second stream id", second.StreamId);
        Diagnostics.Assert("second stream id", 3, second.StreamId);
        Assert.IsTrue(first.HasReceived, "stream 1's response was read while stream 3 waited");
        Assert.AreEqual(3, second.StreamId);
        Assert.AreEqual("HTTP/2 200 \r\n\r\n", await ReadToEndAsync(second));
    }

    [TestMethod]
    public async Task WriteAsync_WhenTheOpenStreamIsResetWhileAnotherWaits_HandsTheResetOnAndOpensTheNext()
    {
        byte[] scripted = Frames(
            [new Http2Setting(Http2SettingIdentifier.MaxConcurrentStreams, 1)],
            Http2FrameFactory.CreateRstStream(1, Http2ErrorCode.RefusedStream));
        Diagnostics.Arrange("server frames", "SETTINGS MAX_CONCURRENT_STREAMS 1, RST_STREAM 1 REFUSED_STREAM");
        Diagnostics.Bytes("scripted server bytes", scripted);
        ScriptedConnection connection = new(scripted, 65536);
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1");
        await session.ReceiveAsync(first, CancellationToken.None);

        Http2StreamConnection second = await StartAsync(session, "/2");
        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(() => ReadToEndAsync(first));

        Diagnostics.Act("second stream id", second.StreamId);
        Diagnostics.Act("first stream failure", failure.Message);
        Diagnostics.Assert("first stream exit code", CurlExitCode.Http2Stream, failure.ExitCode);
        Assert.AreEqual(3, second.StreamId);
        Assert.AreEqual(CurlExitCode.Http2Stream, failure.ExitCode);
    }

    [TestMethod]
    public async Task ReceiveAsync_CancelledPartWayThroughAFrame_ThrowsWithoutFailingTheConnection()
    {
        HpackEncoder server = new();
        PushedBytesConnection connection = new();
        Http2Session session = new(connection);
        Http2StreamConnection stream = await StartAsync(session, "/1");
        byte[] frame = Http2FrameCodec.Serialize(Headers(server, 1, isEndStream: true));
        Diagnostics.Arrange("frame bytes pushed before cancelling", "first 5 of a HEADERS 1 end frame");
        Diagnostics.Bytes("pushed bytes", frame[..5]);
        connection.Push(frame[..5]);
        using CancellationTokenSource cancellation = new();
        Task receiving = session.ReceiveAsync(stream, cancellation.Token).AsTask();
        await connection.ReadsAskedAsync(2);

        await cancellation.CancelAsync();

        OperationCanceledException failure = await Assert.ThrowsAsync<OperationCanceledException>(() => receiving);
        Diagnostics.Act("cancellation", failure.GetType().Name);
        Diagnostics.Assert("stream received", false, stream.HasReceived);
        Assert.IsFalse(stream.HasReceived);
    }

    [TestMethod]
    public async Task WriteAsync_WhenTheConnectionFailsWhileWaitingForAStream_FailsWithTheTransferError()
    {
        byte[] scripted = Frames(
            [new Http2Setting(Http2SettingIdentifier.MaxConcurrentStreams, 1)],
            Http2FrameFactory.CreateGoAway(1, Http2ErrorCode.ProtocolError, ReadOnlyMemory<byte>.Empty));
        Diagnostics.Arrange("server frames", "SETTINGS MAX_CONCURRENT_STREAMS 1, GOAWAY 1 PROTOCOL_ERROR");
        Diagnostics.Bytes("scripted server bytes", scripted);
        ScriptedConnection connection = new(scripted, 65536);
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1");
        await session.ReceiveAsync(first, CancellationToken.None);

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(() => StartAsync(session, "/2"));

        Diagnostics.Act("second stream failure", failure.Message);
        Diagnostics.Assert("exit code", CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
    }

    [TestMethod]
    public async Task ConcurrentTransferLimit_IsUnlimitedUntilThePeersSettingsAndZeroAfterAGoAway()
    {
        byte[] scripted = Frames(
            [new Http2Setting(Http2SettingIdentifier.MaxConcurrentStreams, 7)],
            Http2FrameFactory.CreateGoAway(1, Http2ErrorCode.NoError, ReadOnlyMemory<byte>.Empty));
        Diagnostics.Arrange("server frames", "SETTINGS MAX_CONCURRENT_STREAMS 7, GOAWAY 1 NO_ERROR");
        Diagnostics.Bytes("scripted server bytes", scripted);
        ScriptedConnection connection = new(scripted, 65536);
        Http2Session session = new(connection);
        Http2StreamConnection stream = await StartAsync(session, "/1");
        int? before = session.ConcurrentTransferLimit;

        await session.ReceiveAsync(stream, CancellationToken.None);
        int? afterSettings = session.ConcurrentTransferLimit;
        await session.ReceiveAsync(stream, CancellationToken.None);

        Diagnostics.Act("limit before, after SETTINGS, after GOAWAY", string.Join(", ", before, afterSettings, session.ConcurrentTransferLimit));
        Diagnostics.Assert("limit after GOAWAY", 0, session.ConcurrentTransferLimit);
        Assert.AreEqual(int.MaxValue, before);
        Assert.AreEqual(7, afterSettings);
        Assert.AreEqual(0, session.ConcurrentTransferLimit);
    }

    private static async Task<Http2StreamConnection> StartAsync(Http2Session session, string path, long? bodyLength = 0)
    {
        Http2StreamConnection stream = (Http2StreamConnection)session.CreateStream("http", bodyLength, ignoresBody: false);
        await stream.WriteAsync(Encoding.Latin1.GetBytes($"GET {path} HTTP/1.1\r\nHost: h\r\n\r\n"), CancellationToken.None);
        return stream;
    }

    private static async Task<string> ReadToEndAsync(Http2StreamConnection stream)
    {
        MemoryStream response = new();
        byte[] buffer = new byte[256];
        int read;
        while ((read = await stream.ReadAsync(buffer, CancellationToken.None)) > 0)
        {
            response.Write(buffer, 0, read);
        }

        return Encoding.Latin1.GetString(response.ToArray());
    }

    private static Http2Frame Headers(HpackEncoder server, int streamId, bool isEndStream) =>
        Http2FrameFactory.CreateHeaders(streamId, server.Encode([new(":status", "200")]), isEndStream, isEndHeaders: true);

    /// <summary>Gives a server's empty SETTINGS followed by <paramref name="frames" />.</summary>
    private static byte[] Frames(params Http2Frame[] frames) => Frames([], frames);

    /// <summary>Gives a server's SETTINGS with <paramref name="settings" /> followed by <paramref name="frames" />.</summary>
    private static byte[] Frames(IReadOnlyList<Http2Setting> settings, params Http2Frame[] frames) =>
    [
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings(settings)),
        .. frames.SelectMany(Http2FrameCodec.Serialize),
    ];

    private static string Visible(string text) => text.Replace("\r", "\\r").Replace("\n", "\\n");
}
