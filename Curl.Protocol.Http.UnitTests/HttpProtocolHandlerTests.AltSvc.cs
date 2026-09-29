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

        TransferResult result = await Handler(connector).ExecuteAsync(CookieContext(AltSvcHttpsUrl, new HttpRequestOptions { AltSvcRoute = route }));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        ConnectTarget target = connector.Targets.Single();
        Assert.AreEqual(("localhost", 18499, true), (target.Host, target.Port, target.UseTls));
        Assert.AreSame(route, target.AltSvcRoute);
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

            TransferResult result = await Handler(QueueConnector.For(Connection(head, chunkSize)))
                .ExecuteAsync(CookieContext(AltSvcHttpsUrl, new HttpRequestOptions { AltSvcStore = store }, events));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(expected, HeadEvents(events), $"Chunk size {chunkSize}");
            Assert.AreEqual(
                (CurlUrl.Parse(AltSvcHttpsUrl), "h2=\":8443\"; ma=60, h1=\"a.example:1\", h3=\":443\"", CookieTime),
                store.Responses.Single(),
                $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpResponseWithAltSvc_DoesNotCallTheStore()
    {
        const string head = "HTTP/1.1 200 OK\r\nAlt-Svc: h1=\":18443\"; ma=60\r\nContent-Length: 0\r\n\r\n";
        ScriptedAltSvcStore store = new(AltSvcAlternative18443);

        TransferResult result = await Handler(QueueConnector.For(Connection(head, 65536)))
            .ExecuteAsync(CookieContext("http://localhost:18443/", new HttpRequestOptions { AltSvcStore = store }));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsEmpty(store.Responses);
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpsResponseWithAltSvcAndNoStore_Succeeds()
    {
        const string head = "HTTP/1.1 200 OK\r\nAlt-Svc: h1=\":18443\"; ma=60\r\nContent-Length: 0\r\n\r\n";

        TransferResult result = await Handler(QueueConnector.For(Connection(head, 65536))).ExecuteAsync(CookieContext(AltSvcHttpsUrl));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }
}
