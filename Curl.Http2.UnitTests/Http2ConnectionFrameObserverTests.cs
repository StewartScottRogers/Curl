using Curl.Testing;
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

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SendPrefaceAsync_ReportsItsSettingsAndWindowUpdate()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, observer) = Connect();
        diagnostics.Arrange("peer frames", "none");

        await connection.SendPrefaceAsync(None);
        diagnostics.Act("observer lines", string.Join(", ", observer.Lines));

        diagnostics.Assert("observer lines", "sent SETTINGS 0, sent WINDOW_UPDATE 0", string.Join(", ", observer.Lines));
        CollectionAssert.AreEqual(new[] { "sent SETTINGS 0", "sent WINDOW_UPDATE 0" }, observer.Lines);
    }

    [TestMethod]
    public async Task WriteHeadersAsync_ReportsTheHeadersAndEachContinuation()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, observer) = Connect(CreateSettings([new(Http2SettingIdentifier.MaxFrameSize, 16384)]));
        var streamId = connection.OpenStream();
        diagnostics.Arrange("stream id", streamId);
        diagnostics.Arrange("header block length", 20000);

        await connection.WriteHeadersAsync(streamId, new byte[20000], isEndStream: true, None);
        diagnostics.Act("observer lines", string.Join(", ", observer.Lines));

        diagnostics.Assert("observer lines", "sent HEADERS 1, sent CONTINUATION 1", string.Join(", ", observer.Lines));
        CollectionAssert.AreEqual(new[] { "sent HEADERS 1", "sent CONTINUATION 1" }, observer.Lines);
    }

    [TestMethod]
    public async Task WriteDataAsync_ReportsEachDataFrame()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, observer) = Connect();
        var streamId = connection.OpenStream();
        await connection.WriteHeadersAsync(streamId, new byte[] { 0x82 }, isEndStream: false, None);
        observer.Lines.Clear();
        diagnostics.Arrange("stream id", streamId);
        diagnostics.Arrange("data length", 20000);

        var written = await connection.WriteDataAsync(streamId, new byte[20000], isEndStream: true, None);
        diagnostics.Act("bytes written", written);
        diagnostics.Act("observer lines", string.Join(", ", observer.Lines));

        diagnostics.Assert("observer lines", "sent DATA 1, sent DATA 1", string.Join(", ", observer.Lines));
        CollectionAssert.AreEqual(new[] { "sent DATA 1", "sent DATA 1" }, observer.Lines);
    }

    [TestMethod]
    public async Task ReadFrameAsync_ReportsTheFrameReadBeforeTheAcknowledgementItSends()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, observer) = Connect(CreateSettings([]));
        diagnostics.Arrange("peer frames", "empty SETTINGS");

        var frame = await connection.ReadFrameAsync(None);
        diagnostics.Act("frame read", frame);
        diagnostics.Act("observer lines", string.Join(", ", observer.Lines));

        diagnostics.Assert("observer lines", "received SETTINGS 0, sent SETTINGS 0", string.Join(", ", observer.Lines));
        CollectionAssert.AreEqual(new[] { "received SETTINGS 0", "sent SETTINGS 0" }, observer.Lines);
    }

    [TestMethod]
    public async Task ReadFrameAsync_PeerClosed_ReportsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var (connection, observer) = Connect();
        diagnostics.Arrange("peer frames", "none, peer closed");

        var frame = await connection.ReadFrameAsync(None);
        diagnostics.Act("frame read", frame);
        diagnostics.Act("observer line count", observer.Lines.Count);

        diagnostics.Assert("observer line count", 0, observer.Lines.Count);
        Assert.IsEmpty(observer.Lines);
    }

    [TestMethod]
    public async Task WithoutAnObserver_WritesAndReadsAsBefore()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var peer = new PeerStream(Wire(CreateSettings([])));
        var connection = new Http2Connection(peer);
        diagnostics.Arrange("peer frames", "empty SETTINGS");
        diagnostics.Arrange("observer", "none");

        await connection.SendPrefaceAsync(None);
        var frame = await connection.ReadFrameAsync(None);
        diagnostics.Act("frame read", frame);
        diagnostics.Act("peer settings received", connection.IsPeerSettingsReceived);

        diagnostics.Assert("frame observer", null, connection.FrameObserver);
        diagnostics.Assert("peer settings received", true, connection.IsPeerSettingsReceived);
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
