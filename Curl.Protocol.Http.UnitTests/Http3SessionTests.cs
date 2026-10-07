using System.Net;
using System.Text;
using Curl.Http3;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

[TestClass]
public sealed class Http3SessionTests
{
    /// <summary>The server's SETTINGS in the tests: a field section limit of 100 bytes.</summary>
    private static readonly Http3SettingsFrame ServerSettings = new([new Http3Setting(0x06, 100)]);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task OpenRequestStreamAsync_SecondRequest_OpensNoMoreUnidirectionalStreams()
    {
        Diagnostics.Arrange("request streams the connection hands out", "0, 4");
        FakeMultiplexedConnection quic = new(new FakeMultiplexedStream(0, []), new FakeMultiplexedStream(4, []));
        Http3Session session = new(quic);

        await session.OpenRequestStreamAsync(HttpFrameLog.Silent, CancellationToken.None);
        var second = await session.OpenRequestStreamAsync(HttpFrameLog.Silent, CancellationToken.None);

        long[] unidirectionalIds = quic.UnidirectionalStreams.Select(stream => stream.StreamId).ToArray();
        Diagnostics.Act("second request stream id", second.StreamId);
        Diagnostics.Act("unidirectional stream ids", string.Join(", ", unidirectionalIds));
        Diagnostics.Assert("unidirectional stream ids", "2, 6, 10", string.Join(", ", unidirectionalIds));
        Assert.AreEqual(4L, second.StreamId);
        Assert.HasCount(3, quic.UnidirectionalStreams);
        CollectionAssert.AreEqual(new long[] { 2, 6, 10 }, quic.UnidirectionalStreams.Select(stream => stream.StreamId).ToArray());
    }

