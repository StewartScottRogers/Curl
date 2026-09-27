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

    private const string UpgradeRequest = "GET /1234 HTTP/1.1\r\nUpgrade: websocket\r\nContent-Length: 5\r\n\r\n";

    private const string FortyFiveBytes = "aaaaaaaaaaaaaaaaaaaabbbbbbbbbbbbbbbbbbbbcccc\n";

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
    public async Task AuthRequired_EndsARequestWithNoAuthorizationAtItsHeaders()
    {
        SwsHttpServerConnector server = new(Case(Reply("data", "denied\n"), Reply("data2", "second\n"), Reply("servercmd", "auth_required\n")));
        IConnection connection = await ConnectAsync(server);

        Assert.AreEqual("denied\n", await ExchangeAsync(connection, "PUT /1234 HTTP/1.1\r\nContent-Length: 5\r\n\r\n"), "the body is not waited for");
        Assert.AreEqual(string.Empty, await ExchangeAsync(connection, "abc"), "body bytes start the next request");
        Assert.AreEqual("second\n", await ExchangeAsync(connection, "de /12340002 HTTP/1.1\r\n\r\n"), "the body bytes are read as the next request");
    }

    [TestMethod]
    [DataRow("Authorization: Basic dXNlcjpwYXNz\r\n", "")]
    [DataRow("", "Authorization: in the body")]
    public async Task AuthRequired_WithAuthorizationAnywhereInTheRequest_ReadsTheBody(string headers, string body)
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "posted\n"), Reply("servercmd", "auth_required\n"))));
        string request = $"PUT /1234 HTTP/1.1\r\n{headers}Content-Length: {body.Length + 2}\r\n\r\n{body}";

        Assert.AreEqual(string.Empty, await ExchangeAsync(connection, request), "no reply before the body is complete");
        Assert.AreEqual("posted\n", await ExchangeAsync(connection, "ok"));
    }

    [TestMethod]
    public async Task AuthRequired_DoesNotChangeAChunkedRequest()
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "posted\n"), Reply("servercmd", "auth_required\n"))));

        Assert.AreEqual(string.Empty, await ExchangeAsync(connection, "POST /1234 HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\n"));
        Assert.AreEqual("posted\n", await ExchangeAsync(connection, "3\r\nabc\r\n0\r\n\r\n"));
    }

    [TestMethod]
    [DataRow("Content-Length: 100\r\nexpect: 100-Continue\r\n")]
    [DataRow("Expect: 100-continue\r\nContent-Length: 100\r\n")]
    [DataRow("Expect: 100-continue\r\nContent-Length: 0\r\nContent-Length: 100\r\n")]
    public async Task NoExpect_EndsARequestThatExpectsContinueAtItsHeaders(string headers)
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "refused\n"), Reply("servercmd", "no-expect\n"))));

        Assert.AreEqual("refused\n", await ExchangeAsync(connection, $"PUT /1234 HTTP/1.1\r\n{headers}\r\n"));
    }

    [TestMethod]
    [DataRow("no-expect\n", "Content-Length: 3\r\n")]
    [DataRow("", "Expect: 100-continue\r\nContent-Length: 3\r\n")]
    public async Task ExpectContinue_WithoutNoExpectOrWithoutTheHeader_ReadsTheBody(string serverCommands, string headers)
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "posted\n"), Reply("servercmd", serverCommands))));

        Assert.AreEqual(string.Empty, await ExchangeAsync(connection, $"PUT /1234 HTTP/1.1\r\n{headers}\r\n"));
        Assert.AreEqual("posted\n", await ExchangeAsync(connection, "abc"));
    }

    [TestMethod]
    [DataRow("skip: 3\n", 2)]
    [DataRow("skip: 5\n", 0)]
    [DataRow("skip: 9\nskip: 1\n", 4)]
    [DataRow("skip:-2\n", 7)]
    [DataRow("skip: 99999999999\n", 5)]
    public async Task Skip_ReadsThatManyBytesLessThanContentLength(string serverCommands, int bodyBytesRead)
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "posted\n"), Reply("servercmd", serverCommands))));

        Assert.AreEqual(bodyBytesRead == 0 ? "posted\n" : string.Empty, await ExchangeAsync(connection, "POST /1234 HTTP/1.1\r\nContent-Length: 5\r\n\r\n"));
        for (int read = 1; read <= bodyBytesRead; read++)
        {
            Assert.AreEqual(read == bodyBytesRead ? "posted\n" : string.Empty, await ExchangeAsync(connection, "x"), $"after {read} body bytes");
        }
    }

    [TestMethod]
    [DataRow("Content-Length: 2\r\n")]
    [DataRow("Content-Length: 0\r\nContent-Length: 50\r\n")]
    public async Task Skip_BeyondContentLength_NeverEndsTheRequest(string headers)
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "posted\n"), Reply("servercmd", "skip: 5\n"))));

        Assert.AreEqual(string.Empty, await ExchangeAsync(connection, $"POST /1234 HTTP/1.1\r\n{headers}\r\n" + new string('x', 100)));
    }

    [TestMethod]
    [DataRow("Content-Length: abc\r\n")]
    [DataRow("")]
    public async Task Skip_WithNoValidContentLength_EndsTheRequestAtTheHeaders(string headers)
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "posted\n"), Reply("servercmd", "skip: 5\n"))));

        Assert.AreEqual("posted\n", await ExchangeAsync(connection, $"POST /1234 HTTP/1.1\r\n{headers}\r\n"));
    }

    [TestMethod]
    [DataRow("delay: +5\n")]
    [DataRow("delay:-1\n")]
    [DataRow("delay:\t1000\r\n")]
    public void Delay_IsReportedAsUnsupported(string serverCommands)
    {
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n"), Reply("servercmd", serverCommands)));

        CollectionAssert.AreEqual(new[] { "delay" }, server.UnsupportedServerCommands.ToArray());
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
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n"), Reply("servercmd", serverCommands)));

        Assert.IsEmpty(server.UnsupportedServerCommands);
    }

    [TestMethod]
    public void UnsupportedServerCommands_AreReportedOncePerLineInFileOrder()
    {
        SwsHttpServerConnector server = new(Case(Reply("servercmd", "delay: 1\n\nidle\nauth_required\ndelay: 2\n")));

        CollectionAssert.AreEqual(new[] { "delay", "delay" }, server.UnsupportedServerCommands.ToArray());
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

    [TestMethod]
    public void Constructor_RejectsANullTimeProvider()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SwsHttpServerConnector(Case(Reply("data", "first\n")), null!));
    }

    [TestMethod]
    public async Task WriteDelay_SendsTheReplyInWritesOf20BytesWithTheDelayAfterEach()
    {
        ManualTimeProvider clock = new();
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", FortyFiveBytes), Reply("servercmd", "writedelay: 100\n")), clock));
        await WriteAsync(connection, Get);

        Assert.AreEqual(FortyFiveBytes[..20], await ReadOnceAsync(connection), "the first write is sent at once");
        Task<string> second = ReadOnceAsync(connection);
        clock.Advance(TimeSpan.FromMilliseconds(99));
        Assert.IsFalse(second.IsCompleted, "the second write waits out the first write's delay");
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.AreEqual(FortyFiveBytes[20..40], await second);
        Task<string> third = ReadOnceAsync(connection);
        clock.Advance(TimeSpan.FromMilliseconds(100));
        Assert.AreEqual(FortyFiveBytes[40..], await third);
        Assert.AreEqual(string.Empty, await ReadOnceAsync(connection), "the connection stays open with nothing more to read");
    }

    [TestMethod]
    public async Task WriteDelay_WithAClosingReply_ClosesAfterTheLastWritesDelay()
    {
        ManualTimeProvider clock = new();
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "reply swsclose\n"), Reply("servercmd", "writedelay: 100\n")), clock));
        await WriteAsync(connection, Get);

        Assert.AreEqual("reply swsclose\n", await ReadOnceAsync(connection));
        Task<string> close = ReadOnceAsync(connection);
        clock.Advance(TimeSpan.FromMilliseconds(99));
        Assert.IsFalse(close.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.AreEqual(string.Empty, await close);
    }

    [TestMethod]
    [DataRow("writedelay: 0\n")]
    [DataRow("writedelay: -5\n")]
    [DataRow("writedelay: 100\nwritedelay: 0\n")]
    public async Task WriteDelay_OfZeroOrLess_SendsTheWholeReplyAtOnce(string serverCommands)
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", FortyFiveBytes), Reply("servercmd", serverCommands)), new ManualTimeProvider()));
        await WriteAsync(connection, Get);

        Assert.AreEqual(FortyFiveBytes, await ReadOnceAsync(connection));
    }

    [TestMethod]
    public async Task PostcmdWait_HoldsTheCloseBackThatManySeconds()
    {
        ManualTimeProvider clock = new();
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "reply swsclose\n"), Reply("postcmd", "wait 2\n")), clock));
        await WriteAsync(connection, Get);

        Assert.AreEqual("reply swsclose\n", await ReadOnceAsync(connection));
        Task<string> close = ReadOnceAsync(connection);
        clock.Advance(TimeSpan.FromSeconds(2) - TimeSpan.FromTicks(1));
        Assert.IsFalse(close.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));
        Assert.AreEqual(string.Empty, await close);
    }

    [TestMethod]
    [DataRow("wait 1\n", 1)]
    [DataRow("wait 1\nwait 2\n", 3)]
    [DataRow("  wait\t2\r\n", 2)]
    [DataRow("wait -3\nwait 1\n", 1)]
    [DataRow("sleep 5\nwait\nwait x\nwait10 1\nWAIT 4\nwait 1\n", 1)]
    public async Task PostcmdWait_HoldsTheNextReplyBackThatManySeconds(string postCommands, int seconds)
    {
        ManualTimeProvider clock = new();
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "first\n"), Reply("postcmd", postCommands)), clock));
        await WriteAsync(connection, Get);
        Assert.AreEqual("first\n", await ReadOnceAsync(connection));
        await WriteAsync(connection, Get);

        Task<string> next = ReadOnceAsync(connection);
        clock.Advance(TimeSpan.FromSeconds(seconds) - TimeSpan.FromTicks(1));
        Assert.IsFalse(next.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));
        Assert.AreEqual("first\n", await next);
    }

    [TestMethod]
    public async Task PostcmdWait_DoesNotHoldBackTheNotFoundDocument()
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "first\n"), Reply("postcmd", "wait 5\n"), Reply("servercmd", "writedelay: 100\n")), new ManualTimeProvider()));

        StringAssert.EndsWith(await ExchangeAsync(connection, "GARBAGE\r\n\r\n"), "</BODY></HTML>\n");
    }

    [TestMethod]
    public async Task Idle_AnswersNothingAndAReadWaitsUntilCancelled()
    {
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n"), Reply("servercmd", "idle\n")));
        IConnection connection = await ConnectAsync(server);
        using CancellationTokenSource cancellation = new();
        await WriteAsync(connection, Get);
        await WriteAsync(connection, Get);

        Task<string> read = ReadOnceAsync(connection, cancellation.Token);
        Assert.IsFalse(read.IsCompleted);
        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => read);
        Assert.AreEqual(Get + Get, Text(server.ReceivedBytes), "idle keeps reading requests");
    }

    [TestMethod]
    public async Task Idle_DoesNotApplyToAMalformedRequest()
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "first\n"), Reply("servercmd", "idle\n"))));

        StringAssert.StartsWith(await ExchangeAsync(connection, "GARBAGE\r\n\r\n"), "HTTP/1.1 404 Not Found\r\n");
    }

    [TestMethod]
    public async Task Stream_SendsTheStreamedTextWithoutEndAndReadsNoMore()
    {
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n"), Reply("servercmd", "idle\nstream\n")));
        IConnection connection = await ConnectAsync(server);
        await WriteAsync(connection, Get);

        string streamed = await ReadOnceAsync(connection, 20) + await ReadOnceAsync(connection, 20) + await ReadOnceAsync(connection, 20);
        await WriteAsync(connection, Get);

        Assert.AreEqual(string.Concat(Enumerable.Repeat("a string to stream 01234567890\n", 2))[..60], streamed);
        Assert.AreEqual(Get, Text(server.ReceivedBytes), "sws reads nothing more while it streams");
    }

    [TestMethod]
    public async Task ConnectionMonitor_RecordsADisconnectWhenTheClientCloses()
    {
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n"), Reply("servercmd", "connection-monitor\n")));
        IConnection connection = await ConnectAsync(server);
        await ExchangeAsync(connection, Get);

        await connection.DisposeAsync();
        await connection.DisposeAsync();

        Assert.AreEqual(Get + "[DISCONNECT]\n", Text(server.ReceivedBytes));
    }

    [TestMethod]
    public async Task ConnectionMonitor_RecordsADisconnectWhenTheServerCloses()
    {
        SwsHttpServerConnector server = new(Case(Reply("data", "reply swsclose\n"), Reply("servercmd", "connection-monitor\n")));
        IConnection connection = await ConnectAsync(server);

        await ExchangeAsync(connection, Get);
        Assert.AreEqual(Get + "[DISCONNECT]\n", Text(server.ReceivedBytes));
        await connection.DisposeAsync();

        Assert.AreEqual(Get + "[DISCONNECT]\n", Text(server.ReceivedBytes));
    }

    [TestMethod]
    [DataRow("connection-monitor\n", "")]
    [DataRow("", "GET /1234 HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n")]
    public async Task ConnectionMonitor_RecordsNothingWithoutARequestOrWithoutTheCommand(string serverCommands, string request)
    {
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n"), Reply("servercmd", serverCommands)));
        IConnection connection = await ConnectAsync(server);
        await ExchangeAsync(connection, request);

        await connection.DisposeAsync();

        Assert.AreEqual(request, Text(server.ReceivedBytes));
    }

    [TestMethod]
    public async Task ConnectionMonitor_IsOneFlagForTheServer_SoOnlyTheFirstCloseAfterARequestIsRecorded()
    {
        SwsHttpServerConnector server = new(Case(Reply("data", "first\n"), Reply("servercmd", "connection-monitor\n")));
        IConnection first = await ConnectAsync(server);
        IConnection second = await ConnectAsync(server);
        await ExchangeAsync(first, Get);
        await ExchangeAsync(second, Get);

        await first.DisposeAsync();
        await second.DisposeAsync();

        Assert.AreEqual(Get + Get + "[DISCONNECT]\n", Text(server.ReceivedBytes));
    }

    [TestMethod]
    public async Task Upgrade_AnswersAtTheHeadersThenRecordsTrafficUntilTheClientIsQuietForASecond()
    {
        ManualTimeProvider clock = new();
        SwsHttpServerConnector server = new(Case(Reply("data", "switched\n"), Reply("servercmd", "upgrade\n")), clock);
        IConnection connection = await ConnectAsync(server);
        await WriteAsync(connection, UpgradeRequest + "abc");

        Assert.AreEqual("switched\n", await ReadOnceAsync(connection), "the body is not waited for");
        Task<string> close = ReadOnceAsync(connection);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        await WriteAsync(connection, "def");
        clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.IsFalse(close.IsCompleted, "the write at 500 ms moves the close to 1500 ms");
        clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.AreEqual(string.Empty, await close);
        await WriteAsync(connection, "ghi");

        Assert.AreEqual(UpgradeRequest + "abcdef", Text(server.ReceivedBytes));
    }

    [TestMethod]
    public async Task Upgrade_WriteAfterAQuietSecond_FindsTheConnectionClosed()
    {
        ManualTimeProvider clock = new();
        SwsHttpServerConnector server = new(Case(Reply("data", "switched\n"), Reply("servercmd", "upgrade\nconnection-monitor\n")), clock);
        IConnection connection = await ConnectAsync(server);
        await WriteAsync(connection, UpgradeRequest);
        Assert.AreEqual("switched\n", await ReadOnceAsync(connection));

        clock.Advance(TimeSpan.FromSeconds(1));
        await WriteAsync(connection, "late");

        Assert.AreEqual(string.Empty, await ReadOnceAsync(connection));
        Assert.AreEqual(UpgradeRequest + "[DISCONNECT]\n", Text(server.ReceivedBytes));
    }

    [TestMethod]
    public async Task Upgrade_QuietSecondStartsAfterThePostReplyWait()
    {
        ManualTimeProvider clock = new();
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "switched\n"), Reply("servercmd", "upgrade\n"), Reply("postcmd", "wait 2\n")), clock));
        await WriteAsync(connection, UpgradeRequest);
        Assert.AreEqual("switched\n", await ReadOnceAsync(connection));

        Task<string> close = ReadOnceAsync(connection);
        clock.Advance(TimeSpan.FromSeconds(3) - TimeSpan.FromTicks(1));
        Assert.IsFalse(close.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));

        Assert.AreEqual(string.Empty, await close);
    }

    [TestMethod]
    public async Task Upgrade_WithAClosingReply_ClosesWithoutReadingTraffic()
    {
        SwsHttpServerConnector server = new(Case(Reply("data", "switched swsclose\n"), Reply("servercmd", "upgrade\n")));
        IConnection connection = await ConnectAsync(server);

        Assert.AreEqual("switched swsclose\n", await ExchangeAsync(connection, UpgradeRequest));
        await WriteAsync(connection, "traffic");

        Assert.AreEqual(UpgradeRequest, Text(server.ReceivedBytes));
    }

    [TestMethod]
    public async Task Upgrade_WithStream_Streams()
    {
        SwsHttpServerConnector server = new(Case(Reply("data", "switched\n"), Reply("servercmd", "upgrade\nstream\n")));
        IConnection connection = await ConnectAsync(server);
        await WriteAsync(connection, UpgradeRequest);

        Assert.AreEqual("a string", await ReadOnceAsync(connection, 8));
        await WriteAsync(connection, "traffic");
        Assert.AreEqual(UpgradeRequest, Text(server.ReceivedBytes));
    }

    [TestMethod]
    [DataRow("", "Upgrade: websocket\r\n")]
    [DataRow("upgrade\n", "upgrade: websocket\r\n")]
    public async Task Upgrade_NotAllowedOrNotAskedFor_ReadsTheBody(string serverCommands, string headers)
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "posted\n"), Reply("servercmd", serverCommands))));

        Assert.AreEqual(string.Empty, await ExchangeAsync(connection, $"POST /1234 HTTP/1.1\r\n{headers}Content-Length: 3\r\n\r\n"));
        Assert.AreEqual("posted\n", await ExchangeAsync(connection, "abc"));
    }

    [TestMethod]
    public async Task Upgrade_WithAuthRequiredAndNoAuthorization_IsAnOrdinaryRequestEndedAtItsHeaders()
    {
        IConnection connection = await ConnectAsync(new SwsHttpServerConnector(Case(Reply("data", "denied\n"), Reply("data2", "second\n"), Reply("servercmd", "upgrade\nauth_required\n"))));

        Assert.AreEqual("denied\n", await ExchangeAsync(connection, UpgradeRequest));
        Assert.AreEqual("second\n", await ExchangeAsync(connection, "GET /12340002 HTTP/1.1\r\n\r\n"), "the connection still reads requests");
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
