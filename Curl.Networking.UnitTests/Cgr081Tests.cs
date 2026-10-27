using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// CGR-081 of the third-party gap report (BL-3610): curl 8.22.0 does not fail a CONNECT reply
/// whose first line does not start with <c>HTTP/</c>; it reads the status as 0 and exits 7,
/// <c>CONNECT tunnel failed, response 0</c>. Exit 43, <c>Invalid response header</c>, is for a
/// later line that holds no colon and does not start with a space or tab (measured).
/// </summary>
[TestClass]
public sealed class Cgr081Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("garbage\r\n\r\n")]
    [DataRow("ICY 200 OK\r\n\r\n")]
    [DataRow("garbage\n\n")]
    [DataRow("<html>\r\n\r\n")]
    [DataRow("garbage\r\nFoo: bar\r\n\r\n")]
    [DataRow("garbage\r\n Foo\r\n\r\n")]
    [DataRow("garbage\r\n\tFoo\r\n\r\n")]
    public async Task ReadReplyAsync_FirstLineNotHttp_IsStatusZeroNotAFailure(string reply)
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(reply));
        Diagnostics.Arrange("reply", reply.Replace("\r\n", "|", StringComparison.Ordinal));

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        Diagnostics.Act("status code", result.StatusCode);
        Assert.IsNull(result.FailureMessage);
        Assert.AreEqual(0, result.StatusCode);
    }

    [TestMethod]
    public async Task ReadReplyAsync_FirstLineNotHttpThenEnd_IsProxyConnectAborted()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes("garbage\r\n"));
        Diagnostics.Arrange("reply", "garbage|");

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        Diagnostics.Act("failure message", result.FailureMessage);
        Assert.AreEqual(CurlExitCode.RecvError, result.FailureExitCode);
        Assert.AreEqual("Proxy CONNECT aborted", result.FailureMessage);
    }

    [TestMethod]
    [DataRow("garbage\r\nFoo\r\n\r\n")]
    [DataRow("garbage\r\nFoo\r\n")]
    [DataRow("HTTP/1.1 200 OK\r\nFoo\r\n\r\n")]
    [DataRow("<html>\n<h1>400 Bad request</h1>\n</html>\n")]
    public async Task ReadReplyAsync_LaterLineWithoutColon_FailsWithInvalidResponseHeader(string reply)
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(reply));
        Diagnostics.Arrange("reply", reply.Replace("\r\n", "|", StringComparison.Ordinal));

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        Diagnostics.Act("failure message", result.FailureMessage);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.FailureExitCode);
        Assert.AreEqual(HttpProxyTunnel.InvalidResponseHeader, result.FailureMessage);
    }
}
