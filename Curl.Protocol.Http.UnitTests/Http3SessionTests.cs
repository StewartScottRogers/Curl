using System.Net;
using System.Text;
using Curl.Http3;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

[TestClass]
public sealed class Http3SessionTests
{
    /// <summary>The server's SETTINGS in the tests: a field section limit of 100 bytes.</summary>
    private static readonly Http3SettingsFrame ServerSettings = new([new Http3Setting(0x06, 100)]);

    [TestMethod]
    public async Task OpenRequestStreamAsync_SecondRequest_OpensNoMoreUnidirectionalStreams()
    {
        FakeMultiplexedConnection quic = new(new FakeMultiplexedStream(0, []), new FakeMultiplexedStream(4, []));
        Http3Session session = new(quic);

        await session.OpenRequestStreamAsync(CancellationToken.None);
        var second = await session.OpenRequestStreamAsync(CancellationToken.None);

        Assert.AreEqual(4L, second.StreamId);
        Assert.HasCount(3, quic.UnidirectionalStreams);
        CollectionAssert.AreEqual(new long[] { 2, 6, 10 }, quic.UnidirectionalStreams.Select(stream => stream.StreamId).ToArray());
    }

    [TestMethod]
    public async Task DisposeAsync_AfterARequest_DisposesEveryStreamAndClosesWithNoError()
    {
        FakeMultiplexedStream request = new(0, []);
        FakeMultiplexedStream control = ServerStream(3, [0x00, .. ServerSettings.ToBytes()]);
        FakeMultiplexedConnection quic = new(request) { ServerStreams = [control] };
        Http3Session session = new(quic);
        await session.OpenRequestStreamAsync(CancellationToken.None);

        await session.DisposeAsync();

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
        FakeMultiplexedConnection quic = new() { ServerStreams = [control] };
        Http3Session session = new(quic);
        await control.Drained;

        Assert.IsFalse(session.PeerStreamsReading.IsCompleted);
        await session.DisposeAsync();

        Assert.IsTrue(session.PeerStreamsReading.IsCompletedSuccessfully);
        Assert.IsTrue(quic.IsAcceptCancelled);
        Assert.AreEqual(0x100L, quic.CloseCode);
    }

    [TestMethod]
    public async Task Connection_Session_IsSecureWithTheQuicEndpointsAndCarriesNoBytesItself()
    {
        IPEndPoint remote = new(IPAddress.Loopback, 443);
        IPEndPoint local = new(IPAddress.Loopback, 50000);
        Http3Session session = new(new FakeMultiplexedConnection { RemoteEndPoint = remote, LocalEndPoint = local });

        Assert.IsTrue(session.IsSecure);
        Assert.AreEqual(remote, session.RemoteEndPoint);
        Assert.AreEqual(local, session.LocalEndPoint);
        Assert.AreEqual("HTTP/3", session.VersionName);
        Assert.AreEqual("using HTTP/3", session.UsingLine);
        Assert.IsTrue(session.AcceptsNewStreams);
        await session.FlushAsync(CancellationToken.None);
        await Assert.ThrowsExactlyAsync<NotSupportedException>(async () => await session.ReadAsync(new byte[1], CancellationToken.None));
        await Assert.ThrowsExactlyAsync<NotSupportedException>(async () => await session.WriteAsync(new byte[1], CancellationToken.None));
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task AcceptsNewStreams_ServerSendsSettingsThenGoaway_TurnsFalseAndKeepsTheSettings()
    {
        TaskCompletionSource arrives = new();
        FakeMultiplexedStream control = new(3, [0x00, .. ServerSettings.ToBytes(), .. new Http3GoawayFrame(4).ToBytes()]) { ReadsAfter = arrives.Task, StaysOpen = true };
        await using Http3Session session = new(new FakeMultiplexedConnection { ServerStreams = [control] });
        Assert.IsTrue(session.AcceptsNewStreams, "no GOAWAY yet");
        Assert.IsNull(session.PeerSettings);

        arrives.SetResult();
        await control.Drained;

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
        await using Http3Session session = new(new FakeMultiplexedConnection { BidirectionalStreamLimit = streamLimit });

        Assert.AreEqual(expected, session.ConcurrentTransferLimit);
    }

    [TestMethod]
    public async Task ConcurrentTransferLimit_AfterARefusedStream_IsZero()
    {
        await using Http3Session session = new(new FakeMultiplexedConnection { BidirectionalStreamLimit = 3 });

        session.StopNewStreams();

        Assert.AreEqual(0, session.ConcurrentTransferLimit);
    }

    [TestMethod]
    public async Task ShutDownAsync_WritesNothingAndLeavesTheConnectionOpen()
    {
        FakeMultiplexedConnection quic = new();
        await using Http3Session session = new(quic);

        await session.ShutDownAsync(CancellationToken.None);

        Assert.IsNull(quic.CloseCode);
        Assert.IsEmpty(quic.UnidirectionalStreams);
    }

    [TestMethod]
    public async Task OpenRequestStreamAsync_ThreeTransfersAtOnce_OpenTheUnidirectionalStreamsOnceAndStreams0And4And8()
    {
        FakeMultiplexedConnection quic = new(new FakeMultiplexedStream(0, []), new FakeMultiplexedStream(4, []), new FakeMultiplexedStream(8, []));
        await using Http3Session session = new(quic);

        IMultiplexedStream[] opened = await Task.WhenAll(
            Enumerable.Range(0, 3).Select(_ => Task.Run(async () => await session.OpenRequestStreamAsync(CancellationToken.None))));

        CollectionAssert.AreEquivalent(new long[] { 0, 4, 8 }, opened.Select(stream => stream.StreamId).ToArray());
        Assert.HasCount(3, quic.UnidirectionalStreams);
    }

    [TestMethod]
    public async Task OpenRequestStreamAsync_WhenCancelledWhileAnotherOpens_Throws()
    {
        FakeMultiplexedConnection quic = new(new FakeMultiplexedStream(0, []));
        await using Http3Session session = new(quic);

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await session.OpenRequestStreamAsync(new CancellationToken(canceled: true)));
    }

