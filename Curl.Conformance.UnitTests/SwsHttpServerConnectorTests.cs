using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    private const string UpgradeRequest = "GET /1234 HTTP/1.1\r\nUpgrade: websocket\r\nContent-Length: 5\r\n\r\n";

    private const string FortyFiveBytes = "aaaaaaaaaaaaaaaaaaaabbbbbbbbbbbbbbbbbbbbcccc\n";

    private static readonly ConnectTarget Target = new("127.0.0.1", 8990, false);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task Get_ReceivesData()
    {
        Diagnostics.Arrange("reply parts", "data=first, data2=second");
        Diagnostics.Arrange("request", Get);
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "first\n"), Reply("data2", "second\n"))));

        Assert.AreEqual("first\n", Observe("reply", "first\n", await ExchangeAsync(connection, Get)));
    }

    [TestMethod]
    [DataRow("data crlf=\"headers\"", "HTTP/1.1 200 OK\r\nA: b\r\n\r\nbody\n")]
    [DataRow("data crlf=\"yes\"", "HTTP/1.1 200 OK\r\nA: b\r\n\r\nbody\r\n")]
    [DataRow("data crlf=\"yes\" nonewline=\"yes\"", "HTTP/1.1 200 OK\r\nA: b\r\n\r\nbody\r")]
    public async Task Get_ReceivesDataWithTheLineEndingsPreproForces(string openingTag, string expected)
    {
        Diagnostics.Arrange("opening tag", openingTag);
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply(openingTag, "HTTP/1.1 200 OK\nA: b\n\nbody\n"))));

        Assert.AreEqual(expected, Observe("reply", expected, await ExchangeAsync(connection, Get)));
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
        Diagnostics.Arrange("path", path);
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "first\n"), Reply("data2", "second\n"), Reply("data1", "third\n"))));

        Assert.AreEqual(expected, Observe("reply", expected, await ExchangeAsync(connection, $"GET {path} HTTP/1.1\r\n\r\n")));
    }

    [TestMethod]
    public async Task Post_ReadsTheBodyByContentLength()
    {
        Diagnostics.Arrange("request", "POST /1234 with Content-Length: 5, body sent as ab then cde");
        SwsHttpServerConnector server = new(Case(Reply("data", "posted\n")));
        IConnection connection = await ConnectAsync(server);

        await WriteAsync(connection, "POST /1234 HTTP/1.1\r\nContent-Length: 5\r\n\r\nab");
        Assert.AreEqual(string.Empty, Observe("reply before the body is complete", string.Empty, await ReadAllAsync(connection)), "no reply before the body is complete");
        await WriteAsync(connection, "cde");

        Assert.AreEqual("posted\n", Observe("reply", "posted\n", await ReadAllAsync(connection)));
        Assert.AreEqual("POST /1234 HTTP/1.1\r\nContent-Length: 5\r\n\r\nabcde", Observe("recorded", "POST /1234 HTTP/1.1\r\nContent-Length: 5\r\n\r\nabcde", Text(server.ReceivedBytes)));
    }

    [TestMethod]
    [DataRow("content-length:   3\r\n")]
    [DataRow("Content-Length: 0\r\nContent-Length: 3\r\n")]
    [DataRow("Content-Length: 3\r\nContent-Length: 9\r\n")]
    public async Task ContentLength_IsReadAsSwsReadsIt(string headers)
    {
        Diagnostics.Arrange("headers", headers);
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "posted\n"))));

        Assert.AreEqual("posted\n", Observe("reply", "posted\n", await ExchangeAsync(connection, $"POST /1234 HTTP/1.1\r\n{headers}\r\nabc")));
    }

    [TestMethod]
    [DataRow("Content-Length: abc\r\n")]
    [DataRow("Content-Length: 99999999999999999999999\r\n")]
    public async Task ContentLength_ThatDoesNotParse_EndsTheRequestAtTheHeaders(string headers)
    {
        Diagnostics.Arrange("headers", headers);
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "posted\n"))));

        Assert.AreEqual("posted\n", Observe("reply", "posted\n", await ExchangeAsync(connection, $"POST /1234 HTTP/1.1\r\n{headers}\r\n")));
    }

    [TestMethod]
    public async Task Post_ReadsTheBodyByChunkedEncoding()
    {
        SwsHttpServerConnector server = new(Case(Reply("data", "posted\n")));
        IConnection connection = await ConnectAsync(server);
        const string Request = "POST /1234 HTTP/1.1\r\nTransfer-Encoding: chunked\r\nContent-Length: 99\r\n\r\n3\r\nabc\r\n0\r\n\r\n";
        Diagnostics.Arrange("request", Request);

        await WriteAsync(connection, Request[..^9]);
        Assert.AreEqual(string.Empty, Observe("reply before the last chunk", string.Empty, await ReadAllAsync(connection)), "no reply before the last chunk");
        await WriteAsync(connection, Request[^9..^2]);
        Assert.AreEqual(string.Empty, Observe("reply before the empty line", string.Empty, await ReadAllAsync(connection)), "no reply before the empty line after the last chunk");
        await WriteAsync(connection, Request[^2..]);

        Assert.AreEqual("posted\n", Observe("reply", "posted\n", await ReadAllAsync(connection)));
        Assert.AreEqual(Request, Observe("recorded", Request, Text(server.ReceivedBytes)));
    }

    [TestMethod]
    [DataRow("0\r\n\r\n")]
    [DataRow("0\r\nChecksum: 1\r\n\r\n")]
    public async Task Chunked_EndsAfterTheLastChunkAndItsTrailers(string body)
    {
        Diagnostics.Arrange("body", body);
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "posted\n"))));

        Assert.AreEqual("posted\nposted\n", Observe("replies", "posted\nposted\n", await ExchangeAsync(connection, $"POST /1234 HTTP/1.1\r\ntransfer-encoding: CHUNKED\r\n\r\n{body}{Get}")));
    }

    [TestMethod]
    public async Task TwoRequestsOnOneConnection_AreAnsweredAndRecordedInOrder()
    {
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n"), Reply("data2", "second\n")));
        IConnection connection = await ConnectAsync(server);
        const string Second = "POST /12340002 HTTP/1.1\r\nContent-Length: 2\r\n\r\nhi";
        Diagnostics.Arrange("requests", Get + Second);

        Assert.AreEqual("first\n", Observe("first reply", "first\n", await ExchangeAsync(connection, Get)));
        Assert.AreEqual("second\n", Observe("second reply", "second\n", await ExchangeAsync(connection, Second)));

        Assert.AreEqual(Get + Second, Observe("recorded", Get + Second, Text(server.ReceivedBytes)));
    }

    [TestMethod]
    public async Task TwoRequestsInOneWrite_AreBothAnswered()
    {
        Diagnostics.Arrange("request", Get + "GET /12340002 HTTP/1.1\r\n\r\n");
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "first\n"), Reply("data2", "second\n"))));

        Assert.AreEqual("first\nsecond\n", Observe("replies", "first\nsecond\n", await ExchangeAsync(connection, Get + "GET /12340002 HTTP/1.1\r\n\r\n")));
    }

    [TestMethod]
    public async Task Connections_AreRecordedInTheOrderTheyWereWritten()
    {
        Diagnostics.Arrange("writes", "GET /a on the first connection, then GET /b on the second");
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n")));
        IConnection first = await ConnectAsync(server);
        IConnection second = await ConnectAsync(server);

        await WriteAsync(first, "GET /a HTTP/1.1\r\n\r\n");
        await WriteAsync(second, "GET /b HTTP/1.1\r\n\r\n");

        Assert.AreEqual("GET /a HTTP/1.1\r\n\r\nGET /b HTTP/1.1\r\n\r\n", Observe("recorded", "GET /a HTTP/1.1\r\n\r\nGET /b HTTP/1.1\r\n\r\n", Text(server.ReceivedBytes)));
    }

    [TestMethod]
    [DataRow("reply\nswsclose\n", "")]
    [DataRow("reply\n", "swsclose\n")]
    public async Task Swsclose_ClosesTheConnectionAfterTheReply(string data, string serverCommands)
    {
        Diagnostics.Arrange("data", data);
        Diagnostics.Arrange("servercmd", serverCommands);
        SwsHttpServerConnector server = new(Case(Reply("data", data), Reply("servercmd", serverCommands)));
        IConnection connection = await ConnectAsync(server);

        Assert.AreEqual(data, Observe("reply", data, await ExchangeAsync(connection, Get)));
        Assert.AreEqual(string.Empty, Observe("second reply", string.Empty, await ExchangeAsync(connection, Get)), "the second request goes unanswered");
        Assert.AreEqual(Get, Observe("recorded", Get, Text(server.ReceivedBytes)), "bytes written after the close are not recorded");
    }

    [TestMethod]
    public async Task MissingPart_SendsNothingAndCloses()
    {
        Diagnostics.Arrange("request", "GET /12340005 with only a data part");
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "first\n"))));

        Assert.AreEqual(string.Empty, Observe("reply", string.Empty, await ExchangeAsync(connection, "GET /12340005 HTTP/1.1\r\n\r\n")));
        Assert.AreEqual(string.Empty, Observe("reply after close", string.Empty, await ExchangeAsync(connection, Get)));
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
        Diagnostics.Arrange("request", request);
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "first\n"))));

        string reply = await ExchangeAsync(connection, request);

        Diagnostics.Act("reply", reply);
        Diagnostics.Assert("reply start", "HTTP/1.1 404 Not Found\r\nServer: sws/1.0\r\nConnection: close\r\n", reply[..Math.Min(reply.Length, 60)]);
        StringAssert.StartsWith(reply, "HTTP/1.1 404 Not Found\r\nServer: sws/1.0\r\nConnection: close\r\n");
        StringAssert.EndsWith(reply, "</BODY></HTML>\n");
        Assert.AreEqual(string.Empty, Observe("reply after close", string.Empty, await ExchangeAsync(connection, Get)));
    }

    [TestMethod]
    [DataRow("base64=\"yes\"", "aGVsbG8K\n", "hello\n")]
    [DataRow("base64=\"yes\"", "not base64!\n", "")]
    [DataRow("nonewline=\"yes\"", "hello\n", "hello")]
    [DataRow("base64=\"yes\" nonewline=\"yes\"", "aGVsbG8K\n", "hello")]
    public async Task PartAttributes_AreAppliedAsSwsGetpartDoes(string attributes, string content, string expected)
    {
        Diagnostics.Arrange("attributes", attributes);
        Diagnostics.Arrange("content", content);
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply($"data {attributes}", content))));

        Assert.AreEqual(expected, Observe("reply", expected, await ExchangeAsync(connection, Get)));
    }

    [TestMethod]
    public async Task EmptyPartWithNonewline_SendsNothing()
    {
        Diagnostics.Arrange("part", "data nonewline=\"yes\" with empty content");
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data nonewline=\"yes\"", string.Empty))));

        Assert.AreEqual(string.Empty, Observe("reply", string.Empty, await ExchangeAsync(connection, Get)));
    }

    [TestMethod]
    public async Task AuthRequired_EndsARequestWithNoAuthorizationAtItsHeaders()
    {
        Diagnostics.Arrange("servercmd", "auth_required");
        SwsHttpServerConnector server = new(Case(Reply("data", "denied\n"), Reply("data2", "second\n"), Reply("servercmd", "auth_required\n")));
        IConnection connection = await ConnectAsync(server);

        Assert.AreEqual("denied\n", Observe("reply", "denied\n", await ExchangeAsync(connection, "PUT /1234 HTTP/1.1\r\nContent-Length: 5\r\n\r\n")), "the body is not waited for");
        Assert.AreEqual(string.Empty, Observe("reply to body bytes", string.Empty, await ExchangeAsync(connection, "abc")), "body bytes start the next request");
        Assert.AreEqual("second\n", Observe("reply to next request", "second\n", await ExchangeAsync(connection, "de /12340002 HTTP/1.1\r\n\r\n")), "the body bytes are read as the next request");
    }

    [TestMethod]
    [DataRow("Authorization: Basic dXNlcjpwYXNz\r\n", "")]
    [DataRow("", "Authorization: in the body")]
    public async Task AuthRequired_WithAuthorizationAnywhereInTheRequest_ReadsTheBody(string headers, string body)
    {
        Diagnostics.Arrange("headers", headers);
        Diagnostics.Arrange("body", body);
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "posted\n"), Reply("servercmd", "auth_required\n"))));
        string request = $"PUT /1234 HTTP/1.1\r\n{headers}Content-Length: {body.Length + 2}\r\n\r\n{body}";

        Assert.AreEqual(string.Empty, Observe("reply before the body is complete", string.Empty, await ExchangeAsync(connection, request)), "no reply before the body is complete");
        Assert.AreEqual("posted\n", Observe("reply", "posted\n", await ExchangeAsync(connection, "ok")));
    }

    [TestMethod]
    public async Task AuthRequired_DoesNotChangeAChunkedRequest()
    {
        Diagnostics.Arrange("servercmd", "auth_required");
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "posted\n"), Reply("servercmd", "auth_required\n"))));

        Assert.AreEqual(string.Empty, Observe("reply to headers", string.Empty, await ExchangeAsync(connection, "POST /1234 HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\n")));
        Assert.AreEqual("posted\n", Observe("reply to chunks", "posted\n", await ExchangeAsync(connection, "3\r\nabc\r\n0\r\n\r\n")));
    }

    [TestMethod]
    [DataRow("Content-Length: 100\r\nexpect: 100-Continue\r\n")]
    [DataRow("Expect: 100-continue\r\nContent-Length: 100\r\n")]
    [DataRow("Expect: 100-continue\r\nContent-Length: 0\r\nContent-Length: 100\r\n")]
    public async Task NoExpect_EndsARequestThatExpectsContinueAtItsHeaders(string headers)
    {
        Diagnostics.Arrange("headers", headers);
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "refused\n"), Reply("servercmd", "no-expect\n"))));

        Assert.AreEqual("refused\n", Observe("reply", "refused\n", await ExchangeAsync(connection, $"PUT /1234 HTTP/1.1\r\n{headers}\r\n")));
    }

    [TestMethod]
    [DataRow("no-expect\n", "Content-Length: 3\r\n")]
    [DataRow("", "Expect: 100-continue\r\nContent-Length: 3\r\n")]
    public async Task ExpectContinue_WithoutNoExpectOrWithoutTheHeader_ReadsTheBody(string serverCommands, string headers)
    {
        Diagnostics.Arrange("servercmd", serverCommands);
        Diagnostics.Arrange("headers", headers);
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "posted\n"), Reply("servercmd", serverCommands))));

        Assert.AreEqual(string.Empty, Observe("reply to headers", string.Empty, await ExchangeAsync(connection, $"PUT /1234 HTTP/1.1\r\n{headers}\r\n")));
        Assert.AreEqual("posted\n", Observe("reply to body", "posted\n", await ExchangeAsync(connection, "abc")));
    }

    [TestMethod]
    [DataRow("skip: 3\n", 2)]
    [DataRow("skip: 5\n", 0)]
    [DataRow("skip: 9\nskip: 1\n", 4)]
    [DataRow("skip:-2\n", 7)]
    [DataRow("skip: 99999999999\n", 5)]
    public async Task Skip_ReadsThatManyBytesLessThanContentLength(string serverCommands, int bodyBytesRead)
    {
        Diagnostics.Arrange("servercmd", serverCommands);
        Diagnostics.Arrange("body bytes read before the reply", bodyBytesRead);
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "posted\n"), Reply("servercmd", serverCommands))));

        string expectedFirst = bodyBytesRead == 0 ? "posted\n" : string.Empty;
        Assert.AreEqual(expectedFirst, Observe("reply to headers", expectedFirst, await ExchangeAsync(connection, "POST /1234 HTTP/1.1\r\nContent-Length: 5\r\n\r\n")));
        for (int read = 1; read <= bodyBytesRead; read++)
        {
            string expectedAfter = read == bodyBytesRead ? "posted\n" : string.Empty;
            Assert.AreEqual(expectedAfter, Observe($"reply after {read} body bytes", expectedAfter, await ExchangeAsync(connection, "x")), $"after {read} body bytes");
        }
    }

    [TestMethod]
    [DataRow("Content-Length: 2\r\n")]
    [DataRow("Content-Length: 0\r\nContent-Length: 50\r\n")]
    public async Task Skip_BeyondContentLength_NeverEndsTheRequest(string headers)
    {
        Diagnostics.Arrange("headers", headers);
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "posted\n"), Reply("servercmd", "skip: 5\n"))));

        Assert.AreEqual(string.Empty, Observe("reply", string.Empty, await ExchangeAsync(connection, $"POST /1234 HTTP/1.1\r\n{headers}\r\n" + new string('x', 100))));
    }

    [TestMethod]
    [DataRow("Content-Length: abc\r\n")]
    [DataRow("")]
    public async Task Skip_WithNoValidContentLength_EndsTheRequestAtTheHeaders(string headers)
    {
        Diagnostics.Arrange("headers", headers);
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "posted\n"), Reply("servercmd", "skip: 5\n"))));

        Assert.AreEqual("posted\n", Observe("reply", "posted\n", await ExchangeAsync(connection, $"POST /1234 HTTP/1.1\r\n{headers}\r\n")));
    }

    [TestMethod]
    [DataRow("delay: +5\n")]
    [DataRow("delay:-1\n")]
    [DataRow("delay:\t1000\r\n")]
    public void Delay_IsReportedAsUnsupported(string serverCommands)
    {
        Diagnostics.Arrange("servercmd", serverCommands);
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n"), Reply("servercmd", serverCommands)));

        string[] unsupported = server.UnsupportedServerCommands.ToArray();
        Diagnostics.Act("unsupported", string.Join(",", unsupported));
        Diagnostics.Assert("unsupported", "delay", string.Join(",", unsupported));
        CollectionAssert.AreEqual(new[] { "delay" }, unsupported);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("swsclose\n")]
    [DataRow("Testnum 1234\npipe: 3\n")]
    [DataRow("skip: many\nskip:\ndelay: -\nwritedelay: x\n")]
    [DataRow("auth_required\nno-expect\nskip: 100\nskip:-1\n")]
    [DataRow("idle\nstream\nconnection-monitor\nupgrade\nwritedelay: 1000\n")]
    public void ServerCommandsThatAreCarriedOutOrUnknown_AreNotReported(string serverCommands)
    {
        Diagnostics.Arrange("servercmd", serverCommands);
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n"), Reply("servercmd", serverCommands)));

        Diagnostics.Act("unsupported", string.Join(",", server.UnsupportedServerCommands));
        Diagnostics.Assert("unsupported count", 0, server.UnsupportedServerCommands.Count);
        Assert.IsEmpty(server.UnsupportedServerCommands);
    }

    [TestMethod]
    public void UnsupportedServerCommands_AreReportedOncePerLineInFileOrder()
    {
        Diagnostics.Arrange("servercmd", "delay: 1\n\nidle\nauth_required\ndelay: 2\n");
        SwsHttpServerConnector server = new(Case(Reply("servercmd", "delay: 1\n\nidle\nauth_required\ndelay: 2\n")));

        string[] unsupported = server.UnsupportedServerCommands.ToArray();
        Diagnostics.Act("unsupported", string.Join(",", unsupported));
        Diagnostics.Assert("unsupported", "delay,delay", string.Join(",", unsupported));
        CollectionAssert.AreEqual(new[] { "delay", "delay" }, unsupported);
    }

    [TestMethod]
    public void NoServerCommandPart_ReportsNothing()
    {
        Diagnostics.Arrange("parts", "data only, no servercmd");
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n")));

        Diagnostics.Act("unsupported", string.Join(",", server.UnsupportedServerCommands));
        Diagnostics.Assert("unsupported count", 0, server.UnsupportedServerCommands.Count);
        Assert.IsEmpty(server.UnsupportedServerCommands);
    }

    [TestMethod]
    public async Task Connection_ReadsIntoASmallBufferInPieces()
    {
        Diagnostics.Arrange("reply", "first\n read into a 4 byte buffer");
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "first\n"))));
        await WriteAsync(connection, Get);
        byte[] buffer = new byte[4];

        Assert.AreEqual(4, ObserveValue("first read count", 4, await connection.ReadAsync(buffer, CancellationToken.None)));
        Assert.AreEqual("firs", Observe("first read", "firs", Encoding.Latin1.GetString(buffer)));
        Assert.AreEqual(2, ObserveValue("second read count", 2, await connection.ReadAsync(buffer, CancellationToken.None)));
        Assert.AreEqual(0, ObserveValue("third read count", 0, await connection.ReadAsync(buffer, CancellationToken.None)));
    }

    [TestMethod]
    public async Task Connection_IsPlainAndHasNoAddress()
    {
        Diagnostics.Arrange("target", "plain connection to 127.0.0.1:8990");
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "first\n"))));

        Assert.IsFalse(ObserveValue("IsSecure", false, connection.IsSecure));
        Assert.IsNull(connection.RemoteEndPoint);
        Diagnostics.Assert("RemoteEndPoint is null", true, connection.RemoteEndPoint is null);
        await connection.FlushAsync(CancellationToken.None);
        await connection.DisposeAsync();
    }

    [TestMethod]
    public async Task Abandon_MakesConnectingReadingAndWritingThrow()
    {
        Diagnostics.Arrange("action", "Abandon after one connection");
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n")));
        IConnection connection = await ConnectAsync(server);

        server.Abandon();

        IOException connectException = await Assert.ThrowsExactlyAsync<IOException>(async () => await server.ConnectAsync(Target, CancellationToken.None));
        ObserveValue("connect exception type", nameof(IOException), connectException.GetType().Name);
        IOException readException = await Assert.ThrowsExactlyAsync<IOException>(async () => await connection.ReadAsync(new byte[1], CancellationToken.None));
        ObserveValue("read exception type", nameof(IOException), readException.GetType().Name);
        IOException writeException = await Assert.ThrowsExactlyAsync<IOException>(async () => await connection.WriteAsync(new byte[1], CancellationToken.None));
        ObserveValue("write exception type", nameof(IOException), writeException.GetType().Name);
    }

    [TestMethod]
    public void Constructor_RejectsANullCase()
    {
        Diagnostics.Arrange("case", "null");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => new SwsHttpServerConnector(null!));

        ObserveValue("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public void Constructor_RejectsANullTimeProvider()
    {
        Diagnostics.Arrange("time provider", "null");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => new SwsHttpServerConnector(Case(Reply("data", "first\n")), null!));

        ObserveValue("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public async Task WriteDelay_SendsTheReplyInWritesOf20BytesWithTheDelayAfterEach()
    {
        Diagnostics.Arrange("reply", FortyFiveBytes);
        Diagnostics.Arrange("servercmd", "writedelay: 100");
        ManualTimeProvider clock = new();
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", FortyFiveBytes), Reply("servercmd", "writedelay: 100\n")), clock));
        await WriteAsync(connection, Get);

        Assert.AreEqual(FortyFiveBytes[..20], Observe("first write", FortyFiveBytes[..20], await ReadOnceAsync(connection)), "the first write is sent at once");
        Task<string> second = ReadOnceAsync(connection);
        clock.Advance(TimeSpan.FromMilliseconds(99));
        Assert.IsFalse(ObserveValue("second write completed at 99 ms", false, second.IsCompleted), "the second write waits out the first write's delay");
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.AreEqual(FortyFiveBytes[20..40], Observe("second write", FortyFiveBytes[20..40], await second));
        Task<string> third = ReadOnceAsync(connection);
        clock.Advance(TimeSpan.FromMilliseconds(100));
        Assert.AreEqual(FortyFiveBytes[40..], Observe("third write", FortyFiveBytes[40..], await third));
        Assert.AreEqual(string.Empty, Observe("read after the last write", string.Empty, await ReadOnceAsync(connection)), "the connection stays open with nothing more to read");
    }

    [TestMethod]
    public async Task WriteDelay_WhenTheTimerFiresEarly_WaitsForTheWriteInsteadOfReadingNothing()
    {
        Diagnostics.Arrange("timers fire early by", "1 ms");
        ManualTimeProvider clock = new() { TimersFireEarlyBy = TimeSpan.FromMilliseconds(1) };
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", FortyFiveBytes), Reply("servercmd", "writedelay: 100\n")), clock));
        await WriteAsync(connection, Get);
        Diagnostics.Act("first write", await ReadOnceAsync(connection));

        Task<string> second = ReadOnceAsync(connection);
        clock.Advance(TimeSpan.FromMilliseconds(99));
        Assert.IsFalse(ObserveValue("second write completed at 99 ms", false, second.IsCompleted), "a timer that fired at 99 ms does not end the read with 0 bytes");
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.AreEqual(FortyFiveBytes[20..40], Observe("second write", FortyFiveBytes[20..40], await second));
    }

    [TestMethod]
    public async Task WriteDelay_WithAClosingReply_ClosesAfterTheLastWritesDelay()
    {
        Diagnostics.Arrange("reply", "reply swsclose\n");
        ManualTimeProvider clock = new();
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "reply swsclose\n"), Reply("servercmd", "writedelay: 100\n")), clock));
        await WriteAsync(connection, Get);

        Assert.AreEqual("reply swsclose\n", Observe("reply", "reply swsclose\n", await ReadOnceAsync(connection)));
        Task<string> close = ReadOnceAsync(connection);
        clock.Advance(TimeSpan.FromMilliseconds(99));
        Assert.IsFalse(ObserveValue("close completed at 99 ms", false, close.IsCompleted));
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.AreEqual(string.Empty, Observe("read at the close", string.Empty, await close));
    }

    [TestMethod]
    [DataRow("writedelay: 0\n")]
    [DataRow("writedelay: -5\n")]
    [DataRow("writedelay: 100\nwritedelay: 0\n")]
    public async Task WriteDelay_OfZeroOrLess_SendsTheWholeReplyAtOnce(string serverCommands)
    {
        Diagnostics.Arrange("servercmd", serverCommands);
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", FortyFiveBytes), Reply("servercmd", serverCommands)), new ManualTimeProvider()));
        await WriteAsync(connection, Get);

        Assert.AreEqual(FortyFiveBytes, Observe("reply", FortyFiveBytes, await ReadOnceAsync(connection)));
    }

    [TestMethod]
    public async Task PostcmdWait_HoldsTheCloseBackThatManySecondsAndTheSettleTime()
    {
        Diagnostics.Arrange("postcmd", "wait 2");
        ManualTimeProvider clock = new();
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "reply swsclose\n"), Reply("postcmd", "wait 2\n")), clock));
        await WriteAsync(connection, Get);

        Assert.AreEqual("reply swsclose\n", Observe("reply", "reply swsclose\n", await ReadOnceAsync(connection)));
        Task<string> close = ReadOnceAsync(connection);
        clock.Advance(TimeSpan.FromSeconds(2) + TimeSpan.FromMilliseconds(250) - TimeSpan.FromTicks(1));
        Assert.IsFalse(ObserveValue("close completed one tick early", false, close.IsCompleted));
        clock.Advance(TimeSpan.FromTicks(1));
        Assert.AreEqual(string.Empty, Observe("read at the close", string.Empty, await close));
    }

    [TestMethod]
    public async Task PostcmdWait_WhenTheTimerFiresSecondsLate_HoldsTheCloseSoTheClientsOverdueTimeoutGoesFirst()
    {
        Diagnostics.Arrange("postcmd", "wait 2, timers fire 8 s late");
        ManualTimeProvider clock = new() { TimersFireLateBy = TimeSpan.FromSeconds(8) };
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "reply swsclose\n"), Reply("postcmd", "wait 2\n")), clock));
        await WriteAsync(connection, Get);
        Assert.AreEqual("reply swsclose\n", Observe("reply", "reply swsclose\n", await ReadOnceAsync(connection)));
        using CancellationTokenSource maxTime = new();

        Task<string> close = ReadOnceAsync(connection, maxTime.Token);
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.IsFalse(ObserveValue("close completed at 10 s", false, close.IsCompleted), "a close found 8 s overdue waits for the timers that fell due in the stall");
        await maxTime.CancelAsync();

        TaskCanceledException exception = await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => close);
        ObserveValue("exception type", nameof(TaskCanceledException), exception.GetType().Name);
    }

    [TestMethod]
    public async Task PostcmdWait_WhenTheTimerFiresSecondsLate_ClosesOnceTheOverdueTimersHaveHadTheirTurn()
    {
        Diagnostics.Arrange("postcmd", "wait 2, timers fire 8 s late");
        ManualTimeProvider clock = new() { TimersFireLateBy = TimeSpan.FromSeconds(8) };
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "reply swsclose\n"), Reply("postcmd", "wait 2\n")), clock));
        await WriteAsync(connection, Get);
        Assert.AreEqual("reply swsclose\n", Observe("reply", "reply swsclose\n", await ReadOnceAsync(connection)));

        Task<string> close = ReadOnceAsync(connection);
        clock.Advance(TimeSpan.FromSeconds(10));
        clock.Advance(TimeSpan.FromSeconds(8) + TimeSpan.FromMilliseconds(250) - TimeSpan.FromTicks(1));
        Assert.IsFalse(ObserveValue("close completed one tick early", false, close.IsCompleted));
        clock.Advance(TimeSpan.FromTicks(1));

        Assert.AreEqual(string.Empty, Observe("read at the close", string.Empty, await close));
    }

    [TestMethod]
    public async Task PostcmdWait_OfOneSecondWokenSecondsLate_ClosesOnceTheOverdueTimersHaveHadTheirTurn()
    {
        Diagnostics.Arrange("postcmd", "wait 1, timers fire 8 s late");
        ManualTimeProvider clock = new() { TimersFireLateBy = TimeSpan.FromSeconds(8) };
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "reply swsclose\n"), Reply("postcmd", "wait 1\n")), clock));
        await WriteAsync(connection, Get);
        Assert.AreEqual("reply swsclose\n", Observe("reply", "reply swsclose\n", await ReadOnceAsync(connection)));

        Task<string> close = ReadOnceAsync(connection);
        clock.Advance(TimeSpan.FromSeconds(9));
        clock.Advance(TimeSpan.FromSeconds(8) + TimeSpan.FromMilliseconds(250) - TimeSpan.FromTicks(1));
        Assert.IsFalse(ObserveValue("close completed one tick early", false, close.IsCompleted));
        clock.Advance(TimeSpan.FromTicks(1));

        Assert.AreEqual(string.Empty, Observe("read at the close", string.Empty, await close));
    }

    [TestMethod]
    public async Task PostcmdWait_LongerThanASecondAndWokenOnTime_HoldsTheCloseSoATimeoutStarvedInsideTheWaitGoesFirst()
    {
        Diagnostics.Arrange("postcmd", "wait 10");
        ManualTimeProvider clock = new();
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "reply swsclose\n"), Reply("postcmd", "wait 10\n")), clock));
        await WriteAsync(connection, Get);
        Assert.AreEqual("reply swsclose\n", Observe("reply", "reply swsclose\n", await ReadOnceAsync(connection)));
        using CancellationTokenSource maxTime = new();

        Task<string> close = ReadOnceAsync(connection, maxTime.Token);
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.IsFalse(ObserveValue("close completed at 10 s", false, close.IsCompleted), "a stall from 1.9 s to 10.4 s wakes the close on time beside a -m 8 s overdue");
        await maxTime.CancelAsync();

        TaskCanceledException exception = await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => close);
        ObserveValue("exception type", nameof(TaskCanceledException), exception.GetType().Name);
    }

    [TestMethod]
    public async Task PostcmdWait_LongerThanASecondAndWokenOnTime_ClosesOnceTheOverdueTimersHaveHadTheirTurn()
    {
        Diagnostics.Arrange("postcmd", "wait 10");
        ManualTimeProvider clock = new();
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "reply swsclose\n"), Reply("postcmd", "wait 10\n")), clock));
        await WriteAsync(connection, Get);
        Assert.AreEqual("reply swsclose\n", Observe("reply", "reply swsclose\n", await ReadOnceAsync(connection)));

        Task<string> close = ReadOnceAsync(connection);
        clock.Advance(TimeSpan.FromSeconds(10) + TimeSpan.FromMilliseconds(250) - TimeSpan.FromTicks(1));
        Assert.IsFalse(ObserveValue("close completed one tick early", false, close.IsCompleted));
        clock.Advance(TimeSpan.FromTicks(1));

        Assert.AreEqual(string.Empty, Observe("read at the close", string.Empty, await close));
    }

    [TestMethod]
    [DataRow("wait 1\n", 1, 0)]
    [DataRow("wait 1\nwait 2\n", 3, 250)]
    [DataRow("  wait\t2\r\n", 2, 250)]
    [DataRow("wait -3\nwait 1\n", 1, 0)]
    [DataRow("sleep 5\nwait\nwait x\nwait10 1\nWAIT 4\nwait 1\n", 1, 0)]
    public async Task PostcmdWait_HoldsTheNextReplyBackThatManySecondsAndAnyLongWaitsSettleTime(string postCommands, int seconds, int settleMilliseconds)
    {
        Diagnostics.Arrange("postcmd", postCommands);
        Diagnostics.Arrange("seconds", seconds);
        Diagnostics.Arrange("settle milliseconds", settleMilliseconds);
        ManualTimeProvider clock = new();
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "first\n"), Reply("postcmd", postCommands)), clock));
        await WriteAsync(connection, Get);
        Assert.AreEqual("first\n", Observe("first reply", "first\n", await ReadOnceAsync(connection)));
        await WriteAsync(connection, Get);

        Task<string> next = ReadOnceAsync(connection);
        clock.Advance(TimeSpan.FromSeconds(seconds) + TimeSpan.FromMilliseconds(settleMilliseconds) - TimeSpan.FromTicks(1));
        Assert.IsFalse(ObserveValue("next reply completed one tick early", false, next.IsCompleted));
        clock.Advance(TimeSpan.FromTicks(1));
        Assert.AreEqual("first\n", Observe("next reply", "first\n", await next));
    }

    [TestMethod]
    public async Task PostcmdWait_DoesNotHoldBackTheNotFoundDocument()
    {
        Diagnostics.Arrange("request", "GARBAGE with postcmd wait 5 and writedelay 100");
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "first\n"), Reply("postcmd", "wait 5\n"), Reply("servercmd", "writedelay: 100\n")), new ManualTimeProvider()));

        string reply = await ExchangeAsync(connection, "GARBAGE\r\n\r\n");

        Diagnostics.Act("reply", reply);
        Diagnostics.Assert("reply ends with the document", "</BODY></HTML>\n", reply[Math.Max(0, reply.Length - 15)..]);
        StringAssert.EndsWith(reply, "</BODY></HTML>\n");
    }

    [TestMethod]
    public async Task Idle_AnswersNothingAndAReadWaitsUntilCancelled()
    {
        Diagnostics.Arrange("servercmd", "idle");
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n"), Reply("servercmd", "idle\n")));
        IConnection connection = await ConnectAsync(server);
        using CancellationTokenSource cancellation = new();
        await WriteAsync(connection, Get);
        await WriteAsync(connection, Get);

        Task<string> read = ReadOnceAsync(connection, cancellation.Token);
        Assert.IsFalse(ObserveValue("read completed", false, read.IsCompleted));
        await cancellation.CancelAsync();

        TaskCanceledException exception = await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => read);
        ObserveValue("exception type", nameof(TaskCanceledException), exception.GetType().Name);
        Assert.AreEqual(Get + Get, Observe("recorded", Get + Get, Text(server.ReceivedBytes)), "idle keeps reading requests");
    }

    [TestMethod]
    public async Task Idle_DoesNotApplyToAMalformedRequest()
    {
        Diagnostics.Arrange("request", "GARBAGE with servercmd idle");
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "first\n"), Reply("servercmd", "idle\n"))));

        string reply = await ExchangeAsync(connection, "GARBAGE\r\n\r\n");

        Diagnostics.Act("reply", reply);
        Diagnostics.Assert("reply start", "HTTP/1.1 404 Not Found\r\n", reply[..Math.Min(reply.Length, 24)]);
        StringAssert.StartsWith(reply, "HTTP/1.1 404 Not Found\r\n");
    }

    [TestMethod]
    public async Task Stream_SendsTheStreamedTextWithoutEndAndReadsNoMore()
    {
        Diagnostics.Arrange("servercmd", "idle, stream");
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n"), Reply("servercmd", "idle\nstream\n")));
        IConnection connection = await ConnectAsync(server);
        await WriteAsync(connection, Get);

        string streamed = await ReadOnceAsync(connection, 20) + await ReadOnceAsync(connection, 20) + await ReadOnceAsync(connection, 20);
        await WriteAsync(connection, Get);

        string expectedStream = string.Concat(Enumerable.Repeat("a string to stream 01234567890\n", 2))[..60];
        Assert.AreEqual(expectedStream, Observe("streamed", expectedStream, streamed));
        Assert.AreEqual(Get, Observe("recorded", Get, Text(server.ReceivedBytes)), "sws reads nothing more while it streams");
    }

    [TestMethod]
    public async Task ConnectionMonitor_RecordsADisconnectWhenTheClientCloses()
    {
        Diagnostics.Arrange("servercmd", "connection-monitor");
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n"), Reply("servercmd", "connection-monitor\n")));
        IConnection connection = await ConnectAsync(server);
        Diagnostics.Act("reply", await ExchangeAsync(connection, Get));

        await connection.DisposeAsync();
        await connection.DisposeAsync();

        Assert.AreEqual(Get + "[DISCONNECT]\n", Observe("recorded", Get + "[DISCONNECT]\n", Text(server.ReceivedBytes)));
    }

    [TestMethod]
    public async Task ConnectionMonitor_RecordsADisconnectWhenTheServerCloses()
    {
        Diagnostics.Arrange("servercmd", "connection-monitor, reply closes");
        SwsHttpServerConnector server = new(Case(Reply("data", "reply swsclose\n"), Reply("servercmd", "connection-monitor\n")));
        IConnection connection = await ConnectAsync(server);

        Diagnostics.Act("reply", await ExchangeAsync(connection, Get));
        Assert.AreEqual(Get + "[DISCONNECT]\n", Observe("recorded after the server closes", Get + "[DISCONNECT]\n", Text(server.ReceivedBytes)));
        await connection.DisposeAsync();

        Assert.AreEqual(Get + "[DISCONNECT]\n", Observe("recorded after dispose", Get + "[DISCONNECT]\n", Text(server.ReceivedBytes)));
    }

    [TestMethod]
    [DataRow("connection-monitor\n", "")]
    [DataRow("", "GET /1234 HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n")]
    public async Task ConnectionMonitor_RecordsNothingWithoutARequestOrWithoutTheCommand(string serverCommands, string request)
    {
        Diagnostics.Arrange("servercmd", serverCommands);
        Diagnostics.Arrange("request", request);
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n"), Reply("servercmd", serverCommands)));
        IConnection connection = await ConnectAsync(server);
        Diagnostics.Act("reply", await ExchangeAsync(connection, request));

        await connection.DisposeAsync();

        Assert.AreEqual(request, Observe("recorded", request, Text(server.ReceivedBytes)));
    }

    [TestMethod]
    public async Task ConnectionMonitor_IsOneFlagForTheServer_SoOnlyTheFirstCloseAfterARequestIsRecorded()
    {
        Diagnostics.Arrange("servercmd", "connection-monitor, two connections");
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n"), Reply("servercmd", "connection-monitor\n")));
        IConnection first = await ConnectAsync(server);
        IConnection second = await ConnectAsync(server);
        Diagnostics.Act("first reply", await ExchangeAsync(first, Get));
        Diagnostics.Act("second reply", await ExchangeAsync(second, Get));

        await first.DisposeAsync();
        await second.DisposeAsync();

        Assert.AreEqual(Get + Get + "[DISCONNECT]\n", Observe("recorded", Get + Get + "[DISCONNECT]\n", Text(server.ReceivedBytes)));
    }

    [TestMethod]
    public async Task Upgrade_AnswersAtTheHeadersThenRecordsTrafficUntilTheClientIsQuietForASecond()
    {
        Diagnostics.Arrange("request", UpgradeRequest + "abc");
        ManualTimeProvider clock = new();
        SwsHttpServerConnector server = new(Case(Reply("data", "switched\n"), Reply("servercmd", "upgrade\n")), clock);
        IConnection connection = await ConnectAsync(server);
        await WriteAsync(connection, UpgradeRequest + "abc");

        Assert.AreEqual("switched\n", Observe("reply", "switched\n", await ReadOnceAsync(connection)), "the body is not waited for");
        Task<string> close = ReadOnceAsync(connection);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        await WriteAsync(connection, "def");
        clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.IsFalse(ObserveValue("close completed at 1000 ms", false, close.IsCompleted), "the write at 500 ms moves the close to 1500 ms");
        clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.AreEqual(string.Empty, Observe("read at the close", string.Empty, await close));
        await WriteAsync(connection, "ghi");

        Assert.AreEqual(UpgradeRequest + "abcdef", Observe("recorded", UpgradeRequest + "abcdef", Text(server.ReceivedBytes)));
    }

    [TestMethod]
    public async Task Upgrade_WriteAfterAQuietSecond_FindsTheConnectionClosed()
    {
        Diagnostics.Arrange("servercmd", "upgrade, connection-monitor");
        ManualTimeProvider clock = new();
        SwsHttpServerConnector server = new(Case(Reply("data", "switched\n"), Reply("servercmd", "upgrade\nconnection-monitor\n")), clock);
        IConnection connection = await ConnectAsync(server);
        await WriteAsync(connection, UpgradeRequest);
        Assert.AreEqual("switched\n", Observe("reply", "switched\n", await ReadOnceAsync(connection)));

        clock.Advance(TimeSpan.FromSeconds(1));
        await WriteAsync(connection, "late");

        Assert.AreEqual(string.Empty, Observe("read after the late write", string.Empty, await ReadOnceAsync(connection)));
        Assert.AreEqual(UpgradeRequest + "[DISCONNECT]\n", Observe("recorded", UpgradeRequest + "[DISCONNECT]\n", Text(server.ReceivedBytes)));
    }

    [TestMethod]
    public async Task Upgrade_QuietSecondStartsAfterThePostReplyWaitAndCloseWaitsTheSettleTime()
    {
        Diagnostics.Arrange("servercmd and postcmd", "upgrade, wait 2");
        ManualTimeProvider clock = new();
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "switched\n"), Reply("servercmd", "upgrade\n"), Reply("postcmd", "wait 2\n")), clock));
        await WriteAsync(connection, UpgradeRequest);
        Assert.AreEqual("switched\n", Observe("reply", "switched\n", await ReadOnceAsync(connection)));

        Task<string> close = ReadOnceAsync(connection);
        clock.Advance(TimeSpan.FromSeconds(3) + TimeSpan.FromMilliseconds(250) - TimeSpan.FromTicks(1));
        Assert.IsFalse(ObserveValue("close completed one tick early", false, close.IsCompleted));
        clock.Advance(TimeSpan.FromTicks(1));

        Assert.AreEqual(string.Empty, Observe("read at the close", string.Empty, await close));
    }

    [TestMethod]
    public async Task Upgrade_WithAClosingReply_ClosesWithoutReadingTraffic()
    {
        Diagnostics.Arrange("request", UpgradeRequest);
        SwsHttpServerConnector server = new(Case(Reply("data", "switched swsclose\n"), Reply("servercmd", "upgrade\n")));
        IConnection connection = await ConnectAsync(server);

        Assert.AreEqual("switched swsclose\n", Observe("reply", "switched swsclose\n", await ExchangeAsync(connection, UpgradeRequest)));
        await WriteAsync(connection, "traffic");

        Assert.AreEqual(UpgradeRequest, Observe("recorded", UpgradeRequest, Text(server.ReceivedBytes)));
    }

    [TestMethod]
    public async Task Upgrade_WithStream_Streams()
    {
        Diagnostics.Arrange("servercmd", "upgrade, stream");
        SwsHttpServerConnector server = new(Case(Reply("data", "switched\n"), Reply("servercmd", "upgrade\nstream\n")));
        IConnection connection = await ConnectAsync(server);
        await WriteAsync(connection, UpgradeRequest);

        Assert.AreEqual("a string", Observe("streamed", "a string", await ReadOnceAsync(connection, 8)));
        await WriteAsync(connection, "traffic");
        Assert.AreEqual(UpgradeRequest, Observe("recorded", UpgradeRequest, Text(server.ReceivedBytes)));
    }

    [TestMethod]
    [DataRow("", "Upgrade: websocket\r\n")]
    [DataRow("upgrade\n", "upgrade: websocket\r\n")]
    public async Task Upgrade_NotAllowedOrNotAskedFor_ReadsTheBody(string serverCommands, string headers)
    {
        Diagnostics.Arrange("servercmd", serverCommands);
        Diagnostics.Arrange("headers", headers);
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "posted\n"), Reply("servercmd", serverCommands))));

        Assert.AreEqual(string.Empty, Observe("reply to headers", string.Empty, await ExchangeAsync(connection, $"POST /1234 HTTP/1.1\r\n{headers}Content-Length: 3\r\n\r\n")));
        Assert.AreEqual("posted\n", Observe("reply to body", "posted\n", await ExchangeAsync(connection, "abc")));
    }

    [TestMethod]
    public async Task Upgrade_WithAuthRequiredAndNoAuthorization_IsAnOrdinaryRequestEndedAtItsHeaders()
    {
        Diagnostics.Arrange("servercmd", "upgrade, auth_required");
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "denied\n"), Reply("data2", "second\n"), Reply("servercmd", "upgrade\nauth_required\n"))));

        Assert.AreEqual("denied\n", Observe("reply", "denied\n", await ExchangeAsync(connection, UpgradeRequest)));
        Assert.AreEqual("second\n", Observe("next reply", "second\n", await ExchangeAsync(connection, "GET /12340002 HTTP/1.1\r\n\r\n")), "the connection still reads requests");
    }

    [TestMethod]
    [DataRow("/1234", "Authorization: Digest username=\"a\"\r\n", "data1000")]
    [DataRow("/12340002", "Authorization: Digest username=\"a\"\r\n", "data1002")]
    [DataRow("/1234", "Authorization: NTLM TlRMTVNTUAABAAAA\r\n", "data1001")]
    [DataRow("/1234", "Authorization: NTLM TlRMTVNTUAADAAAA\r\n", "data1002")]
    [DataRow("/1234", "Proxy-Authorization: NTLM TlRMTVNTUAABAAAA\r\n", "data1001")]
    [DataRow("/1234", "Authorization: NTLM TlRMTVNTUAACAAAA\r\n", "data")]
    [DataRow("/12341000", "Authorization: Basic dXNlcjpwYXNz\r\n", "data1001")]
    [DataRow("/1234", "Authorization: Basic dXNlcjpwYXNz\r\n", "data")]
    [DataRow("/1234", "Authorization: Negotiate YII=\r\n", "data1")]
    [DataRow("/1234", "Authorization: Negotiate YII=\r\nX: Authorization: Digest\r\n", "data1")]
    [DataRow("/1234", "Authorization: Digest a\r\nX: Authorization: NTLM TlRMTVNTUAADAAAA\r\n", "data1000")]
    [DataRow("/1234", "Authorization: NTLM TlRMTVNTUAADAAAA\r\nX: Authorization: NTLM TlRMTVNTUAAB\r\n", "data1002")]
    public async Task Authorization_SelectsThePartAsSwsDoes(string path, string headers, string expected)
    {
        Diagnostics.Arrange("path", path);
        Diagnostics.Arrange("headers", headers);
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(PartNamedCase("data", "data1", "data1000", "data1001", "data1002")));

        Assert.AreEqual(expected + "\n", Observe("reply", expected + "\n", await ExchangeAsync(connection, $"GET {path} HTTP/1.1\r\n{headers}\r\n")));
    }

    [TestMethod]
    [DataRow("", "Transfer-Encoding: chunked\r\nAuthorization: Digest a\r\n\r\n0\r\n\r\n", "data")]
    [DataRow("", "Authorization: Digest a\r\ntransfer-encoding: CHUNKED\r\n\r\n0\r\n\r\n", "data")]
    [DataRow("", "Content-Length: abc\r\nAuthorization: Digest a\r\n\r\n", "data")]
    [DataRow("", "Content-Length: 2\r\nContent-Length: abc\r\nAuthorization: Digest a\r\n\r\nhi", "data1000")]
    [DataRow("", "Content-Length: 0\r\nContent-Length: abc\r\nAuthorization: Digest a\r\n\r\n", "data")]
    [DataRow("skip: 2\n", "Content-Length: 2\r\nContent-Length: abc\r\nAuthorization: Digest a\r\n\r\n", "data")]
    [DataRow("no-expect\n", "Content-Length: 2\r\nExpect: 100-continue\r\nContent-Length: abc\r\nAuthorization: Digest a\r\n\r\n", "data")]
    [DataRow("", "Content-Length: 2\r\nExpect: 100-continue\r\nContent-Length: abc\r\nAuthorization: Digest a\r\n\r\nhi", "data1000")]
    public async Task Authorization_IsNotReachedWhereSwsStopsReadingTheHeaders(string serverCommands, string headers, string expected)
    {
        Diagnostics.Arrange("servercmd", serverCommands);
        Diagnostics.Arrange("headers", headers);
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "data\n"), Reply("data1000", "data1000\n"), Reply("servercmd", serverCommands))));

        Assert.AreEqual(expected + "\n", Observe("reply", expected + "\n", await ExchangeAsync(connection, $"POST /1234 HTTP/1.1\r\n{headers}")));
    }

    [TestMethod]
    public async Task Negotiate_CountsUpFromTheFirstNegotiateRequestsPart_AcrossConnections()
    {
        Diagnostics.Arrange("parts", "data, data3, data4, data5");
        SwsHttpServerConnector server = new(PartNamedCase("data", "data3", "data4", "data5"));
        const string Negotiate = "Authorization: Negotiate YII=\r\n\r\n";

        Assert.AreEqual("data3\n", Observe("first reply", "data3\n", await ExchangeAsync(await ConnectAsync(server), "GET /12340002 HTTP/1.1\r\n" + Negotiate)));
        Assert.AreEqual("data4\n", Observe("second reply", "data4\n", await ExchangeAsync(await ConnectAsync(server), "GET /1234 HTTP/1.1\r\n" + Negotiate)));
        Assert.AreEqual("data5\n", Observe("third reply", "data5\n", await ExchangeAsync(await ConnectAsync(server), "GET /12340007 HTTP/1.1\r\n" + Negotiate)));
    }

    [TestMethod]
    public async Task Swsbounce_GivesTheNextRequestThePreviousPartPlusOne_AcrossConnections()
    {
        Diagnostics.Arrange("parts", "data, data1000 (swsbounce), data1001 (swsbounce), data1002");
        SwsHttpServerConnector server = new(Case(
            Reply("data", "plain\n"),
            Reply("data1000", "swsbounce\n"),
            Reply("data1001", "bounced swsbounce\n"),
            Reply("data1002", "bounced twice\n")));

        IConnection first = await ConnectAsync(server);
        Assert.AreEqual("swsbounce\n", Observe("first reply", "swsbounce\n", await ExchangeAsync(first, "GET /1234 HTTP/1.1\r\nAuthorization: Digest a\r\n\r\n")));
        Assert.AreEqual("bounced swsbounce\n", Observe("second reply", "bounced swsbounce\n", await ExchangeAsync(await ConnectAsync(server), Get)));
        Assert.AreEqual("bounced twice\n", Observe("third reply", "bounced twice\n", await ExchangeAsync(first, Get)));
        Assert.AreEqual("plain\n", Observe("fourth reply", "plain\n", await ExchangeAsync(first, Get)), "a reply without swsbounce ends the bounce");
    }

    [TestMethod]
    public async Task Swsbounce_EndsAtTheNotFoundDocument()
    {
        Diagnostics.Arrange("parts", "data (swsbounce), data1");
        SwsHttpServerConnector server = new(Case(Reply("data", "swsbounce\n"), Reply("data1", "bounced\n")));

        Assert.AreEqual("swsbounce\n", Observe("first reply", "swsbounce\n", await ExchangeAsync(await ConnectAsync(server), Get)));
        string notFound = await ExchangeAsync(await ConnectAsync(server), "GARBAGE\r\n\r\n");
        Diagnostics.Act("not found reply", notFound);
        Diagnostics.Assert("not found reply start", "HTTP/1.1 404 Not Found", notFound[..Math.Min(notFound.Length, 22)]);
        StringAssert.StartsWith(notFound, "HTTP/1.1 404 Not Found");
        Assert.AreEqual("swsbounce\n", Observe("third reply", "swsbounce\n", await ExchangeAsync(await ConnectAsync(server), Get)));
    }

    [TestMethod]
    [DataRow("CONNECT test.remote.example.com.1234:8990 HTTP/1.1\r\n\r\n", "connect")]
    [DataRow("CONNECT test.1234:8990 HTTP/1.0\r\n\r\n", "connect")]
    [DataRow("CONNECT [::1]:8990 HTTP/1.1\r\n\r\n", "connect")]
    [DataRow("CONNECT test.1234:8990 HTTP/1.1\r\nProxy-Authorization: NTLM TlRMTVNTUAABAAAA\r\n\r\n", "connect1001")]
    [DataRow("CONNECT test.1234:8990 HTTP/1.1\r\nProxy-Authorization: NTLM TlRMTVNTUAADAAAA\r\n\r\n", "connect1002")]
    [DataRow("CONNECT host/12340002 HTTP/1.1\r\n\r\n", "data2")]
    [DataRow("CONNECT host:8990/1234 HTTP/1.1\r\n\r\n", "data")]
    [DataRow("CONNECTS test.1234:8990 HTTP/1.1\r\n\r\n", "data")]
    [DataRow("CONNECT test 1234 HTTP/1.1\r\n\r\n", "data")]
    [DataRow("connect test.1234:8990 HTTP/1.1\r\n\r\n", "data")]
    public async Task Connect_IsAnsweredFromTheConnectPart(string request, string expected)
    {
        Diagnostics.Arrange("request", request);
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(PartNamedCase("data", "data2", "connect", "connect1001", "connect1002")));

        Assert.AreEqual(expected + "\n", Observe("reply", expected + "\n", await ExchangeAsync(connection, request)));
    }

    [TestMethod]
    public async Task Connect_KeepsTheConnectionOpenForTheTunnelledRequest()
    {
        SwsHttpServerConnector server = new(PartNamedCase("data2", "connect"));
        IConnection connection = await ConnectAsync(server);
        const string Connect = "CONNECT test.1234:8990 HTTP/1.1\r\n\r\n";
        const string Tunnelled = "GET /12340002 HTTP/1.1\r\n\r\n";
        Diagnostics.Arrange("requests", Connect + Tunnelled);

        Assert.AreEqual("connect\n", Observe("connect reply", "connect\n", await ExchangeAsync(connection, Connect)));
        Assert.AreEqual("data2\n", Observe("tunnelled reply", "data2\n", await ExchangeAsync(connection, Tunnelled)));
        Assert.AreEqual(Connect + Tunnelled, Observe("recorded", Connect + Tunnelled, Text(server.ReceivedBytes)));
    }

    // Writes the ACT, ASSERT and DIFF lines for a string the test is about to assert, and returns it.
    private string Observe(string label, string expected, string actual)
    {
        TestDiagnostics diagnostics = Diagnostics;
        diagnostics.Act(label, actual);
        diagnostics.Assert(label, expected, actual);
        diagnostics.Diff(label, expected, actual);
        return actual;
    }

    // Writes the ACT and ASSERT lines for a non-string value the test is about to assert, and returns it.
    private T ObserveValue<T>(string label, T expected, T actual)
    {
        TestDiagnostics diagnostics = Diagnostics;
        diagnostics.Act(label, actual);
        diagnostics.Assert(label, expected, actual);
        return actual;
    }

    // Each part's content is its own name and a line feed.
    private static UpstreamTestCase PartNamedCase(params string[] partNames) =>
        Case([.. partNames.Select(name => Reply(name, name + "\n"))]);

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

    private static async Task<string> ReadOnceAsync(IConnection connection, CancellationToken cancellationToken = default) =>
        await ReadOnceAsync(connection, 1024, cancellationToken);

    private static async Task<string> ReadOnceAsync(IConnection connection, int bufferSize, CancellationToken cancellationToken = default)
    {
        byte[] buffer = new byte[bufferSize];
        int count = await connection.ReadAsync(buffer, cancellationToken);
        return Encoding.Latin1.GetString(buffer, 0, count);
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