    [TestMethod]
    public async Task DisposeAsync_AfterARequest_DisposesEveryStreamAndClosesWithNoError()
    {
        FakeMultiplexedStream request = new(0, []);
        FakeMultiplexedStream control = ServerStream(3, [0x00, .. ServerSettings.ToBytes()]);
        Diagnostics.Arrange("server control stream", "stream 3: type 0 then SETTINGS MAX_FIELD_SECTION_SIZE 100");
        Diagnostics.Bytes("server control stream bytes", [0x00, .. ServerSettings.ToBytes()]);
        FakeMultiplexedConnection quic = new(request) { ServerStreams = [control] };
        Http3Session session = new(quic);
        await session.OpenRequestStreamAsync(HttpFrameLog.Silent, CancellationToken.None);

        await session.DisposeAsync();

        Diagnostics.Act("request, control and connection disposed", string.Join(", ", request.IsDisposed, control.IsDisposed, quic.IsDisposed));
        Diagnostics.Act("close code", quic.CloseCode);
        Diagnostics.Assert("close code", 0x100L, quic.CloseCode);
        Assert.IsTrue(request.IsDisposed);
        Assert.IsTrue(quic.UnidirectionalStreams.TrueForAll(stream => stream.IsDisposed));
        Assert.IsTrue(control.IsDisposed, "the server's streams are disposed too");
        Assert.AreEqual(0x100L, quic.CloseCode);
        Assert.IsTrue(quic.IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsync_WhileWaitingForServerStreams_EndsTheAcceptLoop()
    {
        FakeMultiplexedStream control = ServerStream(3, [0x00, .. ServerSettings.ToBytes()]);
        Diagnostics.Arrange("server control stream", "stream 3: type 0 then SETTINGS, stays open");
        FakeMultiplexedConnection quic = new() { ServerStreams = [control] };
        Http3Session session = new(quic);
        await control.Drained;

        Diagnostics.Act("peer streams reading completed before dispose", session.PeerStreamsReading.IsCompleted);
        Assert.IsFalse(session.PeerStreamsReading.IsCompleted);
        await session.DisposeAsync();

        Diagnostics.Act("accept cancelled", quic.IsAcceptCancelled);
        Diagnostics.Assert("peer streams reading completed successfully", true, session.PeerStreamsReading.IsCompletedSuccessfully);
        Assert.IsTrue(session.PeerStreamsReading.IsCompletedSuccessfully);
        Assert.IsTrue(quic.IsAcceptCancelled);
        Assert.AreEqual(0x100L, quic.CloseCode);
    }

    [TestMethod]
    public async Task Connection_Session_IsSecureWithTheQuicEndpointsAndCarriesNoBytesItself()
    {
        IPEndPoint remote = new(IPAddress.Loopback, 443);
        IPEndPoint local = new(IPAddress.Loopback, 50000);
        Diagnostics.Arrange("remote and local endpoints", string.Join(", ", remote, local));
        Http3Session session = new(new FakeMultiplexedConnection { RemoteEndPoint = remote, LocalEndPoint = local });

        Diagnostics.Act("version name and using line", string.Join(", ", session.VersionName, session.UsingLine));
        Diagnostics.Assert("is secure", true, session.IsSecure);
        Assert.IsTrue(session.IsSecure);
        Assert.AreEqual(remote, session.RemoteEndPoint);
        Assert.AreEqual(local, session.LocalEndPoint);
        Assert.AreEqual("HTTP/3", session.VersionName);
        Assert.AreEqual("using HTTP/3", session.UsingLine);
        Assert.IsTrue(session.AcceptsNewStreams);
        await session.FlushAsync(CancellationToken.None);
        NotSupportedException readFailure = await Assert.ThrowsExactlyAsync<NotSupportedException>(async () => await session.ReadAsync(new byte[1], CancellationToken.None));
        NotSupportedException writeFailure = await Assert.ThrowsExactlyAsync<NotSupportedException>(async () => await session.WriteAsync(new byte[1], CancellationToken.None));
        Diagnostics.Act("read and write failures", string.Join(", ", readFailure.GetType().Name, writeFailure.GetType().Name));
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task AcceptsNewStreams_ServerSendsSettingsThenGoaway_TurnsFalseAndKeepsTheSettings()
    {
        TaskCompletionSource arrives = new();
        FakeMultiplexedStream control = new(3, [0x00, .. ServerSettings.ToBytes(), .. new Http3GoawayFrame(4).ToBytes()]) { ReadsAfter = arrives.Task, StaysOpen = true };
        Diagnostics.Arrange("server control stream", "stream 3: type 0, SETTINGS MAX_FIELD_SECTION_SIZE 100, GOAWAY 4");
        await using Http3Session session = new(new FakeMultiplexedConnection { ServerStreams = [control] });
        Assert.IsTrue(session.AcceptsNewStreams, "no GOAWAY yet");
        Assert.IsNull(session.PeerSettings);

        arrives.SetResult();
        await control.Drained;

        Diagnostics.Act("accepts new streams", session.AcceptsNewStreams);
        Diagnostics.Act("peer settings", string.Join(", ", session.PeerSettings!.Settings));
        Diagnostics.Assert("peer setting", new Http3Setting(0x06, 100), session.PeerSettings!.Settings.Single());
        Assert.IsFalse(session.AcceptsNewStreams);
        Assert.AreEqual(new Http3Setting(0x06, 100), session.PeerSettings!.Settings.Single());
        Assert.IsNull(session.ConnectionError, "a GOAWAY alone fails nothing");
    }

    [TestMethod]
    [DataRow(3L, 3, DisplayName = "the server's MAX_STREAMS")]
    [DataRow(5_000_000_000L, int.MaxValue, DisplayName = "a MAX_STREAMS past int, capped")]
    [DataRow(null, int.MaxValue, DisplayName = "unknown, unlimited")]
    public async Task ConcurrentTransferLimit_WhileTakingRequests_IsTheConnectionsStreamLimit(long? streamLimit, int expected)
    {
        Diagnostics.Arrange("bidirectional stream limit", streamLimit?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown");
        await using Http3Session session = new(new FakeMultiplexedConnection { BidirectionalStreamLimit = streamLimit });

        Diagnostics.Act("concurrent transfer limit", session.ConcurrentTransferLimit);
        Diagnostics.Assert("concurrent transfer limit", expected, session.ConcurrentTransferLimit);
        Assert.AreEqual(expected, session.ConcurrentTransferLimit);
    }

    [TestMethod]
    public async Task ConcurrentTransferLimit_AfterARefusedStream_IsZero()
    {
        Diagnostics.Arrange("bidirectional stream limit", 3);
        await using Http3Session session = new(new FakeMultiplexedConnection { BidirectionalStreamLimit = 3 });

        session.StopNewStreams();

        Diagnostics.Act("concurrent transfer limit after StopNewStreams", session.ConcurrentTransferLimit);
        Diagnostics.Assert("concurrent transfer limit", 0, session.ConcurrentTransferLimit);
        Assert.AreEqual(0, session.ConcurrentTransferLimit);
    }

    [TestMethod]
    public async Task ShutDownAsync_WritesNothingAndLeavesTheConnectionOpen()
    {
        Diagnostics.Arrange("connection", "fake QUIC connection with no streams");
        FakeMultiplexedConnection quic = new();
        await using Http3Session session = new(quic);

        await session.ShutDownAsync(CancellationToken.None);

        Diagnostics.Act("close code and unidirectional stream count", string.Join(", ", quic.CloseCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none", quic.UnidirectionalStreams.Count));
        Diagnostics.Assert("close code", null, quic.CloseCode);
        Assert.IsNull(quic.CloseCode);
        Assert.IsEmpty(quic.UnidirectionalStreams);
    }

    [TestMethod]
    public async Task OpenRequestStreamAsync_ThreeTransfersAtOnce_OpenTheUnidirectionalStreamsOnceAndStreams0And4And8()
    {
        Diagnostics.Arrange("request streams the connection hands out", "0, 4, 8");
        FakeMultiplexedConnection quic = new(new FakeMultiplexedStream(0, []), new FakeMultiplexedStream(4, []), new FakeMultiplexedStream(8, []));
        await using Http3Session session = new(quic);

        IMultiplexedStream[] opened = await Task.WhenAll(
            Enumerable.Range(0, 3).Select(_ => Task.Run(async () => await session.OpenRequestStreamAsync(HttpFrameLog.Silent, CancellationToken.None))));

        Diagnostics.Act("opened stream ids, sorted", string.Join(", ", opened.Select(stream => stream.StreamId).Order()));
        Diagnostics.Assert("unidirectional stream count", 3, quic.UnidirectionalStreams.Count);
        CollectionAssert.AreEquivalent(new long[] { 0, 4, 8 }, opened.Select(stream => stream.StreamId).ToArray());
        Assert.HasCount(3, quic.UnidirectionalStreams);
    }

    [TestMethod]
    public async Task OpenRequestStreamAsync_WhenCancelledWhileAnotherOpens_Throws()
    {
        Diagnostics.Arrange("cancellation token", "already cancelled");
        FakeMultiplexedConnection quic = new(new FakeMultiplexedStream(0, []));
        await using Http3Session session = new(quic);

        OperationCanceledException failure = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await session.OpenRequestStreamAsync(HttpFrameLog.Silent, new CancellationToken(canceled: true)));

        Diagnostics.Act("failure", failure.GetType().Name);
        Diagnostics.Assert("failure type", "OperationCanceledException or a type derived from it", failure.GetType().Name);
    }

    [TestMethod]
    public async Task StopNewStreams_RefusedStream_TurnsAcceptsNewStreamsFalse()
    {
        Diagnostics.Arrange("connection", "fake QUIC connection with no streams");
        await using Http3Session session = new(new FakeMultiplexedConnection());

        session.StopNewStreams();

        Diagnostics.Act("accepts new streams", session.AcceptsNewStreams);
        Diagnostics.Assert("accepts new streams", false, session.AcceptsNewStreams);
        Assert.IsFalse(session.AcceptsNewStreams);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x00, 0x07, 0x01, 0x00 }, "ERR_H3_MISSING_SETTINGS", 0x10aL, DisplayName = "a GOAWAY before SETTINGS")]
    [DataRow(new byte[] { 0x00 }, "ERR_H3_CLOSED_CRITICAL_STREAM", 0x104L, DisplayName = "the control stream ends")]
    [DataRow(new byte[] { 0x00, 0x04, 0x00, 0x04, 0x00 }, "ERR_H3_FRAME_UNEXPECTED", 0x105L, DisplayName = "a second SETTINGS")]
    [DataRow(new byte[] { 0x01 }, "ERR_H3_ID_ERROR", 0x108L, DisplayName = "a push stream")]
    [DataRow(new byte[] { 0x02, 0x21 }, "ERR_QPACK_ENCODER_STREAM_ERROR", 0x201L, DisplayName = "a QPACK encoder stream above the table capacity")]
    [DataRow(new byte[] { 0x02 }, "ERR_H3_CLOSED_CRITICAL_STREAM", 0x104L, DisplayName = "the QPACK encoder stream ends")]
    [DataRow(new byte[] { 0x03, 0x00 }, "ERR_QPACK_DECODER_STREAM_ERROR", 0x202L, DisplayName = "a QPACK decoder stream increment of zero")]
    public async Task ReadAsync_ServerStreamBreaksHttp3_FailsWithExit56AndClosesWithItsCode(byte[] incoming, string errorName, long closeCode)
    {
        Diagnostics.Bytes("server stream 3 bytes", incoming);
        Diagnostics.Arrange("expected error and close code", $"{errorName}, 0x{closeCode:x}");
        FakeMultiplexedConnection quic = new() { ServerStreams = [new FakeMultiplexedStream(3, incoming)] };
        Http3Session session = new(quic);
        await session.PeerStreamsReading;

        HttpTransferException failure = await ReadFailureAsync(session);

        Diagnostics.Act("read failure", failure.Message);
        Diagnostics.Act("exit code and close code", $"{failure.ExitCode}, 0x{quic.CloseCode:x}");
        Diagnostics.Assert("read failure", $"nghttp3_conn_read_stream returned error: {errorName}", failure.Message);
        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual($"nghttp3_conn_read_stream returned error: {errorName}", failure.Message);
        Assert.IsFalse(session.AcceptsNewStreams);
        Assert.AreEqual(closeCode, quic.CloseCode);
        await session.DisposeAsync();
        Assert.AreEqual(closeCode, quic.CloseCode, "not closed again");
        Assert.IsTrue(quic.IsDisposed);
    }

    [TestMethod]
    public async Task ReadAsync_ServerResetsItsControlStream_FailsWithClosedCriticalStream()
    {
        FakeMultiplexedStream control = new(3, [0x00, .. ServerSettings.ToBytes()]) { EndException = new MultiplexedStreamResetException(0x100, "reset") };
        Diagnostics.Arrange("server control stream", "stream 3: type 0, SETTINGS, then reset with 0x100");
        FakeMultiplexedConnection quic = new() { ServerStreams = [control] };
        await using Http3Session session = new(quic);
        await session.PeerStreamsReading;

        HttpTransferException failure = await ReadFailureAsync(session);

        Diagnostics.Act("read failure", failure.Message);
        Diagnostics.Act("close code", $"0x{quic.CloseCode:x}");
        Diagnostics.Assert("read failure", "nghttp3_conn_read_stream returned error: ERR_H3_CLOSED_CRITICAL_STREAM", failure.Message);
        Assert.AreEqual("nghttp3_conn_read_stream returned error: ERR_H3_CLOSED_CRITICAL_STREAM", failure.Message);
        Assert.AreEqual(0x104L, quic.CloseCode);
    }

    [TestMethod]
    public async Task ReadAsync_SecondControlStream_FailsWithStreamCreationError()
    {
        FakeMultiplexedStream first = ServerStream(3, [0x00, .. ServerSettings.ToBytes()]);
        Diagnostics.Arrange("server streams", "stream 3: control with SETTINGS; stream 7: a second control stream");
        FakeMultiplexedConnection quic = new() { ServerStreams = [first, new FakeMultiplexedStream(7, [0x00])] };
        await using Http3Session session = new(quic);
        await session.PeerStreamsReading;

        HttpTransferException failure = await ReadFailureAsync(session);

        Diagnostics.Act("read failure", failure.Message);
        Diagnostics.Act("exit code and close code", $"{failure.ExitCode}, 0x{quic.CloseCode:x}");
        Diagnostics.Assert("read failure", "nghttp3_conn_read_stream returned error: ERR_H3_STREAM_CREATION_ERROR", failure.Message);
        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual("nghttp3_conn_read_stream returned error: ERR_H3_STREAM_CREATION_ERROR", failure.Message);
        Assert.AreEqual(0x103L, quic.CloseCode);
    }

    [TestMethod]
    public async Task ReadAsync_TwoServerStreamsFail_TheFirstFailureWins()
    {
        TaskCompletionSource encoderArrives = new();
        TaskCompletionSource decoderArrives = new();
        Diagnostics.Arrange("server streams", "stream 3: QPACK encoder 02 21, arrives first; stream 7: QPACK decoder 03 00, arrives second");
        FakeMultiplexedConnection quic = new()
        {
            ServerStreams =
            [
                new FakeMultiplexedStream(3, [0x02, 0x21]) { ReadsAfter = encoderArrives.Task },
                new FakeMultiplexedStream(7, [0x03, 0x00]) { ReadsAfter = decoderArrives.Task },
            ],
        };
        await using Http3Session session = new(quic);

        encoderArrives.SetResult();
        decoderArrives.SetResult();
        await session.PeerStreamsReading;

        HttpTransferException failure = await ReadFailureAsync(session);
        Diagnostics.Act("read failure", failure.Message);
        Diagnostics.Act("close code", $"0x{quic.CloseCode:x}");
        Diagnostics.Assert("read failure", "nghttp3_conn_read_stream returned error: ERR_QPACK_ENCODER_STREAM_ERROR", failure.Message);
        Assert.AreEqual("nghttp3_conn_read_stream returned error: ERR_QPACK_ENCODER_STREAM_ERROR", failure.Message);
        Assert.AreEqual(0x201L, quic.CloseCode);
    }

    [TestMethod]
    public async Task ReadAsync_ConnectionErrorWhileTheRequestWaits_FailsTheWaitingRead()
    {
        TaskCompletionSource controlArrives = new();
        TaskCompletionSource responseArrives = new();
        FakeMultiplexedStream request = new(0, []) { ReadsAfter = responseArrives.Task, EndException = new MultiplexedConnectionFailedException(CurlExitCode.RecvError, "closed") };
        FakeMultiplexedConnection quic = new(request)
        {
            ServerStreams = [new FakeMultiplexedStream(3, [0x00, .. new Http3GoawayFrame(0).ToBytes()]) { ReadsAfter = controlArrives.Task }],
        };
        await using Http3Session session = new(quic);
        IHttpStreamConnection stream = session.CreateStream("https", 0, ignoresBody: false);
        byte[] requestBytes = Encoding.Latin1.GetBytes("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n");
        Diagnostics.Arrange("server control stream", "stream 3: type 0 then GOAWAY 0, before any SETTINGS");
        Diagnostics.Bytes("request written", requestBytes);
        await stream.WriteAsync(requestBytes, CancellationToken.None);
        Task<int> read = stream.ReadAsync(new byte[16], CancellationToken.None).AsTask();

        controlArrives.SetResult();
        responseArrives.SetResult();

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(() => read);
        Diagnostics.Act("read failure", failure.Message);
        Diagnostics.Assert("exit code", CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual("nghttp3_conn_read_stream returned error: ERR_H3_MISSING_SETTINGS", failure.Message);
    }

    [TestMethod]
    public async Task PeerStreamsReading_QpackStreamsWithinTheLimits_FeedTheQpackContextsWithoutFailing()
    {
        FakeMultiplexedStream encoder = ServerStream(3, [0x02, 0x20]);
        FakeMultiplexedStream decoder = ServerStream(7, [0x03]);
        Diagnostics.Arrange("server streams", "stream 3: QPACK encoder 02 20; stream 7: QPACK decoder 03");
        FakeMultiplexedConnection quic = new() { ServerStreams = [encoder, decoder] };
        await using Http3Session session = new(quic);

        await encoder.Drained;
        await decoder.Drained;

        Diagnostics.Act("connection error and accepts new streams", string.Join(", ", session.ConnectionError?.Message ?? "none", session.AcceptsNewStreams));
        Diagnostics.Assert("connection error", null, session.ConnectionError);
        Assert.IsNull(session.ConnectionError);
        Assert.IsTrue(session.AcceptsNewStreams);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x21 }, DisplayName = "a grease type")]
    [DataRow(new byte[0], DisplayName = "no type before the stream ends")]
    public async Task PeerStreamsReading_StreamOfNoKnownType_AbortsItWithStreamCreationError(byte[] incoming)
    {
        Diagnostics.Arrange("server stream 3 byte count", incoming.Length);
        Diagnostics.Bytes("server stream 3 bytes", incoming);
        FakeMultiplexedStream unknown = new(3, incoming);
        FakeMultiplexedConnection quic = new() { ServerStreams = [unknown] };
        Http3Session session = new(quic);

        await session.DisposeAsync();

        Diagnostics.Act("stream abort code and connection close code", $"0x{unknown.AbortCode:x}, 0x{quic.CloseCode:x}");
        Diagnostics.Assert("stream abort code", 0x103L, unknown.AbortCode);
        Assert.AreEqual(0x103L, unknown.AbortCode);
        Assert.AreEqual(0x100L, quic.CloseCode, "not a connection error");
    }

    [TestMethod]
    public async Task PeerStreamsReading_StreamResetBeforeItsType_IsDropped()
    {
        Diagnostics.Arrange("server stream", "stream 3: reset with 0x10c before any byte");
        FakeMultiplexedConnection quic = new() { ServerStreams = [new FakeMultiplexedStream(3, []) { EndException = new MultiplexedStreamResetException(0x10c, "cancelled") }] };
        Http3Session session = new(quic);

        await session.DisposeAsync();

        Diagnostics.Act("connection error and close code", $"{session.ConnectionError?.Message ?? "none"}, 0x{quic.CloseCode:x}");
        Diagnostics.Assert("close code", 0x100L, quic.CloseCode);
        Assert.IsNull(session.ConnectionError);
        Assert.AreEqual(0x100L, quic.CloseCode);
    }

    [TestMethod]
    public async Task PeerStreamsReading_ConnectionLost_EndsWithoutAConnectionError()
    {
        MultiplexedConnectionFailedException lost = new(CurlExitCode.RecvError, "lost");
        Diagnostics.Arrange("connection failure on the server stream and on accept", lost.Message);
        FakeMultiplexedConnection quic = new()
        {
            ServerStreams = [new FakeMultiplexedStream(3, [0x00]) { EndException = lost }],
            AcceptException = lost,
        };
        Http3Session session = new(quic);

        await session.PeerStreamsReading;

        Diagnostics.Act("connection error and accepts new streams", string.Join(", ", session.ConnectionError?.Message ?? "none", session.AcceptsNewStreams));
        Diagnostics.Assert("connection error", null, session.ConnectionError);
        Assert.IsNull(session.ConnectionError);
        Assert.IsTrue(session.AcceptsNewStreams);
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task PeerStreamsReading_ConnectionHandsOutNoServerStreams_EndsWithoutAConnectionError()
    {
        Diagnostics.Arrange("accept failure", nameof(NotSupportedException));
        Http3Session session = new(new FakeMultiplexedConnection { AcceptException = new NotSupportedException() });

        await session.PeerStreamsReading;

        Diagnostics.Act("connection error", session.ConnectionError?.Message ?? "none");
        Diagnostics.Assert("connection error", null, session.ConnectionError);
        Assert.IsNull(session.ConnectionError);
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task PeerStreamsReading_ClosingAfterAConnectionErrorFindsTheConnectionLost_StillFailsTheRead()
    {
        Diagnostics.Arrange("server stream and close failure", "stream 3: push stream type 01; close throws connection lost");
        FakeMultiplexedConnection quic = new()
        {
            ServerStreams = [new FakeMultiplexedStream(3, [0x01])],
            CloseException = new MultiplexedConnectionFailedException(CurlExitCode.RecvError, "lost"),
        };
        Http3Session session = new(quic);

        await session.PeerStreamsReading;

        HttpTransferException failure = await ReadFailureAsync(session);
        Diagnostics.Act("read failure", failure.Message);
        Diagnostics.Assert("read failure", "nghttp3_conn_read_stream returned error: ERR_H3_ID_ERROR", failure.Message);
        Assert.AreEqual("nghttp3_conn_read_stream returned error: ERR_H3_ID_ERROR", failure.Message);
        await session.DisposeAsync();
        Diagnostics.Act("close code", quic.CloseCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none");
        Assert.IsNull(quic.CloseCode);
    }

    /// <summary>A server stream that stays open once its bytes are read.</summary>
    [TestMethod]
    public async Task LogConnectionFrame_AfterATransferOpenedAStream_LogsSettingsAndGoawayToItAndNoOtherFrame()
    {
        RecordingDiagnosticLog log = new();
        Diagnostics.Arrange("connection frames logged", "CANCEL_PUSH 1, SETTINGS MAX_FIELD_SECTION_SIZE 100, GOAWAY 8");
        Http3Session session = new(new FakeMultiplexedConnection(new FakeMultiplexedStream(0, [])));
        await session.OpenRequestStreamAsync(new HttpFrameLog(log, DiagnosticLogComponents.Http3), CancellationToken.None);

        session.LogConnectionFrame(new Http3CancelPushFrame(1));
        session.LogConnectionFrame(ServerSettings);
        session.LogConnectionFrame(new Http3GoawayFrame(8));

        string[] messages = log.Lines.Select(line => line.Message).ToArray();
        Diagnostics.Act("log lines", string.Join(" | ", messages));
        Diagnostics.Assert("log lines", "SETTINGS received: MAX_FIELD_SECTION_SIZE 100 | GOAWAY received: stream 8", string.Join(" | ", messages));
        CollectionAssert.AreEqual(
            new[] { "SETTINGS received: MAX_FIELD_SECTION_SIZE 100", "GOAWAY received: stream 8" },
            log.Lines.Select(line => line.Message).ToArray());
    }

    [TestMethod]
    public async Task OpenRequestStreamAsync_SecondTransfer_TakesTheConnectionsLaterFrames()
    {
        RecordingDiagnosticLog first = new();
        RecordingDiagnosticLog second = new();
        Diagnostics.Arrange("connection frames", "SETTINGS before both transfers, GOAWAY 8 after the second opened");
        Http3Session session = new(new FakeMultiplexedConnection(new FakeMultiplexedStream(0, []), new FakeMultiplexedStream(4, [])));
        session.LogConnectionFrame(ServerSettings);
        await session.OpenRequestStreamAsync(new HttpFrameLog(first, DiagnosticLogComponents.Http3), CancellationToken.None);
        await session.OpenRequestStreamAsync(new HttpFrameLog(second, DiagnosticLogComponents.Http3), CancellationToken.None);

        session.LogConnectionFrame(new Http3GoawayFrame(8));

        string firstLines = string.Join(" | ", first.Lines.Select(line => line.Message));
        string secondLines = string.Join(" | ", second.Lines.Select(line => line.Message));
        Diagnostics.Act("first transfer log lines", firstLines);
        Diagnostics.Act("second transfer log lines", secondLines);
        Diagnostics.Assert("second transfer log lines", "GOAWAY received: stream 8", secondLines);
        CollectionAssert.AreEqual(new[] { "SETTINGS received: MAX_FIELD_SECTION_SIZE 100" }, first.Lines.Select(line => line.Message).ToArray());
        CollectionAssert.AreEqual(new[] { "GOAWAY received: stream 8" }, second.Lines.Select(line => line.Message).ToArray());
    }

    private static FakeMultiplexedStream ServerStream(long streamId, byte[] incoming) => new(streamId, incoming) { StaysOpen = true };

    private static async Task<HttpTransferException> ReadFailureAsync(Http3Session session)
    {
        IHttpStreamConnection stream = session.CreateStream("https", 0, ignoresBody: false);
        return await Assert.ThrowsExactlyAsync<HttpTransferException>(async () => await stream.ReadAsync(new byte[16], CancellationToken.None));
    }
}
