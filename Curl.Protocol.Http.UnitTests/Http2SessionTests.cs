using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins what <see cref="Http2Session" /> gives an h2c upgrade request, and that its closing
/// GOAWAY tolerates a connection already gone.
/// </summary>
[TestClass]
public sealed class Http2SessionTests
{
    [TestMethod]
    public void UpgradeSettings_IsTheMeasuredHttp2SettingsValue()
    {
        // curl --http2 -v http://127.0.0.1:48716/ sent HTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA (BL-716 Notes).
        Assert.AreEqual("AAMAAABkAAQAAQAAAAIAAAAA", Http2Session.UpgradeSettings);
    }

    [TestMethod]
    public async Task ShutDownAsync_ConnectionThatFailsTheGoAway_CompletesWithoutThrowing()
    {
        FailingSendConnection connection = new(new IOException("reset"), writesBeforeFailure: null);
        Http2Session session = new(connection);
        _ = await session.StartStreamAsync((Http2StreamConnection)session.CreateStream("http", 0, ignoresBody: false), [new(":method", "GET")], isEndStream: true, CancellationToken.None);

        await session.ShutDownAsync(CancellationToken.None);

        Assert.IsTrue(session.Frames.IsGoAwaySent, "the GOAWAY was written; the flush after it failed");
    }

    [TestMethod]
    public async Task ReadAsync_TwoStreamsWhoseResponsesInterleave_EachReadsItsOwn()
    {
        HpackEncoder server = new();
        ScriptedConnection connection = new(Frames(
            Headers(server, 3, isEndStream: false),
            Http2FrameFactory.CreateData(3, "three"u8.ToArray(), isEndStream: false),
            Headers(server, 1, isEndStream: false),
            Http2FrameFactory.CreateData(1, "one"u8.ToArray(), isEndStream: true),
            Http2FrameFactory.CreateData(3, "!"u8.ToArray(), isEndStream: true)), 65536);
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1");
        Http2StreamConnection second = await StartAsync(session, "/2");

        string firstResponse = await ReadToEndAsync(first);
        string secondResponse = await ReadToEndAsync(second);

        Assert.AreEqual(1, first.StreamId);
        Assert.AreEqual(3, second.StreamId);
        Assert.AreEqual("HTTP/2 200 \r\n\r\none", firstResponse);
        Assert.AreEqual("HTTP/2 200 \r\n\r\nthree!", secondResponse);
    }

    [TestMethod]
    public async Task ReadAsync_WhenAnotherStreamWasReset_FailsThatStreamOnlyWithExit92()
    {
        HpackEncoder server = new();
        ScriptedConnection connection = new(Frames(
            Http2FrameFactory.CreateRstStream(3, Http2ErrorCode.Cancel),
            Headers(server, 1, isEndStream: true)), 65536);
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1");
        Http2StreamConnection second = await StartAsync(session, "/2");

        string firstResponse = await ReadToEndAsync(first);
        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(() => ReadToEndAsync(second));

        Assert.AreEqual("HTTP/2 200 \r\n\r\n", firstResponse);
        Assert.AreEqual(CurlExitCode.Http2Stream, failure.ExitCode);
    }

    [TestMethod]
    public async Task ReadAsync_WhenAResetComesForAStreamWhoseResponseEnded_DropsIt()
    {
        HpackEncoder server = new();
        ScriptedConnection connection = new(Frames(
            Headers(server, 1, isEndStream: true),
            Http2FrameFactory.CreateRstStream(1, Http2ErrorCode.Cancel),
            Headers(server, 3, isEndStream: true)), 65536);
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1", bodyLength: null);
        Http2StreamConnection second = await StartAsync(session, "/2");

        string firstResponse = await ReadToEndAsync(first);
        string secondResponse = await ReadToEndAsync(second);

        Assert.AreEqual("HTTP/2 200 \r\n\r\n", firstResponse);
        Assert.AreEqual("HTTP/2 200 \r\n\r\n", secondResponse);
    }

    [TestMethod]
    public async Task ReadAsync_WhenAStreamWasResetByThisClient_DropsItsLaterHeaders()
    {
        HpackEncoder server = new();
        ScriptedConnection connection = new(Frames(
            Headers(server, 1, isEndStream: true),
            Headers(server, 3, isEndStream: true)), 65536);
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1");
        Http2StreamConnection second = await StartAsync(session, "/2");
        await session.ResetStreamAsync(1, Http2ErrorCode.Cancel, CancellationToken.None);

        string secondResponse = await ReadToEndAsync(second);

        Assert.AreEqual("HTTP/2 200 \r\n\r\n", secondResponse);
        Assert.IsFalse(first.HasReceived);
    }

    [TestMethod]
    public async Task ReadAsync_AfterTheConnectionFailed_FailsEveryStreamTheSameWay()
    {
        ScriptedConnection connection = new(Frames(
            Http2FrameFactory.CreateGoAway(0, Http2ErrorCode.ProtocolError, ReadOnlyMemory<byte>.Empty)), 65536);
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1");
        Http2StreamConnection second = await StartAsync(session, "/2");

        HttpTransferException firstFailure = await Assert.ThrowsExactlyAsync<HttpTransferException>(() => ReadToEndAsync(first));
        HttpTransferException secondFailure = await Assert.ThrowsExactlyAsync<HttpTransferException>(() => ReadToEndAsync(second));

        Assert.AreEqual(CurlExitCode.RecvError, firstFailure.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, secondFailure.ExitCode);
    }

