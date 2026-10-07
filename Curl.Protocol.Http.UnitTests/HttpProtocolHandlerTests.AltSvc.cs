using System.Net;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Drives <see cref="HttpProtocolHandler" />'s <c>--alt-svc</c> seams through
/// <see cref="ScriptedAltSvcStore" />, never a socket: the alternative a transfer connects to,
/// its <c>Alt-Used</c> header, and each <c>Alt-Svc</c> header handed to the store. Every line is
/// what curl 8.21.0 printed with <c>-k -v --alt-svc cache.txt</c> (BL-623 Notes).
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string AltSvcHttpsUrl = "https://localhost:18499/";

    private static readonly AltSvcAlternative AltSvcAlternative18443 = new("h1", "localhost", 18443);

    [TestMethod]
    public async Task ExecuteAsync_WithAnAltSvcRoute_ConnectsToTheOriginCarryingTheRouteAndSendsAltUsed()
    {
        const string expected = "GET / HTTP/1.1\r\nHost: localhost:18499\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nAlt-Used: localhost:18443\r\n\r\n";
        AltSvcRoute route = new("h1", AltSvcAlternative18443);
        QueueConnector connector = QueueConnector.For(Connection(EmptyOkHead, 65536, expected));
        Diagnostics.Arrange("url, alt-svc route", $"{AltSvcHttpsUrl}, h1 -> localhost:18443");
        Diagnostics.Arrange("expected request", OneLine(expected));

        TransferResult result = await Handler(connector).ExecuteAsync(CookieContext(AltSvcHttpsUrl, new HttpRequestOptions { AltSvcRoute = route }));

        WriteResult(result);
        Diagnostics.Act("connect target", connector.Targets.Single());
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        ConnectTarget target = connector.Targets.Single();
        Assert.AreEqual(("localhost", 18499, true), (target.Host, target.Port, target.UseTls));
        Assert.AreSame(route, target.AltSvcRoute);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithARouteSwitchingToH2_AsksTheConnectorToOfferH2Alone()
    {
        // curl.se 8.18.0 with "h1 127.0.0.1 18736 h2 127.0.0.1 18735 ..." says ALPN: curl offers h2 (BL-733 Notes case 4).
        AltSvcRoute route = new("h1", new AltSvcAlternative("h2", "localhost", 18443));
        QueueConnector connector = QueueConnector.For(Connection(EmptyOkHead, 65536));
        Diagnostics.Arrange("url, alt-svc route", $"{AltSvcHttpsUrl}, h1 -> h2 localhost:18443");

        TransferResult result = await Handler(connector).ExecuteAsync(CookieContext(AltSvcHttpsUrl, new HttpRequestOptions { AltSvcRoute = route }));

        WriteResult(result);
        string offered = string.Join(", ", connector.Targets.Single().ApplicationProtocols ?? []);
        Diagnostics.Act("protocols offered", offered);
        Diagnostics.Assert("protocols offered", "h2", offered);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "h2" }, connector.Targets.Single().ApplicationProtocols!.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_WithARouteOfTheSameVersion_LeavesTheOfferToTheConnector()
    {
        QueueConnector connector = QueueConnector.For(Connection(EmptyOkHead, 65536));
        Diagnostics.Arrange("url, alt-svc route", $"{AltSvcHttpsUrl}, h1 -> h1 localhost:18443");

        TransferResult result = await Handler(connector).ExecuteAsync(CookieContext(AltSvcHttpsUrl, new HttpRequestOptions { AltSvcRoute = new("h1", AltSvcAlternative18443) }));

        WriteResult(result);
        string offered = connector.Targets.Single().ApplicationProtocols is null ? "(the connector's)" : "(set)";
        Diagnostics.Act("protocols offered", offered);
        Diagnostics.Assert("protocols offered", "(the connector's)", offered);
        Assert.IsNull(connector.Targets.Single().ApplicationProtocols);
    }

    /// <summary>
    /// Measured (BL-623 Notes, case 1): with the entry <c>h1 localhost 18499 h1 localhost 18443</c>,
    /// curl 8.21.0 ends <c>* Connection #0 to host localhost:18443 left intact</c>, naming the alternative.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_KeptAliveWithAnAltSvcRoute_ReportsTheAlternativeLeftIntact()
    {
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("url, alt-svc route", $"{AltSvcHttpsUrl}, h1 -> localhost:18443");

        TransferResult result = await Handler(QueueConnector.For(Connection(EmptyOkHead, 65536)))
            .ExecuteAsync(CookieContext(AltSvcHttpsUrl, new HttpRequestOptions { AltSvcRoute = new("h1", AltSvcAlternative18443) }, events));

        WriteResult(result);
        WriteEvents("info lines", events.Info);
        Diagnostics.Assert("last info line", "Connection #0 to host localhost:18443 left intact", events.Info[^1]);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("Connection #0 to host localhost:18443 left intact", events.Info[^1]);
    }

    /// <summary>
    /// Measured (BL-975): <c>-v --connect-to example.invalid:80:127.0.0.1:18499 http://example.invalid/</c>
    /// makes curl 8.21.0 end <c>* Connection #0 to host 127.0.0.1:18499 left intact</c>, naming the
    /// destination the connector reports, ahead of any alt-svc alternative.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_KeptAliveOnAConnectToDestination_ReportsTheDestinationLeftIntact()
    {
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("url, alt-svc route, connect-to", $"{AltSvcHttpsUrl}, h1 -> localhost:18443, 127.0.0.1:18499");
        QueueConnector connector = new(ConnectResult.Connected(Connection(EmptyOkHead, 65536), null, mappedHost: "127.0.0.1", mappedPort: 18499));

        TransferResult result = await Handler(connector)
            .ExecuteAsync(CookieContext(AltSvcHttpsUrl, new HttpRequestOptions { AltSvcRoute = new("h1", AltSvcAlternative18443) }, events));

        WriteResult(result);
        WriteEvents("info lines", events.Info);
        Diagnostics.Assert("last info line", "Connection #0 to host 127.0.0.1:18499 left intact", events.Info[^1]);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("Connection #0 to host 127.0.0.1:18499 left intact", events.Info[^1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_KeptAliveWithoutAnAltSvcRoute_ReportsTheOriginLeftIntact()
    {
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("url, alt-svc route", $"{AltSvcHttpsUrl}, none");

        TransferResult result = await Handler(QueueConnector.For(Connection(EmptyOkHead, 65536)))
            .ExecuteAsync(CookieContext(AltSvcHttpsUrl, new HttpRequestOptions(), events));

        WriteResult(result);
        WriteEvents("info lines", events.Info);
        Diagnostics.Assert("last info line", "Connection #0 to host localhost:18499 left intact", events.Info[^1]);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("Connection #0 to host localhost:18499 left intact", events.Info[^1]);
    }

    /// <summary>
    /// Measured: <c>Alt-Svc: h2=":8443"; ma=60, h1="a.example:1", h3=":443"</c> prints one
    /// <c>Added alt-svc</c> line per alternative, after the status line and before the header line.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_HttpsResponseWithAltSvc_HandsItToTheStoreAndReportsEachAddedBeforeTheHeaderLine()
    {
        const string head = "HTTP/1.1 200 OK\r\nAlt-Svc: h2=\":8443\"; ma=60, h1=\"a.example:1\", h3=\":443\"\r\nContent-Length: 0\r\n\r\n";
        string[] expected =
        [
            "< HTTP/1.1 200 OK\r\n",
            "* Added alt-svc: localhost:8443 over h2",
            "* Added alt-svc: a.example:1 over h1",
            "* Added alt-svc: localhost:443 over h3",
            "< Alt-Svc: h2=\":8443\"; ma=60, h1=\"a.example:1\", h3=\":443\"\r\n",
            "< Content-Length: 0\r\n",
            "< \r\n",
        ];
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedAltSvcStore store = new(new("h2", "localhost", 8443), new("h1", "a.example", 1), new("h3", "localhost", 443));
            RecordingTransferEvents events = new();
            Diagnostics.Arrange("chunk size, response head", $"{chunkSize}, {OneLine(head)}");

            TransferResult result = await Handler(QueueConnector.For(Connection(head, chunkSize)))
                .ExecuteAsync(CookieContext(AltSvcHttpsUrl, new HttpRequestOptions { AltSvcStore = store }, events));

            WriteResult(result);
            WriteEvents("head events", HeadEvents(events));
            Diagnostics.Act("stored alt-svc headers", store.Responses.Count);
            Diagnostics.Assert("head events", OneLine(string.Join(" | ", expected)), OneLine(string.Join(" | ", HeadEvents(events))));
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(expected, HeadEvents(events), $"Chunk size {chunkSize}");
            Assert.AreEqual(
                (CurlUrl.Parse(AltSvcHttpsUrl), "h2=\":8443\"; ma=60, h1=\"a.example:1\", h3=\":443\"", HttpVersion.Version11, CookieTime),
                store.Responses.Single(),
                $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// curl 8.21.0 learns an <c>Alt-Svc</c> header under the version its response came over
    /// (<c>k->httpversion</c>, BL-947): HTTP/2 here.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_Http2ResponseWithAltSvc_TellsTheStoreHttp2()
    {
        HpackEncoder server = new();
        byte[] response = Http2Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "204"), new("alt-svc", "h3=\":443\"; ma=60")]), isEndStream: true, isEndHeaders: true));
        ScriptedAltSvcStore store = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("https://example.com/"),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions { AltSvcStore = store },
        };
        Diagnostics.Arrange("HTTP/2 response", "204 with alt-svc: h3=\":443\"; ma=60");
        Diagnostics.Bytes("response frames", response);

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(new ScriptedConnection(response, 65536), null, applicationProtocol: "h2")))
            .ExecuteAsync(context);

        WriteResult(result);
        Diagnostics.Act("stored", string.Join(" | ", store.Responses.Select(stored => $"{stored.AltSvcHeader} over {stored.ResponseVersion}")));
        Diagnostics.Assert("stored", "h3=\":443\"; ma=60 over 2.0", string.Join(" | ", store.Responses.Select(stored => $"{stored.AltSvcHeader} over {stored.ResponseVersion}")));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(("h3=\":443\"; ma=60", HttpVersion.Version20), (store.Responses.Single().AltSvcHeader, store.Responses.Single().ResponseVersion));
    }

    /// <summary>
    /// curl 8.21.0 learns an <c>Alt-Svc</c> header under the version its response came over
    /// (<c>k->httpversion</c>, BL-947): HTTP/3 here.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_Http3ResponseWithAltSvc_TellsTheStoreHttp3()
    {
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("204", ("alt-svc", "h3=\":443\"; ma=60"))));
        ScriptedAltSvcStore store = new();
        Diagnostics.Arrange("HTTP/3 response", "204 with alt-svc: h3=\":443\"; ma=60");

        TransferResult result = await Handler(QuicConnector(new FakeMultiplexedConnection(stream)))
            .ExecuteAsync(Http3Context("https://example.com/", new MemoryStream(), options: new HttpRequestOptions { AltSvcStore = store }));

        WriteResult(result);
        Diagnostics.Act("stored", string.Join(" | ", store.Responses.Select(stored => $"{stored.AltSvcHeader} over {stored.ResponseVersion}")));
        Diagnostics.Assert("stored", "h3=\":443\"; ma=60 over 3.0", string.Join(" | ", store.Responses.Select(stored => $"{stored.AltSvcHeader} over {stored.ResponseVersion}")));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(("h3=\":443\"; ma=60", HttpVersion.Version30), (store.Responses.Single().AltSvcHeader, store.Responses.Single().ResponseVersion));
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpResponseWithAltSvc_DoesNotCallTheStore()
    {
        const string head = "HTTP/1.1 200 OK\r\nAlt-Svc: h1=\":18443\"; ma=60\r\nContent-Length: 0\r\n\r\n";
        ScriptedAltSvcStore store = new(AltSvcAlternative18443);
        Diagnostics.Arrange("url, response head", $"http://localhost:18443/, {OneLine(head)}");

        TransferResult result = await Handler(QueueConnector.For(Connection(head, 65536)))
            .ExecuteAsync(CookieContext("http://localhost:18443/", new HttpRequestOptions { AltSvcStore = store }));

        WriteResult(result);
        Diagnostics.Act("stored alt-svc headers", store.Responses.Count);
        Diagnostics.Assert("stored alt-svc headers", 0, store.Responses.Count);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsEmpty(store.Responses);
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpsResponseWithAltSvcAndNoStore_Succeeds()
    {
        const string head = "HTTP/1.1 200 OK\r\nAlt-Svc: h1=\":18443\"; ma=60\r\nContent-Length: 0\r\n\r\n";
        Diagnostics.Arrange("url, response head, store", $"{AltSvcHttpsUrl}, {OneLine(head)}, none");

        TransferResult result = await Handler(QueueConnector.For(Connection(head, 65536))).ExecuteAsync(CookieContext(AltSvcHttpsUrl));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }
}
