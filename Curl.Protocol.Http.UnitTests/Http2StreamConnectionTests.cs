using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins what <see cref="Http2StreamConnection" /> takes from the connection under it; the
/// exchange itself is pinned through the handler in <c>HttpProtocolHandlerTests.Http2</c>.
/// </summary>
[TestClass]
public sealed class Http2StreamConnectionTests
{
    [TestMethod]
    public async Task Members_TakeTheConnectionsSecurityAndEndPointAndDisposingLeavesItOpen()
    {
        ScriptedConnection connection = new([], 1);
        Http2StreamConnection stream = (Http2StreamConnection)new Http2Session(connection).CreateStream("http", 0, ignoresBody: false);

        await stream.DisposeAsync();

        Assert.AreEqual(connection.IsSecure, stream.IsSecure);
        Assert.AreEqual(connection.RemoteEndPoint, stream.RemoteEndPoint);
        Assert.IsFalse(connection.IsDisposed);
    }
}
