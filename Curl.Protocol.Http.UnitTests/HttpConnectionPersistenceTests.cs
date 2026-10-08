using System.Text;
using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpConnectionPersistence" /> to HTTP/1.1's rules for when a connection
/// stays open after a response.
/// </summary>
[TestClass]
public sealed class HttpConnectionPersistenceTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("HTTP/1.1 401 Unauthorized\r\nContent-Length: 4\r\n\r\n", false, true, DisplayName = "1.1 with a length stays open")]
    [DataRow("HTTP/1.1 401 Unauthorized\r\nTransfer-Encoding: chunked\r\n\r\n", false, true, DisplayName = "1.1 chunked stays open")]
    [DataRow("HTTP/1.1 401 Unauthorized\r\nConnection: close\r\nContent-Length: 4\r\n\r\n", false, false, DisplayName = "Connection: close closes")]
    [DataRow("HTTP/1.1 401 Unauthorized\r\nconnection: Keep-Alive, CLOSE\r\nContent-Length: 4\r\n\r\n", false, false, DisplayName = "close in a list, any case, closes")]
    [DataRow("HTTP/1.1 401 Unauthorized\r\nConnection: Upgrade\r\nContent-Length: 4\r\n\r\n", false, true, DisplayName = "Other Connection options stay open")]
    [DataRow("HTTP/1.1 401 Unauthorized\r\n\r\n", false, false, DisplayName = "Body to the close closes")]
    [DataRow("HTTP/1.1 401 Unauthorized\r\n\r\n", true, true, DisplayName = "HEAD has no body to run to the close")]
    [DataRow("HTTP/1.1 204 No Content\r\n\r\n", false, true, DisplayName = "204 has no body to run to the close")]
    [DataRow("HTTP/1.0 401 Unauthorized\r\nContent-Length: 4\r\n\r\n", false, false, DisplayName = "1.0 closes")]
    [DataRow("HTTP/1.0 401 Unauthorized\r\nConnection: keep-alive\r\nContent-Length: 4\r\n\r\n", false, true, DisplayName = "1.0 keep-alive stays open")]
    public async Task KeepsAlive_Head_FollowsHttp11(string response, bool noBody, bool expected)
    {
        Diagnostics.Arrange("response", Visible(response));
        Diagnostics.Arrange("no body", noBody);
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes(response), 65536);
        HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);

        bool keepsAlive = HttpConnectionPersistence.KeepsAlive(head, noBody);

        Diagnostics.Act("keeps alive", keepsAlive);
        Diagnostics.Assert("keeps alive", expected, keepsAlive);
        Assert.AreEqual(expected, keepsAlive);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n", true, false, false, DisplayName = "--raw chunked runs to the close")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\n", true, false, true, DisplayName = "--raw with a length stays open")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\n", false, true, false, DisplayName = "--ignore-content-length runs to the close")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n", false, true, true, DisplayName = "--ignore-content-length chunked stays open")]
    public async Task KeepsAlive_RawOrIgnoreContentLength_ClosesWhenTheBodyRunsToTheClose(
        string response,
        bool passesTransferCoding,
        bool ignoresContentLength,
        bool expected)
    {
        Diagnostics.Arrange("response", Visible(response));
        Diagnostics.Arrange("passes transfer coding", passesTransferCoding);
        Diagnostics.Arrange("ignores content length", ignoresContentLength);
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes(response), 65536);
        HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);

        bool keepsAlive = HttpConnectionPersistence.KeepsAlive(head, false, passesTransferCoding, ignoresContentLength);

        Diagnostics.Act("keeps alive", keepsAlive);
        Diagnostics.Assert("keeps alive", expected, keepsAlive);
        Assert.AreEqual(expected, keepsAlive);
    }

    [TestMethod]
    [DataRow("HTTP/1.0 200 OK\r\nConnection: keep-alive\r\n\r\n", false, false, true, DisplayName = "1.0 keep-alive with no length")]
    [DataRow("HTTP/1.0 200 OK\r\nConnection: keep-alive\r\nContent-Length: 4\r\n\r\n", false, true, true, DisplayName = "1.0 keep-alive with --ignore-content-length")]
    [DataRow("HTTP/1.0 200 OK\r\nConnection: keep-alive\r\nContent-Length: 4\r\n\r\n", false, false, false, DisplayName = "1.0 keep-alive with a length")]
    [DataRow("HTTP/1.0 200 OK\r\nConnection: keep-alive\r\n\r\n", true, false, false, DisplayName = "1.0 keep-alive to HEAD has no body")]
    [DataRow("HTTP/1.0 200 OK\r\n\r\n", false, false, false, DisplayName = "1.0 without keep-alive")]
    [DataRow("HTTP/1.0 200 OK\r\nConnection: keep-alive, close\r\n\r\n", false, false, false, DisplayName = "1.0 keep-alive and close")]
    [DataRow("HTTP/1.1 200 OK\r\nConnection: keep-alive\r\n\r\n", false, false, false, DisplayName = "1.1 keep-alive with no length")]
    public async Task KeepsHttp10AliveUntilServerCloses_Head_IsTrueForAnHttp10KeepAliveBodyThatRunsToTheClose(
        string response,
        bool noBody,
        bool ignoresContentLength,
        bool expected)
    {
        Diagnostics.Arrange("response", Visible(response));
        Diagnostics.Arrange("no body", noBody);
        Diagnostics.Arrange("ignores content length", ignoresContentLength);
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes(response), 65536);
        HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);

        bool keepsAlive = HttpConnectionPersistence.KeepsHttp10AliveUntilServerCloses(head, noBody, false, ignoresContentLength, false);

        Diagnostics.Act("keeps HTTP/1.0 alive until the server closes", keepsAlive);
        Diagnostics.Assert("keeps HTTP/1.0 alive until the server closes", expected, keepsAlive);
        Assert.AreEqual(expected, keepsAlive);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 4\r\nTransfer-Encoding: gzip\r\n\r\n", false, DisplayName = "gzip without chunked runs to the close")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip, chunked\r\n\r\n", true, DisplayName = "gzip, chunked stays open")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\n", true, DisplayName = "No Transfer-Encoding stays open")]
    public async Task KeepsAlive_TransferEncoding_ClosesWhenTheBodyIsNotChunked(string response, bool expected)
    {
        Diagnostics.Arrange("response", Visible(response));
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes(response), 65536);
        HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);

        bool keepsAlive = HttpConnectionPersistence.KeepsAlive(head, false, decodesTransferCoding: true);

        Diagnostics.Act("keeps alive", keepsAlive);
        Diagnostics.Assert("keeps alive", expected, keepsAlive);
        Assert.AreEqual(expected, keepsAlive);
    }

    [TestMethod]
    [DataRow("Connection: keep-alive", true, DisplayName = "keep-alive")]
    [DataRow("connection:Keep-Alive", true, DisplayName = "Any case, no blank")]
    [DataRow("Connection: Foo, Keep-Alive", true, DisplayName = "Among other options")]
    [DataRow("Connection: keep-alive, close", false, DisplayName = "close wins")]
    [DataRow("Connection: close, keep-alive", false, DisplayName = "close wins when named first")]
    [DataRow("Connection: close", false, DisplayName = "close")]
    [DataRow("Keep-Alive: timeout=5", false, DisplayName = "Another header")]
    [DataRow("X-Connection: keep-alive", false, DisplayName = "A header ending in Connection")]
    [DataRow("no colon", false, DisplayName = "No colon")]
    public void KeepsHttp10Alive_HeaderLine_IsTrueForAConnectionHeaderNamingKeepAliveAndNotClose(string headerLine, bool expected)
    {
        Diagnostics.Arrange("header line", headerLine);

        bool keepsAlive = HttpConnectionPersistence.KeepsHttp10Alive(headerLine);

        Diagnostics.Act("keeps HTTP/1.0 alive", keepsAlive);
        Diagnostics.Assert("keeps HTTP/1.0 alive", expected, keepsAlive);
        Assert.AreEqual(expected, keepsAlive);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nX: y\r\n\r\n", false, false, true, DisplayName = "1.1 without a length")]
    [DataRow("HTTP/1.1 200 OK\r\nConnection: keep-alive\r\n\r\n", false, false, true, DisplayName = "1.1 keep-alive without a length")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n", false, true, true, DisplayName = "--ignore-content-length")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n", false, false, false, DisplayName = "A Content-Length")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n", false, true, false, DisplayName = "Transfer-Encoding")]
    [DataRow("HTTP/1.1 200 OK\r\nConnection: close, keep-alive\r\n\r\n", false, false, false, DisplayName = "Connection: close")]
    [DataRow("HTTP/1.0 200 OK\r\nConnection: keep-alive\r\n\r\n", false, false, false, DisplayName = "HTTP/1.0")]
    [DataRow("HTTP/1.1 204 No Content\r\n\r\n", false, false, false, DisplayName = "204")]
    [DataRow("HTTP/1.1 200 OK\r\nX: y\r\n\r\n", true, false, false, DisplayName = "HEAD")]
    public async Task LacksEndOfMessageIndicator_Head_IsTrueWhenOnlyTheServerClosingCanEndTheBody(
        string response,
        bool noBody,
        bool ignoresContentLength,
        bool expected)
    {
        Diagnostics.Arrange("response", Visible(response));
        Diagnostics.Arrange("no body", noBody);
        Diagnostics.Arrange("ignores content length", ignoresContentLength);
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes(response), 65536);
        HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);

        bool lacksIndicator = HttpConnectionPersistence.LacksEndOfMessageIndicator(head, noBody, ignoresContentLength);

        Diagnostics.Act("lacks an end-of-message indicator", lacksIndicator);
        Diagnostics.Assert("lacks an end-of-message indicator", expected, lacksIndicator);
        Assert.AreEqual(expected, lacksIndicator);
    }

    private static string Visible(string text) => text.Replace("\r", "\\r").Replace("\n", "\\n");
}
