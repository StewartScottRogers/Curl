using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// When <see cref="Http3Session" /> has curl's <c>peer idle timeout</c> line written under
/// <c>--trace-config http/3</c> (BL-5995). Measured with curl 8.18.0's ngtcp2 build against
/// <c>Record-CurlExchange.ps1 -Http3StalledOrigin</c>: three URLs one after another write it
/// before each of streams 0, 4 and 8; with <c>-Z</c>, the three open together and only stream 0 does.
/// </summary>
[TestClass]
public sealed class Http3PeerIdleTimeoutTraceTests
{
    private const string IdleLine = "[HTTP/3] peer idle timeout is 300000ms, set keep-alive to 150000 ms.";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task OpenRequestStreamAsync_SecondStreamAfterTheFirstIsDone_WritesTheLineForBoth()
    {
        Http3Session session = new(Connection(new FakeMultiplexedStream(0, []), new FakeMultiplexedStream(4, [])));
        RecordingTransferEvents first = new();
        RecordingTransferEvents second = new();
        Diagnostics.Arrange("second stream opens", "after the first is marked done");

        await session.OpenRequestStreamAsync(HttpFrameLog.Silent, CancellationToken.None, new Http3StreamTrace(first));
        session.EndRequestStream();
        await session.OpenRequestStreamAsync(HttpFrameLog.Silent, CancellationToken.None, new Http3StreamTrace(second));

        Diagnostics.Act("second transfer's lines", string.Join(" | ", second.Info));
        Diagnostics.Assert("second transfer's lines", IdleLine, string.Join(" | ", second.Info));
        CollectionAssert.AreEqual(new[] { IdleLine }, first.Info);
        CollectionAssert.AreEqual(new[] { IdleLine }, second.Info);
    }

    [TestMethod]
    public async Task OpenRequestStreamAsync_SecondStreamWhileTheFirstIsActive_WritesTheLineForTheFirstOnly()
    {
        Http3Session session = new(Connection(new FakeMultiplexedStream(0, []), new FakeMultiplexedStream(4, [])));
        RecordingTransferEvents first = new();
        RecordingTransferEvents second = new();
        Diagnostics.Arrange("second stream opens", "while the first is active");

        await session.OpenRequestStreamAsync(HttpFrameLog.Silent, CancellationToken.None, new Http3StreamTrace(first));
        await session.OpenRequestStreamAsync(HttpFrameLog.Silent, CancellationToken.None, new Http3StreamTrace(second));

        Diagnostics.Act("second transfer's lines", string.Join(" | ", second.Info));
        Diagnostics.Assert("second transfer's line count", 0, second.Info.Count);
        CollectionAssert.AreEqual(new[] { IdleLine }, first.Info);
        Assert.IsEmpty(second.Info);
    }

    [TestMethod]
    public async Task OpenRequestStreamAsync_AfterAnOpenThatFailed_WritesTheLineAgain()
    {
        FakeMultiplexedConnection quic = new()
        {
            PeerIdleTimeout = TimeSpan.FromMinutes(5),
            BidirectionalOpenException = new IOException("refused"),
        };
        Http3Session session = new(quic);
        RecordingTransferEvents first = new();
        RecordingTransferEvents second = new();
        Diagnostics.Arrange("stream opens", "both refused");

        await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await session.OpenRequestStreamAsync(HttpFrameLog.Silent, CancellationToken.None, new Http3StreamTrace(first)));
        await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await session.OpenRequestStreamAsync(HttpFrameLog.Silent, CancellationToken.None, new Http3StreamTrace(second)));

        Diagnostics.Act("second transfer's lines", string.Join(" | ", second.Info));
        Diagnostics.Assert("second transfer's lines", IdleLine, string.Join(" | ", second.Info));
        CollectionAssert.AreEqual(new[] { IdleLine }, second.Info);
    }

    private static FakeMultiplexedConnection Connection(params FakeMultiplexedStream[] requestStreams) =>
        new(requestStreams) { PeerIdleTimeout = TimeSpan.FromMinutes(5) };
}
