using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.SignalR.Transport;

/// <summary>BL-5377: a negotiate redirect to another origin carries none of the user's Cookie fields unless trusted.</summary>
[TestClass]
public sealed class SignalRNegotiatorRedirectCookieTests
{
    private const string Version1Body =
        "{\"connectionToken\":\"tok\",\"connectionId\":\"cid\",\"negotiateVersion\":1,"
        + "\"availableTransports\":[{\"transport\":\"WebSockets\",\"transferFormats\":[\"Text\",\"Binary\"]}]}";

    [TestMethod]
    [DataRow("sid=BCOOKIE", "http://127.0.0.1:43010/hub", "http://127.0.0.1:43011/evilhub")]
    [DataRow("HDRCOOKIE=1", "http://127.0.0.1:43010/hub", "http://127.0.0.1:43011/evilhub")]
    [DataRow("HDRCOOKIE=1", "https://localhost:43012/hub", "http://localhost:43011/evilhub")]
    public async Task NegotiateAsync_RedirectToAnotherOrigin_SendsNoCookieAndKeepsOtherFields(string cookie, string startUrl, string redirectUrl)
    {
        (string second, NegotiatedWebSocketEndpoint endpoint) = await RedirectAsync(startUrl, redirectUrl, cookie, false);

        Assert.DoesNotContain("Cookie", second);
        Assert.DoesNotContain(cookie, second);
        Assert.DoesNotContain("Authorization", second);
        Assert.IsFalse(endpoint.RequestFields!.Any(field => field.Key == "Cookie"));
        Assert.IsTrue(endpoint.RequestFields!.Any(field => field.Key == "User-Agent"));
    }

    [TestMethod]
    public async Task NegotiateAsync_LocationTrusted_SendsTheCookieAcrossOrigins()
    {
        (string second, NegotiatedWebSocketEndpoint endpoint) = await RedirectAsync("http://127.0.0.1:43010/hub", "http://127.0.0.1:43011/evilhub", "sid=BCOOKIE", true);

        Assert.Contains("Cookie: sid=BCOOKIE\r\n", second);
        Assert.IsTrue(endpoint.RequestFields!.Any(field => field.Key == "Cookie"));
    }

    [TestMethod]
    public async Task NegotiateAsync_SameOriginRedirect_KeepsTheCookie()
    {
        (string second, NegotiatedWebSocketEndpoint endpoint) = await RedirectAsync("http://127.0.0.1:43010/hub", "http://127.0.0.1:43010/hub2", "sid=BCOOKIE", false);

        Assert.Contains("Cookie: sid=BCOOKIE\r\n", second);
        Assert.IsTrue(endpoint.RequestFields!.Any(field => field.Key == "Cookie"));
    }

    [TestMethod]
    public async Task ExecuteAsync_CrossOriginRedirect_UpgradesWithoutTheCookie()
    {
        string upgraded = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: "
            + SignalRWebSocketUpgrade.AcceptFor(Convert.ToBase64String(new byte[16])) + "\r\n\r\n";
        ScriptedConnection webSocket = new([.. System.Text.Encoding.Latin1.GetBytes(upgraded), 0x81, 3, (byte)'{', (byte)'}', 0x1e]);
        Curl.Extended.Protocol.SignalR.Client.ScriptedConnector connector = new(
            ConnectResult.Connected(Answer("{\"url\":\"http://other.example/evilhub\"}")),
            ConnectResult.Connected(Answer(Version1Body)),
            ConnectResult.Connected(webSocket));

        _ = await new Curl.Extended.Protocol.SignalR.Client.SignalRProtocolHandler(connector, new RepeatingRandomSource(0)).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("signalr://example.com/chat"),
            Output = new MemoryStream(),
            Http = new Curl.Protocol.Abstractions.HttpRequestOptions { CookieStrings = "sid=BCOOKIE", Headers = ["Cookie: HDR=1"] },
        });

        Assert.DoesNotContain("Cookie", webSocket.WrittenText);
    }

    private static async Task<(string Second, NegotiatedWebSocketEndpoint Endpoint)> RedirectAsync(
        string startUrl, string redirectUrl, string cookie, bool trusted)
    {
        ScriptedConnection first = Answer($"{{\"url\":\"{redirectUrl}\"}}");
        ScriptedConnection second = Answer(Version1Body);
        Queue<ScriptedConnection> pending = new([first, second]);
        SignalRNegotiator negotiator = new(
            (_, _) => ValueTask.FromResult<IConnection>(pending.Dequeue()),
            [new("Authorization", "Basic abc"), new("User-Agent", "curl/8.22.0"), new("Cookie", cookie)],
            trusted);

        NegotiatedWebSocketEndpoint endpoint = await negotiator.NegotiateAsync(new Uri(startUrl), CancellationToken.None);
        return (second.WrittenText, endpoint);
    }

    private static ScriptedConnection Answer(string body) =>
        ScriptedConnection.FromText($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\n\r\n{body}");
}
