using System.Text.Json;
using Curl.Mcp.Protocol.JsonRpc;

namespace Curl.Mcp.Protocol.Server;

[TestClass]
public sealed class McpRequestTrackerTests
{
    private static JsonRpcRequestId IdOf(string json) => new(JsonDocument.Parse(json).RootElement.Clone());

    [TestMethod]
    public void CancelAll_RequestInFlight_CancelsItsSource()
    {
        McpRequestTracker tracker = new();
        using CancellationTokenSource source = tracker.Start(IdOf("1"), CancellationToken.None);

        tracker.CancelAll();

        Assert.IsTrue(source.IsCancellationRequested);
    }

    [TestMethod]
    public void CancelAll_SourceDisposedBeforeTheRequestFinished_DoesNotThrow()
    {
        McpRequestTracker tracker = new();
        CancellationTokenSource source = tracker.Start(IdOf("1"), CancellationToken.None);
        source.Dispose();

        tracker.CancelAll();

        Assert.ThrowsExactly<ObjectDisposedException>(() => source.Token);
    }

    [TestMethod]
    public void Cancel_SourceDisposedBeforeTheRequestFinished_DoesNotThrow()
    {
        McpRequestTracker tracker = new();
        CancellationTokenSource source = tracker.Start(IdOf("\"a-1\""), CancellationToken.None);
        source.Dispose();

        tracker.Cancel("\"a-1\"");

        Assert.ThrowsExactly<ObjectDisposedException>(() => source.Token);
    }

    [TestMethod]
    public void Cancel_FinishedRequest_LeavesItsSourceUncancelled()
    {
        McpRequestTracker tracker = new();
        JsonRpcRequestId id = IdOf("7");
        using CancellationTokenSource source = tracker.Start(id, CancellationToken.None);
        tracker.Finish(id, source);

        tracker.Cancel("7");

        Assert.IsFalse(source.IsCancellationRequested);
    }
}
