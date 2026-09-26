using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="SwsHttpServerConnector"/> against upstream's <c>tests/server/sws.c</c> at
/// <c>curl-8_21_0</c>: how a request is framed, which <c>&lt;reply&gt;</c> part answers it, when the
/// connection closes, which <c>&lt;servercmd&gt;</c> commands are reported, and what is recorded.
/// </summary>
[TestClass]
public sealed class SwsHttpServerConnectorTests
{
    private const string Get = "GET /1234 HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n";

    private static readonly ConnectTarget Target = new("127.0.0.1", 8990, false);

    [TestMethod]
    public async Task Get_ReceivesData()
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "first\n"), Reply("data2", "second\n"))));

        Assert.AreEqual("first\n", await ExchangeAsync(connection, Get));
    }

    [TestMethod]
    [DataRow("/12340002", "second\n")]
    [DataRow("/want/12340002?query=1", "second\n")]
    [DataRow("/12340000", "first\n")]
    [DataRow("/1234", "first\n")]
    [DataRow("/10000", "first\n")]
    [DataRow("/10001", "third\n")]
    [DataRow("/", "first\n")]
    [DataRow("/page", "first\n")]
    [DataRow("/99999999999", "first\n")]
    [DataRow("127.0.0.1:12340002", "first\n")]
    public async Task Path_SelectsThePartAsSwsDoes(string path, string expected)
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "first\n"), Reply("data2", "second\n"), Reply("data1", "third\n"))));

        Assert.AreEqual(expected, await ExchangeAsync(connection, $"GET {path} HTTP/1.1\r\n\r\n"));
    }

    [TestMethod]
    public async Task Post_ReadsTheBodyByContentLength()
    {
        SwsHttpServerConnector server = new(Case(Reply("data", "posted\n")));
        IConnection connection = await ConnectAsync(server);

        await WriteAsync(connection, "POST /1234 HTTP/1.1\r\nContent-Length: 5\r\n\r\nab");
        Assert.AreEqual(string.Empty, await ReadAllAsync(connection), "no reply before the body is complete");
        await WriteAsync(connection, "cde");

        Assert.AreEqual("posted\n", await ReadAllAsync(connection));
        Assert.AreEqual("POST /1234 HTTP/1.1\r\nContent-Length: 5\r\n\r\nabcde", Text(server.ReceivedBytes));
    }

    [TestMethod]
    [DataRow("content-length:   3\r\n")]
    [DataRow("Content-Length: 0\r\nContent-Length: 3\r\n")]
    [DataRow("Content-Length: 3\r\nContent-Length: 9\r\n")]
    public async Task ContentLength_IsReadAsSwsReadsIt(string headers)
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "posted\n"))));

        Assert.AreEqual("posted\n", await ExchangeAsync(connection, $"POST /1234 HTTP/1.1\r\n{headers}\r\nabc"));
    }

    [TestMethod]
    [DataRow("Content-Length: abc\r\n")]
    [DataRow("Content-Length: 99999999999999999999999\r\n")]
    public async Task ContentLength_ThatDoesNotParse_EndsTheRequestAtTheHeaders(string headers)
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "posted\n"))));

        Assert.AreEqual("posted\n", await ExchangeAsync(connection, $"POST /1234 HTTP/1.1\r\n{headers}\r\n"));
    }

    [TestMethod]
    public async Task Post_ReadsTheBodyByChunkedEncoding()
    {
        SwsHttpServerConnector server = new(Case(Reply("data", "posted\n")));
        IConnection connection = await ConnectAsync(server);
        const string Request = "POST /1234 HTTP/1.1\r\nTransfer-Encoding: chunked\r\nContent-Length: 99\r\n\r\n3\r\nabc\r\n0\r\n\r\n";

        await WriteAsync(connection, Request[..^9]);
        Assert.AreEqual(string.Empty, await ReadAllAsync(connection), "no reply before the last chunk");
        await WriteAsync(connection, Request[^9..^2]);
        Assert.AreEqual(string.Empty, await ReadAllAsync(connection), "no reply before the empty line after the last chunk");
        await WriteAsync(connection, Request[^2..]);

        Assert.AreEqual("posted\n", await ReadAllAsync(connection));
        Assert.AreEqual(Request, Text(server.ReceivedBytes));
    }

    [TestMethod]
    [DataRow("0\r\n\r\n")]
    [DataRow("0\r\nChecksum: 1\r\n\r\n")]
    public async Task Chunked_EndsAfterTheLastChunkAndItsTrailers(string body)
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "posted\n"))));

        Assert.AreEqual("posted\nposted\n", await ExchangeAsync(connection, $"POST /1234 HTTP/1.1\r\ntransfer-encoding: CHUNKED\r\n\r\n{body}{Get}"));
    }

    [TestMethod]
    public async Task TwoRequestsOnOneConnection_AreAnsweredAndRecordedInOrder()
    {
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n"), Reply("data2", "second\n")));
        IConnection connection = await ConnectAsync(server);
        const string Second = "POST /12340002 HTTP/1.1\r\nContent-Length: 2\r\n\r\nhi";

        Assert.AreEqual("first\n", await ExchangeAsync(connection, Get));
        Assert.AreEqual("second\n", await ExchangeAsync(connection, Second));

        Assert.AreEqual(Get + Second, Text(server.ReceivedBytes));
    }

    [TestMethod]
    public async Task TwoRequestsInOneWrite_AreBothAnswered()
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "first\n"), Reply("data2", "second\n"))));

        Assert.AreEqual("first\nsecond\n", await ExchangeAsync(connection, Get + "GET /12340002 HTTP/1.1\r\n\r\n"));
    }

    [TestMethod]
    public async Task Connections_AreRecordedInTheOrderTheyWereWritten()
    {
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n")));
        IConnection first = await ConnectAsync(server);
        IConnection second = await ConnectAsync(server);

        await WriteAsync(first, "GET /a HTTP/1.1\r\n\r\n");
        await WriteAsync(second, "GET /b HTTP/1.1\r\n\r\n");

        Assert.AreEqual("GET /a HTTP/1.1\r\n\r\nGET /b HTTP/1.1\r\n\r\n", Text(server.ReceivedBytes));
    }

    [TestMethod]
    [DataRow("reply\nswsclose\n", "")]
    [DataRow("reply\n", "swsclose\n")]
    public async Task Swsclose_ClosesTheConnectionAfterTheReply(string data, string serverCommands)
    {
        SwsHttpServerConnector server = new(Case(Reply("data", data), Reply("servercmd", serverCommands)));
        IConnection connection = await ConnectAsync(server);

        Assert.AreEqual(data, await ExchangeAsync(connection, Get));
        Assert.AreEqual(string.Empty, await ExchangeAsync(connection, Get), "the second request goes unanswered");
        Assert.AreEqual(Get, Text(server.ReceivedBytes), "bytes written after the close are not recorded");
    }

    [TestMethod]
    public async Task MissingPart_SendsNothingAndCloses()
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "first\n"))));

        Assert.AreEqual(string.Empty, await ExchangeAsync(connection, "GET /12340005 HTTP/1.1\r\n\r\n"));
        Assert.AreEqual(string.Empty, await ExchangeAsync(connection, Get));
    }

    [TestMethod]
    [DataRow("GARBAGE\r\n\r\n")]
    [DataRow("GET /1234\r\n\r\n")]
    [DataRow("GET /1234 HTTP/x.1\r\n\r\n")]
    [DataRow("GET /1234 HTTP/1\r\n\r\n")]
    [DataRow("GET /1234 HTTP/1-1\r\n\r\n")]
    [DataRow("GET /1234 HTTP/1.x\r\n\r\n")]
    [DataRow("GET  HTTP/1.1\r\n\r\n")]
    public async Task MalformedRequestLine_GetsTheNotFoundDocumentAndACLose(string request)
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "first\n"))));

        string reply = await ExchangeAsync(connection, request);

        StringAssert.StartsWith(reply, "HTTP/1.1 404 Not Found\r\nServer: sws/1.0\r\nConnection: close\r\n");
        StringAssert.EndsWith(reply, "</BODY></HTML>\n");
        Assert.AreEqual(string.Empty, await ExchangeAsync(connection, Get));
    }

    [TestMethod]
    [DataRow("base64=\"yes\"", "aGVsbG8K\n", "hello\n")]
    [DataRow("base64=\"yes\"", "not base64!\n", "")]
    [DataRow("nonewline=\"yes\"", "hello\n", "hello")]
    [DataRow("base64=\"yes\" nonewline=\"yes\"", "aGVsbG8K\n", "hello")]
    public async Task PartAttributes_AreAppliedAsSwsGetpartDoes(string attributes, string content, string expected)
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply($"data {attributes}", content))));

        Assert.AreEqual(expected, await ExchangeAsync(connection, Get));
    }

    [TestMethod]
    public async Task EmptyPartWithNonewline_SendsNothing()
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data nonewline=\"yes\"", string.Empty))));

        Assert.AreEqual(string.Empty, await ExchangeAsync(connection, Get));
    }

    [TestMethod]
    [DataRow("auth_required\n", "auth_required")]
    [DataRow("idle\n", "idle")]
    [DataRow("stream\n", "stream")]
    [DataRow("connection-monitor\n", "connection-monitor")]
    [DataRow("upgrade\n", "upgrade")]
    [DataRow("no-expect\n", "no-expect")]
    [DataRow("skip: 100\n", "skip")]
    [DataRow("skip:-1\n", "skip")]
    [DataRow("delay: +5\n", "delay")]
    [DataRow("writedelay: 1000\r\n", "writedelay")]
    public void UnsupportedServerCommand_IsReportedByName(string serverCommands, string name)
    {
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n"), Reply("servercmd", serverCommands)));

        CollectionAssert.AreEqual(new[] { name }, server.UnsupportedServerCommands.ToArray());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("swsclose\n")]
    [DataRow("Testnum 1234\npipe: 3\n")]
    [DataRow("skip: many\nskip:\ndelay: -\n")]
    public void ServerCommandsThatAreCarriedOutOrUnknown_AreNotReported(string serverCommands)
    {
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n"), Reply("servercmd", serverCommands)));

        Assert.IsEmpty(server.UnsupportedServerCommands);
    }

    [TestMethod]
    public void ServerCommands_AreReportedInFileOrder()
    {
        SwsHttpServerConnector server = new(Case(Reply("servercmd", "writedelay: 1\n\nidle\nauth_required\n")));

        CollectionAssert.AreEqual(new[] { "writedelay", "idle", "auth_required" }, server.UnsupportedServerCommands.ToArray());
    }

    [TestMethod]
    public void NoServerCommandPart_ReportsNothing()
    {
        Assert.IsEmpty(new SwsHttpServerConnector(Case(Reply("data", "first\n"))).UnsupportedServerCommands);
    }

    [TestMethod]
    public async Task Connection_ReadsIntoASmallBufferInPieces()
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "first\n"))));
        await WriteAsync(connection, Get);
        byte[] buffer = new byte[4];

        Assert.AreEqual(4, await connection.ReadAsync(buffer, CancellationToken.None));
        Assert.AreEqual("firs", Encoding.Latin1.GetString(buffer));
        Assert.AreEqual(2, await connection.ReadAsync(buffer, CancellationToken.None));
        Assert.AreEqual(0, await connection.ReadAsync(buffer, CancellationToken.None));
    }

    [TestMethod]
    public async Task Connection_IsPlainAndHasNoAddress()
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "first\n"))));

        Assert.IsFalse(connection.IsSecure);
        Assert.IsNull(connection.RemoteEndPoint);
        await connection.FlushAsync(CancellationToken.None);
        await connection.DisposeAsync();
    }

    [TestMethod]
    public void Constructor_RejectsANullCase()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SwsHttpServerConnector(null!));
    }

    private static UpstreamTestCase Case(params string[] replyParts)
    {
        string file = "<testcase>\n<reply>\n" + string.Concat(replyParts) + "</reply>\n</testcase>\n";
        return UpstreamTestCaseParser.Parse(Encoding.Latin1.GetBytes(file)).TestCase!;
    }

    private static string Reply(string openingTag, string content) =>
        $"<{openingTag}>\n{content}</{openingTag.Split(' ')[0]}>\n";

    private static async Task<IConnection> ConnectAsync(SwsHttpServerConnector server)
    {
        ConnectResult result = await server.ConnectAsync(Target, CancellationToken.None);
        return result.Connection!;
    }

    private static async Task<string> ExchangeAsync(IConnection connection, string request)
    {
        await WriteAsync(connection, request);
        return await ReadAllAsync(connection);
    }

    private static ValueTask WriteAsync(IConnection connection, string text) =>
        connection.WriteAsync(Encoding.Latin1.GetBytes(text), CancellationToken.None);

    private static async Task<string> ReadAllAsync(IConnection connection)
    {
        StringBuilder text = new();
        byte[] buffer = new byte[1024];
        int count;
        while ((count = await connection.ReadAsync(buffer, CancellationToken.None)) > 0)
        {
            text.Append(Encoding.Latin1.GetString(buffer, 0, count));
        }

        return text.ToString();
    }

    private static string Text(ReadOnlyMemory<byte> bytes) => Encoding.Latin1.GetString(bytes.Span);
}
