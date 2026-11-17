using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.SignalR.Transport;

/// <summary>BL-5311: a negotiate redirect to another origin carries none of the user's Authorization unless trusted.</summary>
[TestClass]
public sealed class SignalRNegotiatorRedirectCredentialTests
{
    private const string Version1Body =
        "{\"connectionToken\":\"tok\",\"connectionId\":\"cid\",\"negotiateVersion\":1,"
        + "\"availableTransports\":[{\"transport\":\"WebSockets\",\"transferFormats\":[\"Text\",\"Binary\"]}]}";

    [TestMethod]
    [DataRow("Basic YWxpY2U6U0VDUkVUUFc=", "http://127.0.0.1:37011/evilhub", "http://127.0.0.1:37010/hub")]
    [DataRow("Bearer SECRETBEARER", "http://127.0.0.1:37010/hub", "http://localhost:37010/hub")]
    [DataRow("SECRETHDR", "http://other.example/hub", "http://127.0.0.1:37010/hub")]
    [DataRow("Basic YWxpY2U6U0VDUkVUUFc=", "https://127.0.0.1:37010/hub", "http://127.0.0.1:37010/hub")]
    public async Task NegotiateAsync_RedirectToAnotherOrigin_SendsNoAuthorization(string authorization, string redirectUrl, string startUrl)
    {
        (string second, NegotiatedWebSocketEndpoint endpoint) = await RedirectAsync(startUrl, redirectUrl, null, authorization, false);

        Assert.DoesNotContain("Authorization", second);
        Assert.DoesNotContain(authorization, second);
        Assert.IsTrue(endpoint.RequestFields!.Any(field => field.Key == "User-Agent"));
        Assert.IsFalse(endpoint.RequestFields!.Any(field => field.Key == "Authorization"));
    }

    [TestMethod]
    public async Task NegotiateAsync_RedirectFromHttpsToHttp_SendsNoAuthorization()
    {
        (string second, _) = await RedirectAsync("https://example.com/hub", "http://example.com/hub", null, "Basic abc", false);

        Assert.DoesNotContain("Authorization", second);
    }

    [TestMethod]
    public async Task NegotiateAsync_SameOriginRedirectWithoutToken_KeepsTheCredentials()
    {
        (string second, _) = await RedirectAsync("http://example.com/hub", "http://EXAMPLE.com:80/other", null, "Basic abc", false);

        Assert.Contains("Authorization: Basic abc\r\n", second);
    }

    [TestMethod]
    public async Task NegotiateAsync_RedirectToAnotherOriginWithToken_SendsTheBearerToken()
    {
        (string second, _) = await RedirectAsync("http://127.0.0.1:37010/hub", "http://127.0.0.1:37011/evilhub", "SRVTOKEN", "Basic abc", false);

        Assert.Contains("Authorization: Bearer SRVTOKEN\r\n", second);
        Assert.DoesNotContain("Basic abc", second);
    }

    [TestMethod]
    public async Task NegotiateAsync_LocationTrusted_KeepsTheCredentialsAcrossOrigins()
    {
        (string second, NegotiatedWebSocketEndpoint endpoint) = await RedirectAsync("https://a.example/hub", "http://b.example/hub", null, "Basic abc", true);

        Assert.Contains("Authorization: Basic abc\r\n", second);
        Assert.IsTrue(endpoint.RequestFields!.Any(field => field.Key == "Authorization"));
    }

    private static async Task<(string Second, NegotiatedWebSocketEndpoint Endpoint)> RedirectAsync(
        string startUrl, string redirectUrl, string? accessToken, string authorization, bool trusted)
    {
        string token = accessToken is null ? string.Empty : $",\"accessToken\":\"{accessToken}\"";
        ScriptedConnection first = Answer($"{{\"url\":\"{redirectUrl}\"{token}}}");
        ScriptedConnection second = Answer(Version1Body);
        Queue<ScriptedConnection> pending = new([first, second]);
        SignalRNegotiator negotiator = new(
            (_, _) => ValueTask.FromResult<IConnection>(pending.Dequeue()),
            [new("Authorization", authorization), new("User-Agent", "curl/8.22.0")],
            trusted);

        NegotiatedWebSocketEndpoint endpoint = await negotiator.NegotiateAsync(new Uri(startUrl), CancellationToken.None);
        return (second.WrittenText, endpoint);
    }

    private static ScriptedConnection Answer(string body) =>
        ScriptedConnection.FromText($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\n\r\n{body}");

    [TestMethod]
    public async Task ExecuteAsync_LocationTrustedHttpOptions_UpgradesWithTheCredentials()
    {
        string upgraded = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: "
            + SignalRWebSocketUpgrade.AcceptFor(Convert.ToBase64String(new byte[16])) + "\r\n\r\n";
        ScriptedConnection webSocket = new([.. System.Text.Encoding.Latin1.GetBytes(upgraded), 0x81, 3, (byte)'{', (byte)'}', 0x1e]);
        Curl.Extended.Protocol.SignalR.Client.ScriptedConnector connector = new(ConnectResult.Connected(Answer(Version1Body)), ConnectResult.Connected(webSocket));

        _ = await new Curl.Extended.Protocol.SignalR.Client.SignalRProtocolHandler(connector, new RepeatingRandomSource(0)).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("signalr://example.com/chat"),
            Output = new MemoryStream(),
            Http = new Curl.Protocol.Abstractions.HttpRequestOptions { BearerToken = "TT", SendsCredentialsToOtherOrigins = true },
        });

        Assert.Contains("Authorization: Bearer TT\r\n", webSocket.WrittenText);
    }
}
