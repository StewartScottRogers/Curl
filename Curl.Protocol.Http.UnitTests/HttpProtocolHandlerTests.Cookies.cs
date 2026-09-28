using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Drives <see cref="HttpProtocolHandler" />'s cookies through <see cref="ScriptedCookieStore" />,
/// never a socket. Every request is what curl 8.21.0 sent a loopback server with a cookie
/// jar; the commands are in the BL-182 Notes. Each exchange is replayed with 1-byte reads and
/// with one read.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string CookieUrl = "http://127.0.0.1:18082/";

    private const string EmptyOkHead = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    private static readonly DateTimeOffset CookieTime = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Measured: <c>curl -b jar.txt -H "Cookie: c=d"</c> sends the jar's <c>Cookie</c> after
    /// <c>Accept</c> and the custom one after it.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_CookieStoreGivesAValue_SendsItBeforeTheCustomHeaders()
    {
        const string expected = "GET / HTTP/1.1\r\nHost: 127.0.0.1:18082\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nCookie: j=k\r\nCookie: c=d\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(EmptyOkHead, chunkSize, expected);
            ScriptedCookieStore store = new("j=k");
            HttpRequestOptions options = new() { Headers = ["Cookie: c=d"] };

            TransferResult result = await CookieHandler(QueueConnector.For(connection), store)
                .ExecuteAsync(CookieContext(CookieUrl, options));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual((CurlUrl.Parse(CookieUrl), false, CookieTime), store.Requests.Single(), $"Chunk size {chunkSize}");
            Assert.IsEmpty(store.Responses, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_CookieStoreGivesNull_SendsNoCookieHeader()
    {
        const string expected = "GET / HTTP/1.1\r\nHost: 127.0.0.1:18082\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";
        ScriptedConnection connection = Connection(EmptyOkHead, 65536, expected);
        ScriptedCookieStore store = new();

        TransferResult result = await CookieHandler(QueueConnector.For(connection), store).ExecuteAsync(CookieContext(CookieUrl));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(1, store.Requests);
    }

    [TestMethod]
    public async Task ExecuteAsync_Https_AsksForSecureCookies()
    {
        ScriptedCookieStore store = new();

        await CookieHandler(QueueConnector.For(Connection(EmptyOkHead, 65536)), store)
            .ExecuteAsync(CookieContext("https://example.com/p"));

        Assert.AreEqual((CurlUrl.Parse("https://example.com/p"), true, CookieTime), store.Requests.Single());
    }

    /// <summary>
    /// A 3xx's <c>Set-Cookie</c> values reach the store, every one and in received order, with
    /// the request URL and the transfer's time, whether or not <c>-L</c> follows it.
    /// </summary>
    [TestMethod]
    [DataRow(false, DisplayName = "Without -L")]
    [DataRow(true, DisplayName = "With -L")]
    public async Task ExecuteAsync_RedirectSetsCookies_StoresEveryOneInOrder(bool followRedirects)
    {
        const string head = "HTTP/1.1 302 Found\r\nSet-Cookie: b=2; Path=/\r\nLocation: /next\r\nset-cookie: a=1\r\nContent-Length: 0\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedCookieStore store = new();
            HttpRequestOptions options = new() { FollowRedirects = followRedirects };

            TransferResult result = await CookieHandler(QueueConnector.For(Connection(head, chunkSize)), store)
                .ExecuteAsync(CookieContext(CookieUrl, options));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(new[] { "b=2; Path=/", "a=1" }, store.Responses.Select(call => call.SetCookieHeader).ToArray(), $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(new[] { 0, 1 }, store.Responses.Select(call => call.StoredFromResponse).ToArray(), $"Chunk size {chunkSize}");
            foreach ((CurlUrl uri, _, _, DateTimeOffset now, ITransferEvents events) in store.Responses)
            {
                Assert.AreEqual(CurlUrl.Parse(CookieUrl), uri, $"Chunk size {chunkSize}");
                Assert.AreEqual(CookieTime, now, $"Chunk size {chunkSize}");
                Assert.AreSame(NoTransferEvents.Instance, events, $"Chunk size {chunkSize}");
            }
        }
    }

    /// <summary>
    /// Measured (BL-468 Notes): curl 8.21.0 prints each cookie's <c>-v</c> line immediately
    /// before the <c>Set-Cookie</c> header line it came from, not after the head.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ResponseSetsCookies_ReportsEachCookieLineBeforeItsHeaderLine()
    {
        const string head = "HTTP/1.1 200 OK\r\nSet-Cookie: a=1\r\nX-Mid: y\r\nSet-Cookie: b=2; Path=/\r\nSet-Cookie: a=3\r\nContent-Length: 2\r\n\r\nok";
        string[] expected =
        [
            "< HTTP/1.1 200 OK\r\n",
            "* Added a=1",
            "< Set-Cookie: a=1\r\n",
            "< X-Mid: y\r\n",
            "* Added b=2; Path=/",
            "< Set-Cookie: b=2; Path=/\r\n",
            "* Added a=3",
            "< Set-Cookie: a=3\r\n",
            "< Content-Length: 2\r\n",
            "< \r\n",
        ];
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedCookieStore store = new() { ReportedLine = header => "Added " + header };
            RecordingTransferEvents events = new();

            TransferResult result = await CookieHandler(QueueConnector.For(Connection(head, chunkSize)), store)
                .ExecuteAsync(CookieContext(CookieUrl, events: events));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(expected, HeadEvents(events), $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, store.Responses.Select(call => call.StoredFromResponse).ToArray(), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured (BL-468 Notes): curl 8.21.0 unfolds a folded <c>Set-Cookie</c> header before
    /// storing its cookie, and prints the cookie line before the header.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_SetCookieIsFolded_StoresTheWholeValueAndReportsItsLineFirst()
    {
        const string head = "HTTP/1.1 200 OK\r\nSet-Cookie: a=1;\r\n Path=/x\r\nContent-Length: 0\r\n\r\n";
        string[] expected =
        [
            "< HTTP/1.1 200 OK\r\n",
            "* Added a=1; Path=/x",
            "< Set-Cookie: a=1;\r\n",
            "<  Path=/x\r\n",
            "< Content-Length: 0\r\n",
            "< \r\n",
        ];
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedCookieStore store = new() { ReportedLine = header => "Added " + header };
            RecordingTransferEvents events = new();

            await CookieHandler(QueueConnector.For(Connection(head, chunkSize)), store)
                .ExecuteAsync(CookieContext(CookieUrl, events: events));

            CollectionAssert.AreEqual(expected, HeadEvents(events), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured with <c>-s -v -c -</c> (BL-475 Notes): curl 8.21.0 stops reading the head at a
    /// header it refuses, so it reports and stores the cookies before it, and reports neither
    /// the refused header, nor a <c>Set-Cookie</c> header or any other line after it, nor the
    /// head's empty line.
    /// </summary>
    [TestMethod]
    [DataRow("Content-Length: x\r\n", false, CurlExitCode.WeirdServerReply, DisplayName = "Invalid Content-Length")]
    [DataRow("Transfer-Encoding: bogus\r\n", false, CurlExitCode.BadContentEncoding, DisplayName = "Unsolicited Transfer-Encoding")]
    [DataRow("Content-Encoding: gzip,gzip,gzip,gzip,gzip,gzip\r\n", true, CurlExitCode.BadContentEncoding, DisplayName = "Six content codings, with --compressed")]
    public async Task ExecuteAsync_HeaderRefused_ReportsAndStoresNothingFromItOn(string refusedHeader, bool compressed, CurlExitCode exitCode)
    {
        string head = "HTTP/1.1 200 OK\r\nSet-Cookie: b=2\r\nX-Before: 1\r\n" + refusedHeader + "Set-Cookie: a=1\r\nX-After: 1\r\n\r\n";
        string[] expected =
        [
            "< HTTP/1.1 200 OK\r\n",
            "* Added b=2",
            "< Set-Cookie: b=2\r\n",
            "< X-Before: 1\r\n",
        ];
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedCookieStore store = new() { ReportedLine = header => "Added " + header };
            RecordingTransferEvents events = new();

            TransferResult result = await CookieHandler(QueueConnector.For(Connection(head, chunkSize)), store)
                .ExecuteAsync(CookieContext(CookieUrl, new HttpRequestOptions { Compressed = compressed }, events));

            Assert.AreEqual(exitCode, result.ExitCode, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(expected, HeadEvents(events), $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(new[] { "b=2" }, store.Responses.Select(call => call.SetCookieHeader).ToArray(), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured (BL-468 Notes): curl 8.21.0 stores a cookie a 1xx head sets, reporting it before
    /// its header line, and counts it toward the request's cookies.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_InformationalHeadSetsACookie_StoresItBeforeItsHeaderLine()
    {
        const string head = "HTTP/1.1 100 Continue\r\nSet-Cookie: z=9\r\n\r\nHTTP/1.1 200 OK\r\nSet-Cookie: y=8\r\nContent-Length: 0\r\n\r\n";
        string[] expected =
        [
            "< HTTP/1.1 100 Continue\r\n",
            "* Added z=9",
            "< Set-Cookie: z=9\r\n",
            "< \r\n",
            "< HTTP/1.1 200 OK\r\n",
            "* Added y=8",
            "< Set-Cookie: y=8\r\n",
            "< Content-Length: 0\r\n",
            "< \r\n",
        ];
        ScriptedCookieStore store = new() { ReportedLine = header => "Added " + header };
        RecordingTransferEvents events = new();

        await CookieHandler(QueueConnector.For(Connection(head, 65536)), store)
            .ExecuteAsync(CookieContext(CookieUrl, events: events));

        CollectionAssert.AreEqual(expected, HeadEvents(events));
        CollectionAssert.AreEqual(new[] { 0, 1 }, store.Responses.Select(call => call.StoredFromResponse).ToArray());
    }

    /// <summary>
    /// A head that fails before a header is whole still reports that header's lines, but hands
    /// no cookie to the store.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_HeadFailsAfterASetCookieLine_ReportsTheLineAndStoresNothing()
    {
        const string head = "HTTP/1.1 200 OK\r\nSet-Cookie: a=1\r\nno colon\r\n\r\n";
        ScriptedCookieStore store = new() { ReportedLine = header => "Added " + header };
        RecordingTransferEvents events = new();

        TransferResult result = await CookieHandler(QueueConnector.For(Connection(head, 65536)), store)
            .ExecuteAsync(CookieContext(CookieUrl, events: events));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "< HTTP/1.1 200 OK\r\n", "< Set-Cookie: a=1\r\n" }, HeadEvents(events));
        Assert.IsEmpty(store.Responses);
    }

    /// <summary>
    /// The store is handed the transfer's events, so it can report each cookie it adds or
    /// drops as curl's <c>-v</c> lines (BL-367).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ResponseSetsCookies_HandsTheStoreTheTransferEvents()
    {
        const string head = "HTTP/1.1 200 OK\r\nSet-Cookie: n2=v; Path=/\r\nContent-Length: 0\r\n\r\n";
        ScriptedCookieStore store = new();
        RecordingTransferEvents events = new();
        TransferContext context = new() { Url = CurlUrl.Parse(CookieUrl), Output = new MemoryStream(), TimeProvider = new FakeTimeProvider(CookieTime), Events = events };

        TransferResult result = await CookieHandler(QueueConnector.For(Connection(head, 65536)), store).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        (_, string setCookie, _, _, ITransferEvents handed) = store.Responses.Single();
        Assert.AreEqual("n2=v; Path=/", setCookie);
        Assert.AreSame(events, handed);
    }

    [TestMethod]
    public async Task ExecuteAsync_SetCookieWithoutAStore_IsIgnored()
    {
        const string head = "HTTP/1.1 200 OK\r\nSet-Cookie: a=1\r\nContent-Length: 0\r\n\r\n";

        TransferResult result = await Handler(QueueConnector.For(Connection(head, 65536, RootRequest)))
            .ExecuteAsync(Context("http://example.com/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    /// <summary>
    /// Measured: <c>curl -b jar.txt --anyauth -u u:p</c> against a 401 that sets
    /// <c>s=1</c> and <c>t=2</c> stores both and asks the jar again for the retry, which
    /// sends <c>Cookie: t=2; s=1; j=k</c> (the order is the store's).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ChallengeSetsCookies_StoresThemAndAsksAgainForTheRetry()
    {
        const string challengeHead = "HTTP/1.1 401 Unauthorized\r\nSet-Cookie: s=1\r\nSet-Cookie: t=2\r\nWWW-Authenticate: Basic realm=\"x\"\r\nContent-Length: 0\r\n\r\n";
        const string first = "GET / HTTP/1.1\r\nHost: 127.0.0.1:18083\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nCookie: j=k\r\n\r\n";
        const string retry = "GET / HTTP/1.1\r\nHost: 127.0.0.1:18083\r\nAuthorization: Basic dTpw\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nCookie: t=2; s=1; j=k\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, challengeHead, EmptyOkHead);
            ScriptedCookieStore store = new("j=k", "t=2; s=1; j=k");
            HttpProtocolHandler handler = new(QueueConnector.For(connection), new ScriptedAuthenticator(null, "Basic dTpw"), store);

            TransferResult result = await handler.ExecuteAsync(CookieContext("http://127.0.0.1:18083/"));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(first + retry, connection.Written, $"Chunk size {chunkSize}");
            Assert.HasCount(2, store.Requests, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(new[] { "s=1", "t=2" }, store.Responses.Select(call => call.SetCookieHeader).ToArray(), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public void Constructor_KeepsTheCookieStore()
    {
        ScriptedCookieStore store = new();

        HttpProtocolHandler handler = new(new QueueConnector(), new SilentAuthenticator(), store);

        Assert.AreSame(store, handler.CookieStore);
    }

    private static HttpProtocolHandler CookieHandler(QueueConnector connector, ICookieStore store) =>
        new(connector, new SilentAuthenticator(), store);

    private static TransferContext CookieContext(string url, HttpRequestOptions? options = null, ITransferEvents? events = null) =>
        new()
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Http = options,
            TimeProvider = new FakeTimeProvider(CookieTime),
            Events = events ?? NoTransferEvents.Instance,
        };

    /// <summary>
    /// The response head lines and the <c>-v</c> lines among them: every event from the first
    /// response header on, up to the first body byte.
    /// </summary>
    private static string[] HeadEvents(RecordingTransferEvents events) =>
        [.. events.Events
            .SkipWhile(line => !line.StartsWith("< ", StringComparison.Ordinal))
            .TakeWhile(line => !line.StartsWith("{ ", StringComparison.Ordinal))
            .Where(line => line.StartsWith("< ", StringComparison.Ordinal) || line.StartsWith("* Added", StringComparison.Ordinal))];
}
