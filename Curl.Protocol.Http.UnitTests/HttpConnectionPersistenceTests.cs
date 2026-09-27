using System.Text;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpConnectionPersistence" /> to HTTP/1.1's rules for when a connection
/// stays open after a response.
/// </summary>
[TestClass]
public sealed class HttpConnectionPersistenceTests
{
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
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes(response), 65536);
        HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);

        Assert.AreEqual(expected, HttpConnectionPersistence.KeepsAlive(head, noBody));
    }
}