    [TestMethod]
    public async Task StopNewStreams_RefusedStream_TurnsAcceptsNewStreamsFalse()
    {
        await using Http3Session session = new(new FakeMultiplexedConnection());

        session.StopNewStreams();

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
        FakeMultiplexedConnection quic = new() { ServerStreams = [new FakeMultiplexedStream(3, incoming)] };
        Http3Session session = new(quic);
        await session.PeerStreamsReading;

        HttpTransferException failure = await ReadFailureAsync(session);

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
        FakeMultiplexedConnection quic = new() { ServerStreams = [control] };
        await using Http3Session session = new(quic);
        await session.PeerStreamsReading;

        HttpTransferException failure = await ReadFailureAsync(session);

        Assert.AreEqual("nghttp3_conn_read_stream returned error: ERR_H3_CLOSED_CRITICAL_STREAM", failure.Message);
        Assert.AreEqual(0x104L, quic.CloseCode);
    }

    [TestMethod]
    public async Task ReadAsync_SecondControlStream_FailsWithStreamCreationError()
    {
        FakeMultiplexedStream first = ServerStream(3, [0x00, .. ServerSettings.ToBytes()]);
        FakeMultiplexedConnection quic = new() { ServerStreams = [first, new FakeMultiplexedStream(7, [0x00])] };
        await using Http3Session session = new(quic);
        await session.PeerStreamsReading;

        HttpTransferException failure = await ReadFailureAsync(session);

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual("nghttp3_conn_read_stream returned error: ERR_H3_STREAM_CREATION_ERROR", failure.Message);
        Assert.AreEqual(0x103L, quic.CloseCode);
    }

    [TestMethod]
    public async Task ReadAsync_TwoServerStreamsFail_TheFirstFailureWins()
    {
        TaskCompletionSource encoderArrives = new();
        TaskCompletionSource decoderArrives = new();
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

        Assert.AreEqual("nghttp3_conn_read_stream returned error: ERR_QPACK_ENCODER_STREAM_ERROR", (await ReadFailureAsync(session)).Message);
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
        await stream.WriteAsync(Encoding.Latin1.GetBytes("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n"), CancellationToken.None);
        Task<int> read = stream.ReadAsync(new byte[16], CancellationToken.None).AsTask();

        controlArrives.SetResult();
        responseArrives.SetResult();

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(() => read);
        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual("nghttp3_conn_read_stream returned error: ERR_H3_MISSING_SETTINGS", failure.Message);
    }

