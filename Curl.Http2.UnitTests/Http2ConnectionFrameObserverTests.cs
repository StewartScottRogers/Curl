using static Curl.Http2.Http2FrameFactory;
using static Curl.Http2.Http2Test;

namespace Curl.Http2;

/// <summary>
/// Pins that <see cref="Http2Connection.FrameObserver" /> hears of every frame the connection
/// writes - the preface's, a header block's, DATA and the frames it sends on its own - and of every
/// frame it reads, before acting on it (BL-1167).
/// </summary>
[TestClass]
public sealed class Http2ConnectionFrameObserverTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    [TestMethod]
    public async Task SendPrefaceAsync_ReportsItsSettingsAndWindowUpdate()
    {
        var (connection, observer) = Connect();

        await connection.SendPrefaceAsync(None);

        CollectionAssert.AreEqual(new[] { "sent SETTINGS 0", "sent WINDOW_UPDATE 0" }, observer.Lines);
    }

    [TestMethod]
    public async Task WriteHeadersAsync_ReportsTheHeadersAndEachContinuation()
    {
        var (connection, observer) = Connect(CreateSettings([new(Http2SettingIdentifier.MaxFrameSize, 16384)]));
        var streamId = connection.OpenStream();

        await connection.WriteHeadersAsync(streamId, new byte[20000], isEndStream: true, None);

        CollectionAssert.AreEqual(new[] { "sent HEADERS 1", "sent CONTINUATION 1" }, observer.Lines);
    }

    [TestMethod]
    public async Task WriteDataAsync_ReportsEachDataFrame()
    {
        var (connection, observer) = Connect();
        var streamId = connection.OpenStream();
        await connection.WriteHeadersAsync(streamId, new byte[] { 0x82 }, isEndStream: false, None);
        observer.Lines.Clear();

        _ = await connection.WriteDataAsync(streamId, new byte[20000], isEndStream: true, None);

        CollectionAssert.AreEqual(new[] { "sent DATA 1", "sent DATA 1" }, observer.Lines);
    }

    [TestMethod]
    public async Task ReadFrameAsync_ReportsTheFrameReadBeforeTheAcknowledgementItSends()
    {
        var (connection, observer) = Connect(CreateSettings([]));

        _ = await connection.ReadFrameAsync(None);

        CollectionAssert.AreEqual(new[] { "received SETTINGS 0", "sent SETTINGS 0" }, observer.Lines);
    }

    [TestMethod]
    public async Task ReadFrameAsync_PeerClosed_ReportsNothing()
    {
        var (connection, observer) = Connect();

        _ = await connection.ReadFrameAsync(None);

        Assert.IsEmpty(observer.Lines);
    }

    [TestMethod]
    public async Task WithoutAnObserver_WritesAndReadsAsBefore()
    {
        using var peer = new PeerStream(Wire(CreateSettings([])));
        var connection = new Http2Connection(peer);

        await connection.SendPrefaceAsync(None);
        _ = await connection.ReadFrameAsync(None);

        Assert.IsNull(connection.FrameObserver);
        Assert.IsTrue(connection.IsPeerSettingsReceived);
    }

    private static (Http2Connection Connection, RecordingObserver Observer) Connect(params Http2Frame[] fromPeer)
    {
        RecordingObserver observer = new();
        return (new Http2Connection(new PeerStream(Wire(fromPeer))) { FrameObserver = observer }, observer);
    }

    private sealed class RecordingObserver : IHttp2FrameObserver
    {
        public List<string> Lines { get; } = [];

        public void FrameSent(Http2Frame frame) => Lines.Add($"sent {Name(frame)} {frame.StreamId}");

        public void FrameReceived(Http2Frame frame) => Lines.Add($"received {Name(frame)} {frame.StreamId}");

        private static string Name(Http2Frame frame) => frame.Type switch
        {
            Http2FrameType.WindowUpdate => "WINDOW_UPDATE",
            _ => frame.Type.ToString().ToUpperInvariant(),
        };
    }
}
