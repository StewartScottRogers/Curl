using Curl.Protocol.Abstractions;

namespace Curl.Http3;

/// <summary>
/// Pins <see cref="Http3ClientSession" /> over an in-memory connection: the local streams
/// opened before the first request stream, the peer's SETTINGS and GOAWAY, extended CONNECT,
/// stream accounting, the peer stream failures kept as the connection error, and closing.
/// </summary>
[TestClass]
public sealed class Http3ClientSessionTests
{
    private static readonly Http3Setting[] LocalSettings = [new(Http3SettingIdentifier.QpackMaximumTableCapacity, 0)];

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3ClientSession(null!, LocalSettings));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3ClientSession(new InMemoryMultiplexedConnection(), null!));
    }

    [TestMethod]
    public async Task OpenRequestStreamAsync_FirstAndSecond_OpensLocalStreamsOnceBeforeTheFirst()
    {
        InMemoryMultiplexedConnection connection = new();
        await using Http3ClientSession session = new(connection, LocalSettings);
        int readyCalls = 0;

        Http3ClientRequestStream first = await session.OpenRequestStreamAsync(() => readyCalls++, CancellationToken.None);
        Http3ClientRequestStream second = await session.OpenRequestStreamAsync(null, CancellationToken.None);

        Assert.AreEqual(1, readyCalls);
        Assert.AreEqual(0, first.StreamId);
        Assert.AreEqual(4, second.StreamId);
        Assert.HasCount(3, connection.UnidirectionalStreams);
        CollectionAssert.AreEqual(Qpack.FromHex("00 04 02 01 00"), connection.UnidirectionalStreams[0].Written.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x02 }, connection.UnidirectionalStreams[1].Written.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x03 }, connection.UnidirectionalStreams[2].Written.ToArray());
    }

    [TestMethod]
    public async Task OpenRequestStreamAsync_ConnectionRefusesStream_ThrowsItsIOException()
    {
        InMemoryMultiplexedConnection connection = new() { BidirectionalOpenFailure = new IOException("refused") };
        await using Http3ClientSession session = new(connection, LocalSettings);

        await Assert.ThrowsExactlyAsync<IOException>(async () => await session.OpenRequestStreamAsync(null, CancellationToken.None));
    }

    [TestMethod]
    public async Task EndRequestStream_WithAndWithoutLimit_ReportsStreamsLeftAndInUse()
    {
        InMemoryMultiplexedConnection limited = new() { BidirectionalStreamLimit = 10 };
        await using Http3ClientSession session = new(limited, LocalSettings);
        await session.OpenRequestStreamAsync(null, CancellationToken.None);
        await session.OpenRequestStreamAsync(null, CancellationToken.None);
        await using Http3ClientSession unlimited = new(new InMemoryMultiplexedConnection(), LocalSettings);
        await unlimited.OpenRequestStreamAsync(null, CancellationToken.None);

        (long? left, int inUse) = session.EndRequestStream();
        (long? noLimit, int noneInUse) = unlimited.EndRequestStream();

        Assert.AreEqual(8, left);
        Assert.AreEqual(1, inUse);
        Assert.IsNull(noLimit);
        Assert.AreEqual(0, noneInUse);
    }

    [TestMethod]
    public async Task PeerSettings_EnableConnectProtocolOne_IsExtendedConnectEnabled()
    {
        (Http3ClientSession session, List<Http3Frame> frames) = await SessionWithControlStreamAsync("00 04 02 08 01");
        await using (session)
        {
            Assert.IsTrue(session.IsExtendedConnectEnabled);
            Assert.IsInstanceOfType<Http3SettingsFrame>(frames[0]);
            Assert.AreSame(frames[0], session.PeerSettings);
            Assert.IsTrue(session.AcceptsNewStreams);
        }
    }

    [TestMethod]
    public async Task PeerSettings_EnableConnectProtocolMissing_IsNotExtendedConnectEnabled()
    {
        await using Http3ClientSession before = new(new InMemoryMultiplexedConnection(), LocalSettings);
        (Http3ClientSession session, _) = await SessionWithControlStreamAsync("00 04 02 08 00");
        await using (session)
        {
            Assert.IsFalse(before.IsExtendedConnectEnabled);
            Assert.IsNull(before.PeerSettings);
            Assert.IsFalse(session.IsExtendedConnectEnabled);
        }
    }

    [TestMethod]
    public async Task GoawayStreamId_PeerSendsGoaway_StopsNewStreams()
    {
        (Http3ClientSession session, List<Http3Frame> frames) = await SessionWithControlStreamAsync("00 04 00 07 01 04", frameCount: 2);
        await using (session)
        {
            Assert.AreEqual(4, session.GoawayStreamId);
            Assert.IsInstanceOfType<Http3GoawayFrame>(frames[1]);
            Assert.IsFalse(session.AcceptsNewStreams);
        }
    }

    [TestMethod]
    public async Task StopNewStreams_AfterRefusal_StopsNewStreams()
    {
        await using Http3ClientSession session = new(new InMemoryMultiplexedConnection(), LocalSettings);

        session.StopNewStreams();

        Assert.IsFalse(session.AcceptsNewStreams);
    }

    [TestMethod]
    public async Task PeerStreams_WithoutCallback_ReadsSettings()
    {
        InMemoryMultiplexedConnection connection = new();
        connection.AddPeerStream(Qpack.FromHex("00 04 02 08 01")).EndReceiving();
        Http3ClientSession session = new(connection, LocalSettings);

        await session.PeerStreamsReading;

        Assert.IsTrue(session.IsExtendedConnectEnabled);
        Assert.IsInstanceOfType<Http3Exception>(session.ConnectionError);
        await session.DisposeAsync();
    }

    [TestMethod]
    [DataRow("01", Http3ErrorCode.IdError, DisplayName = "push stream")]
    [DataRow("03", Http3ErrorCode.ClosedCriticalStream, DisplayName = "QPACK decoder stream ends")]
    [DataRow("00 07 01 00", Http3ErrorCode.MissingSettings, DisplayName = "control stream without SETTINGS")]
    public async Task PeerStreams_Violation_IsConnectionErrorAndClosesTheConnection(string hex, Http3ErrorCode expected)
    {
        InMemoryMultiplexedConnection connection = new();
        connection.AddPeerStream(Qpack.FromHex(hex)).EndReceiving();
        connection.AddPeerStream(0x01).EndReceiving();
        Http3ClientSession session = new(connection, LocalSettings);

        await session.PeerStreamsReading;
        await session.DisposeAsync();

        Assert.AreEqual(expected, ((Http3Exception)session.ConnectionError!).ErrorCode);
        Assert.AreEqual((long)expected, connection.ClosedWith);
        Assert.IsFalse(session.AcceptsNewStreams);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task PeerStreams_BadEncoderInstruction_IsQpackConnectionError()
    {
        InMemoryMultiplexedConnection connection = new();
        connection.AddPeerStream(0x02, 0x21);
        Http3ClientSession session = new(connection, LocalSettings);

        await session.PeerStreamsReading;
        await session.DisposeAsync();

        QpackException error = (QpackException)session.ConnectionError!;
        Assert.AreEqual((long)error.ErrorCode, connection.ClosedWith);
    }

    [TestMethod]
    public async Task PeerStreams_CriticalStreamReset_IsClosedCriticalStream()
    {
        InMemoryMultiplexedConnection connection = new();
        connection.AddPeerStream(0x00).FailReceiving(new MultiplexedStreamResetException(0x100, "reset"));
        Http3ClientSession session = new(connection, LocalSettings);

        await session.PeerStreamsReading;
        await session.DisposeAsync();

        Assert.AreEqual(Http3ErrorCode.ClosedCriticalStream, ((Http3Exception)session.ConnectionError!).ErrorCode);
    }

    [TestMethod]
    public async Task PeerStreams_ResetBeforeTypeOrLostOrUnknownType_IsNoConnectionError()
    {
        InMemoryMultiplexedConnection connection = new() { CloseFailure = new IOException("lost") };
        connection.AddPeerStream().FailReceiving(new MultiplexedStreamResetException(0x100, "reset"));
        connection.AddPeerStream(0x02).FailReceiving(new MultiplexedConnectionFailedException(CurlExitCode.RecvError, "lost"));
        InMemoryMultiplexedStream grease = connection.AddPeerStream(0x21);
        connection.FailAccepting(new MultiplexedConnectionFailedException(CurlExitCode.RecvError, "lost"));
        Http3ClientSession session = new(connection, LocalSettings);

        await session.PeerStreamsReading;
        await session.DisposeAsync();

        Assert.IsNull(session.ConnectionError);
        Assert.AreEqual((long)Http3ErrorCode.StreamCreationError, grease.AbortedWith);
        Assert.AreEqual((long)Http3ErrorCode.NoError, connection.ClosedWith);
        Assert.IsTrue(grease.IsDisposed);
    }

    [TestMethod]
    public async Task PeerStreams_ConnectionHandsOutNone_StopsReading()
    {
        InMemoryMultiplexedConnection connection = new();
        connection.FailAccepting(new NotSupportedException());
        Http3ClientSession session = new(connection, LocalSettings);

        await session.PeerStreamsReading;
        await session.DisposeAsync();

        Assert.IsNull(session.ConnectionError);
    }

    [TestMethod]
    public async Task PeerStreams_UnknownTypesWithCallback_TakenReachItAfterTheirTypeAndDeclinedAreStopped()
    {
        InMemoryMultiplexedConnection connection = new();
        InMemoryMultiplexedStream webTransport = connection.AddPeerStream(0x40, 0x54, 0x07);
        InMemoryMultiplexedStream grease = connection.AddPeerStream(0x21);
        connection.FailAccepting(new MultiplexedConnectionFailedException(CurlExitCode.RecvError, "lost"));
        List<Http3UnknownPeerStream> offered = [];
        Http3ClientSession session = new(connection, LocalSettings, unknownPeerStreamReceived: stream => Offer(offered, stream, takenType: 0x54));

        await session.PeerStreamsReading;
        byte[] next = new byte[1];
        int read = await webTransport.ReadAsync(next, CancellationToken.None);
        await session.DisposeAsync();

        CollectionAssert.AreEqual(new (IMultiplexedStream, bool, long)[] { (grease, false, 0x21L), (webTransport, false, 0x54L) }, Sorted(offered));
        Assert.AreEqual((1, (byte)0x07), (read, next[0]));
        Assert.IsNull(webTransport.AbortedWith);
        Assert.AreEqual((long)Http3ErrorCode.StreamCreationError, grease.AbortedWith);
        Assert.IsTrue(webTransport.IsDisposed);
        Assert.IsNull(session.ConnectionError);
    }

    [TestMethod]
    public async Task PeerStreams_WithoutCallback_StopsUnknownTypeAndAcceptsNoBidirectionalStream()
    {
        InMemoryPeerBidirectionalConnection connection = new();
        InMemoryMultiplexedStream webTransport = connection.Inner.AddPeerStream(0x40, 0x54);
        InMemoryMultiplexedStream bidirectional = connection.AddPeerBidirectionalStream(0x40, 0x41);
        connection.Inner.FailAccepting(new MultiplexedConnectionFailedException(CurlExitCode.RecvError, "lost"));
        Http3ClientSession session = new(connection, LocalSettings);

        await session.PeerStreamsReading;
        await session.DisposeAsync();

        Assert.AreEqual((long)Http3ErrorCode.StreamCreationError, webTransport.AbortedWith);
        Assert.AreEqual(0, connection.BidirectionalAccepts);
        Assert.IsFalse(bidirectional.IsDisposed);
    }

    [TestMethod]
    public async Task PeerStreams_BidirectionalWithCallback_TakenReachItAfterTheirSignalAndOthersAreStopped()
    {
        InMemoryPeerBidirectionalConnection connection = new();
        InMemoryMultiplexedStream signalled = connection.AddPeerBidirectionalStream(0x40, 0x41, 0x00);
        InMemoryMultiplexedStream declined = connection.AddPeerBidirectionalStream(0x05);
        InMemoryMultiplexedStream empty = connection.AddPeerBidirectionalStream();
        empty.EndReceiving();
        InMemoryMultiplexedStream cutShort = connection.AddPeerBidirectionalStream(0x40);
        cutShort.EndReceiving();
        connection.AddPeerBidirectionalStream().FailReceiving(new MultiplexedStreamResetException(0x100, "reset"));
        connection.AddPeerBidirectionalStream().FailReceiving(new MultiplexedConnectionFailedException(CurlExitCode.RecvError, "lost"));
        connection.FailAcceptingBidirectional(new MultiplexedConnectionFailedException(CurlExitCode.RecvError, "lost"));
        connection.Inner.FailAccepting(new MultiplexedConnectionFailedException(CurlExitCode.RecvError, "lost"));
        List<Http3UnknownPeerStream> offered = [];
        Http3ClientSession session = new(connection, LocalSettings, unknownPeerStreamReceived: stream => Offer(offered, stream, takenType: 0x41));

        await session.PeerStreamsReading;
        await session.DisposeAsync();

        CollectionAssert.AreEqual(new (IMultiplexedStream, bool, long)[] { (declined, true, 0x05L), (signalled, true, 0x41L) }, Sorted(offered));
        Assert.IsNull(signalled.AbortedWith);
        CollectionAssert.AreEqual(Enumerable.Repeat<long?>((long)Http3ErrorCode.StreamCreationError, 3).ToArray(), new[] { declined.AbortedWith, empty.AbortedWith, cutShort.AbortedWith });
        Assert.IsTrue(signalled.IsDisposed);
        Assert.IsNull(session.ConnectionError);
    }

    [TestMethod]
    public async Task DisposeAsync_BidirectionalStreamWaitingForItsSignal_StopsReadingIt()
    {
        InMemoryPeerBidirectionalConnection connection = new();
        InMemoryMultiplexedStream waiting = connection.AddPeerBidirectionalStream();
        List<Http3UnknownPeerStream> offered = [];
        Http3ClientSession session = new(connection, LocalSettings, unknownPeerStreamReceived: stream => Offer(offered, stream, takenType: 0x41));

        await session.DisposeAsync();

        Assert.IsEmpty(offered);
        Assert.IsNull(waiting.AbortedWith);
        Assert.IsTrue(waiting.IsDisposed);
        Assert.AreEqual((long)Http3ErrorCode.NoError, connection.Inner.ClosedWith);
    }

    [TestMethod]
    public async Task DisposeAsync_WhileReading_StopsAndClosesWithNoError()
    {
        InMemoryMultiplexedConnection connection = new();
        connection.AddPeerStream(0x00);
        Http3ClientSession session = new(connection, LocalSettings);
        await session.OpenRequestStreamAsync(null, CancellationToken.None);

        await session.DisposeAsync();

        Assert.AreEqual((long)Http3ErrorCode.NoError, connection.ClosedWith);
        Assert.IsTrue(connection.BidirectionalStreams[0].IsDisposed);
        Assert.IsTrue(connection.UnidirectionalStreams[0].IsDisposed);
    }

    private static bool Offer(List<Http3UnknownPeerStream> offered, Http3UnknownPeerStream stream, long takenType)
    {
        lock (offered)
        {
            offered.Add(stream);
        }

        return stream.Type == takenType;
    }

    private static (IMultiplexedStream Stream, bool IsBidirectional, long Type)[] Sorted(List<Http3UnknownPeerStream> offered) =>
        [.. offered.OrderBy(stream => stream.Type).Select(stream => (stream.Stream, stream.IsBidirectional, stream.Type))];

    private static async Task<(Http3ClientSession Session, List<Http3Frame> Frames)> SessionWithControlStreamAsync(string controlHex, int frameCount = 1)
    {
        InMemoryMultiplexedConnection connection = new();
        connection.AddPeerStream(Qpack.FromHex(controlHex));
        List<Http3Frame> frames = [];
        TaskCompletionSource framesRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Http3ClientSession session = new(connection, LocalSettings, frame =>
        {
            lock (frames)
            {
                frames.Add(frame);
                if (frames.Count == frameCount)
                {
                    framesRead.SetResult();
                }
            }
        });
        await framesRead.Task;
        return (session, frames);
    }
}