    [TestMethod]
    public async Task ReadAsync_AfterAHeaderBlockDidNotDecode_FailsEveryStreamWithExit16()
    {
        ScriptedConnection connection = new(Frames(
            Http2FrameFactory.CreateHeaders(3, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, isEndStream: true, isEndHeaders: true)), 65536);
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1");
        Http2StreamConnection second = await StartAsync(session, "/2");

        HttpTransferException firstFailure = await Assert.ThrowsExactlyAsync<HttpTransferException>(() => ReadToEndAsync(first));
        HttpTransferException secondFailure = await Assert.ThrowsExactlyAsync<HttpTransferException>(() => ReadToEndAsync(second));

        Assert.AreEqual(CurlExitCode.Http2, firstFailure.ExitCode);
        Assert.AreEqual(CurlExitCode.Http2, secondFailure.ExitCode);
    }

    [TestMethod]
    public async Task ReceiveAsync_WhenTheReceiverAlreadyHasAFrame_ReadsNoOther()
    {
        HpackEncoder server = new();
        ScriptedConnection connection = new(
            [.. Http2FrameCodec.Serialize(Headers(server, 1, isEndStream: true)), .. Http2FrameCodec.Serialize(Headers(server, 3, isEndStream: true))], 65536);
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1");
        Http2StreamConnection second = await StartAsync(session, "/2");

        await session.ReceiveAsync(first, CancellationToken.None);
        await session.ReceiveAsync(first, CancellationToken.None);

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

        connection.Push(Http2FrameCodec.Serialize(Headers(server, 1, isEndStream: true)));
        await Task.WhenAll(firstWait, secondWait);

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

        connection.Push(Http2FrameCodec.Serialize(Http2FrameFactory.CreateGoAway(0, Http2ErrorCode.InternalError, ReadOnlyMemory<byte>.Empty)));

        await Assert.ThrowsExactlyAsync<Http2GoAwayException>(() => firstWait);
        await Assert.ThrowsExactlyAsync<Http2GoAwayException>(() => secondWait);
    }

    [TestMethod]
    public async Task WriteAsync_WhenThePeersStreamsAreAllOpen_WaitsForOneToCloseBeforeOpeningAnother()
    {
        HpackEncoder server = new();
        ScriptedConnection connection = new(Frames(
            [new Http2Setting(Http2SettingIdentifier.MaxConcurrentStreams, 1)],
            Headers(server, 1, isEndStream: true),
            Headers(server, 3, isEndStream: true)), 65536);
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1");
        await session.ReceiveAsync(first, CancellationToken.None);
        Assert.AreEqual(1, session.ConcurrentTransferLimit);

        Http2StreamConnection second = await StartAsync(session, "/2");

        Assert.IsTrue(first.HasReceived, "stream 1's response was read while stream 3 waited");
        Assert.AreEqual(3, second.StreamId);
        Assert.AreEqual("HTTP/2 200 \r\n\r\n", await ReadToEndAsync(second));
    }

    [TestMethod]
    public async Task WriteAsync_WhenTheOpenStreamIsResetWhileAnotherWaits_HandsTheResetOnAndOpensTheNext()
    {
        ScriptedConnection connection = new(Frames(
            [new Http2Setting(Http2SettingIdentifier.MaxConcurrentStreams, 1)],
            Http2FrameFactory.CreateRstStream(1, Http2ErrorCode.RefusedStream)), 65536);
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1");
        await session.ReceiveAsync(first, CancellationToken.None);

        Http2StreamConnection second = await StartAsync(session, "/2");
        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(() => ReadToEndAsync(first));

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
        connection.Push(frame[..5]);
        using CancellationTokenSource cancellation = new();
        Task receiving = session.ReceiveAsync(stream, cancellation.Token).AsTask();
        await connection.ReadsAskedAsync(2);

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => receiving);
        Assert.IsFalse(stream.HasReceived);
    }

    [TestMethod]
    public async Task WriteAsync_WhenTheConnectionFailsWhileWaitingForAStream_FailsWithTheTransferError()
    {
        ScriptedConnection connection = new(Frames(
            [new Http2Setting(Http2SettingIdentifier.MaxConcurrentStreams, 1)],
            Http2FrameFactory.CreateGoAway(1, Http2ErrorCode.ProtocolError, ReadOnlyMemory<byte>.Empty)), 65536);
        Http2Session session = new(connection);
        Http2StreamConnection first = await StartAsync(session, "/1");
        await session.ReceiveAsync(first, CancellationToken.None);

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(() => StartAsync(session, "/2"));

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
    }

    [TestMethod]
    public async Task ConcurrentTransferLimit_IsUnlimitedUntilThePeersSettingsAndZeroAfterAGoAway()
    {
        ScriptedConnection connection = new(Frames(
            [new Http2Setting(Http2SettingIdentifier.MaxConcurrentStreams, 7)],
            Http2FrameFactory.CreateGoAway(1, Http2ErrorCode.NoError, ReadOnlyMemory<byte>.Empty)), 65536);
        Http2Session session = new(connection);
        Http2StreamConnection stream = await StartAsync(session, "/1");
        int? before = session.ConcurrentTransferLimit;

        await session.ReceiveAsync(stream, CancellationToken.None);
        int? afterSettings = session.ConcurrentTransferLimit;
        await session.ReceiveAsync(stream, CancellationToken.None);

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
}
