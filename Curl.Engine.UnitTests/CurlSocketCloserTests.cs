using Curl.Api.Callbacks;

namespace Curl.Engine;

[TestClass]
public sealed class CurlSocketCloserTests
{
    [TestMethod]
    public void IsTheConnectionsOwnSocket_NotGiven_IsFalseSoTheCallbackRunsAfterTheConnectionCloses()
    {
        CurlSocketCloser closer = new(new CurlSocket(5), _ => 0);

        Assert.IsFalse(closer.IsTheConnectionsOwnSocket);
    }

    [TestMethod]
    public void IsTheConnectionsOwnSocket_Given_IsTrue()
    {
        CurlSocketCloser closer = new(new CurlSocket(5), _ => 0, isTheConnectionsOwnSocket: true);

        Assert.IsTrue(closer.IsTheConnectionsOwnSocket);
    }

    [TestMethod]
    public void Close_CalledTwice_CallsTheCallbackOnceThenDisposesTheOwnedSocket()
    {
        List<string> calls = [];
        CurlSocketCloser closer = new(new CurlSocket(5), socket => { calls.Add($"close {socket.Handle}"); return 0; }, ownedSocket: new RecordingDisposable(calls));

        closer.Close();
        closer.Close();

        CollectionAssert.AreEqual(new[] { "close 5", "dispose" }, calls);
    }

    private sealed class RecordingDisposable(List<string> calls) : IDisposable
    {
        public void Dispose() => calls.Add("dispose");
    }
}