    [TestMethod]
    public async Task PeerStreamsReading_QpackStreamsWithinTheLimits_FeedTheQpackContextsWithoutFailing()
    {
        FakeMultiplexedStream encoder = ServerStream(3, [0x02, 0x20]);
        FakeMultiplexedStream decoder = ServerStream(7, [0x03]);
        FakeMultiplexedConnection quic = new() { ServerStreams = [encoder, decoder] };
        await using Http3Session session = new(quic);

        await encoder.Drained;
        await decoder.Drained;

        Assert.IsNull(session.ConnectionError);
        Assert.IsTrue(session.AcceptsNewStreams);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x21 }, DisplayName = "a grease type")]
    [DataRow(new byte[0], DisplayName = "no type before the stream ends")]
    public async Task PeerStreamsReading_StreamOfNoKnownType_AbortsItWithStreamCreationError(byte[] incoming)
    {
        FakeMultiplexedStream unknown = new(3, incoming);
        FakeMultiplexedConnection quic = new() { ServerStreams = [unknown] };
        Http3Session session = new(quic);

        await session.DisposeAsync();

        Assert.AreEqual(0x103L, unknown.AbortCode);
        Assert.AreEqual(0x100L, quic.CloseCode, "not a connection error");
    }

    [TestMethod]
    public async Task PeerStreamsReading_StreamResetBeforeItsType_IsDropped()
    {
        FakeMultiplexedConnection quic = new() { ServerStreams = [new FakeMultiplexedStream(3, []) { EndException = new MultiplexedStreamResetException(0x10c, "cancelled") }] };
        Http3Session session = new(quic);

        await session.DisposeAsync();

        Assert.IsNull(session.ConnectionError);
        Assert.AreEqual(0x100L, quic.CloseCode);
    }

    [TestMethod]
    public async Task PeerStreamsReading_ConnectionLost_EndsWithoutAConnectionError()
    {
        MultiplexedConnectionFailedException lost = new(CurlExitCode.RecvError, "lost");
        FakeMultiplexedConnection quic = new()
        {
            ServerStreams = [new FakeMultiplexedStream(3, [0x00]) { EndException = lost }],
            AcceptException = lost,
        };
        Http3Session session = new(quic);

        await session.PeerStreamsReading;

        Assert.IsNull(session.ConnectionError);
        Assert.IsTrue(session.AcceptsNewStreams);
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task PeerStreamsReading_ConnectionHandsOutNoServerStreams_EndsWithoutAConnectionError()
    {
        Http3Session session = new(new FakeMultiplexedConnection { AcceptException = new NotSupportedException() });

        await session.PeerStreamsReading;

        Assert.IsNull(session.ConnectionError);
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task PeerStreamsReading_ClosingAfterAConnectionErrorFindsTheConnectionLost_StillFailsTheRead()
    {
        FakeMultiplexedConnection quic = new()
        {
            ServerStreams = [new FakeMultiplexedStream(3, [0x01])],
            CloseException = new MultiplexedConnectionFailedException(CurlExitCode.RecvError, "lost"),
        };
        Http3Session session = new(quic);

        await session.PeerStreamsReading;

        Assert.AreEqual("nghttp3_conn_read_stream returned error: ERR_H3_ID_ERROR", (await ReadFailureAsync(session)).Message);
        await session.DisposeAsync();
        Assert.IsNull(quic.CloseCode);
    }

    /// <summary>A server stream that stays open once its bytes are read.</summary>
    private static FakeMultiplexedStream ServerStream(long streamId, byte[] incoming) => new(streamId, incoming) { StaysOpen = true };

    private static async Task<HttpTransferException> ReadFailureAsync(Http3Session session)
    {
        IHttpStreamConnection stream = session.CreateStream("https", 0, ignoresBody: false);
        return await Assert.ThrowsExactlyAsync<HttpTransferException>(async () => await stream.ReadAsync(new byte[16], CancellationToken.None));
    }
}
