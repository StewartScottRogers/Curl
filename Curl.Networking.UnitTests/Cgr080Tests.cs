using System.Text;

using Curl.Networking.Fakes;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// CGR-080 of the third-party gap report (BL-3609): curl 8.22.0 reads a CONNECT reply's status
/// only after <c>HTTP/1.0</c> or <c>HTTP/1.1</c>, so <c>HTTP/2 200</c>, <c>HTTP/3 200</c>,
/// <c>HTTP/2.0 200</c> or <c>HTTP/1.2 200 OK</c> is "response 0", exit 7 (measured).
/// </summary>
[TestClass]
public sealed class Cgr080Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("HTTP/2 200\r\n\r\n", 0)]
    [DataRow("HTTP/3 200\r\n\r\n", 0)]
    [DataRow("HTTP/2.0 200\r\n\r\n", 0)]
    [DataRow("HTTP/1.2 200 OK\r\n\r\n", 0)]
    [DataRow("HTTP/1. 200 OK\r\n\r\n", 0)]
    [DataRow("HTTP/1.10 200 OK\r\n\r\n", 0)]
    [DataRow("HTTP/11 200\r\n\r\n", 0)]
    [DataRow("HTTP/1.0 200 OK\r\n\r\n", 200)]
    [DataRow("HTTP/1.1 200 OK\r\n\r\n", 200)]
    public async Task ReadReplyAsync_StatusLineNotHttp10OrHttp11_IsStatusZero(string reply, int expected)
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(reply));
        Diagnostics.Arrange("reply", reply.Replace("\r\n", "|", StringComparison.Ordinal));

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        Diagnostics.Act("status code", result.StatusCode);
        Assert.AreEqual(expected, result.StatusCode);
        Assert.IsNull(result.FailureMessage);
    }
}
