using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins what <see cref="Http2StreamConnection" /> takes from the connection under it; the
/// exchange itself is pinned through the handler in <c>HttpProtocolHandlerTests.Http2</c>.
/// </summary>
[TestClass]
public sealed class Http2StreamConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task Members_TakeTheConnectionsSecurityAndEndPointAndDisposingLeavesItOpen()
    {
        ScriptedConnection connection = new([], 1);
        Http2StreamConnection stream = (Http2StreamConnection)new Http2Session(connection).CreateStream("http", 0, ignoresBody: false);

        Diagnostics.Arrange("connection", $"secure {connection.IsSecure}, remote end point {connection.RemoteEndPoint?.ToString() ?? "none"}");

        await stream.DisposeAsync();

        Diagnostics.Act("stream secure, connection disposed", $"{stream.IsSecure}, {connection.IsDisposed}");
        Diagnostics.Assert("connection disposed", false, connection.IsDisposed);

        Assert.AreEqual(connection.IsSecure, stream.IsSecure);
        Assert.AreEqual(connection.RemoteEndPoint, stream.RemoteEndPoint);
        Assert.IsFalse(connection.IsDisposed);
    }
}
