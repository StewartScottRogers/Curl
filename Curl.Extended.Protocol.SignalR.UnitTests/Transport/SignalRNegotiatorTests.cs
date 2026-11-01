using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.SignalR.Transport;

/// <summary>Tests against TransportProtocols.md, "POST [endpoint-base]/negotiate request".</summary>
[TestClass]
public sealed class SignalRNegotiatorTests
{
    private const string Version1Body =
        "{\"connectionToken\":\"tok\",\"connectionId\":\"cid\",\"negotiateVersion\":1,"
        + "\"availableTransports\":[{\"transport\":\"WebSockets\",\"transferFormats\":[\"Text\",\"Binary\"]}]}";

    [TestMethod]
    public async Task NegotiateAsync_Version1_PostsTheExactRequestAndGivesTheWebSocketUrl_TransportProtocolsNegotiate()
    {
        ScriptedConnection connection = Answer("200 OK", Version1Body);
        List<Uri> opened = [];

        NegotiatedWebSocketEndpoint endpoint = await Negotiator(opened, connection)
            .NegotiateAsync(new Uri("http://example.com:5000/chat?room=a"), CancellationToken.None);

        Assert.AreEqual(
            "POST /chat/negotiate?room=a&negotiateVersion=1 HTTP/1.1\r\nHost: example.com:5000\r\nContent-Length: 0\r\n\r\n",
            connection.WrittenText);
        Assert.AreEqual("http://example.com:5000/chat/negotiate?room=a&negotiateVersion=1", opened.Single().AbsoluteUri);
        Assert.AreEqual("ws://example.com:5000/chat?room=a&id=tok", endpoint.WebSocketUrl.AbsoluteUri);
        Assert.IsNull(endpoint.AccessToken);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task NegotiateAsync_Redirect_FollowsItWithTheAccessTokenAsBearer_TransportProtocolsNegotiateRedirect()
    {
        ScriptedConnection first = Answer("200 OK", "{\"url\":\"https://signalr.example.net/client/?hub=chat\",\"accessToken\":\"secret\"}");
        ScriptedConnection second = Answer("200 OK", Version1Body);
        List<Uri> opened = [];

        NegotiatedWebSocketEndpoint endpoint = await Negotiator(opened, first, second)
            .NegotiateAsync(new Uri("https://example.com/chat"), CancellationToken.None);

        Assert.AreEqual(
            "POST /client/negotiate?hub=chat&negotiateVersion=1 HTTP/1.1\r\nHost: signalr.example.net\r\n"
            + "Authorization: Bearer secret\r\nContent-Length: 0\r\n\r\n",
            second.WrittenText);
        Assert.AreEqual("https://signalr.example.net/client/negotiate?hub=chat&negotiateVersion=1", opened[1].AbsoluteUri);
        Assert.AreEqual("wss://signalr.example.net/client/?hub=chat&id=tok", endpoint.WebSocketUrl.AbsoluteUri);
        Assert.AreEqual("secret", endpoint.AccessToken);
    }

    [TestMethod]
    public async Task NegotiateAsync_SecondRedirect_Throws_TransportProtocolsNegotiateRedirect()
    {
        string redirect = "{\"url\":\"http://other.example/hub\",\"accessToken\":\"t\"}";

        SignalRTransportException failure = await NegotiateFailureAsync(Answer("200 OK", redirect), Answer("200 OK", redirect));

        Assert.AreEqual("The negotiate response redirected a second time.", failure.Message);
    }

    [TestMethod]
    public async Task NegotiateAsync_RelativeRedirectUrl_Throws_TransportProtocolsNegotiateRedirect()
    {
        SignalRTransportException failure = await NegotiateFailureAsync(Answer("200 OK", "{\"url\":\"/elsewhere\"}"));

        StringAssert.Contains(failure.Message, "not absolute: /elsewhere");
    }

    [TestMethod]
    [DataRow("file:///elsewhere")]
    [DataRow("ftp://other.example/hub")]
    public async Task NegotiateAsync_RedirectUrlNotHttp_Throws_TransportProtocolsNegotiateRedirect(string redirectUrl)
    {
        SignalRTransportException failure = await NegotiateFailureAsync(Answer("200 OK", $"{{\"url\":\"{redirectUrl}\"}}"));

        StringAssert.Contains(failure.Message, "not absolute: " + redirectUrl);
    }

    [TestMethod]
    [DataRow("404 Not Found", 404)]
    [DataRow("302 Found", 302)]
    [DataRow("199 Odd", 199)]
    public async Task NegotiateAsync_StatusNot2xx_Throws_TransportProtocolsNegotiate(string status, int statusCode)
    {
        SignalRTransportException failure = await NegotiateFailureAsync(Answer(status, "not json"));

        Assert.AreEqual($"The negotiate request failed with HTTP status {statusCode}.", failure.Message);
    }

    [TestMethod]
    public async Task NegotiateAsync_ErrorResponse_ThrowsWithTheServersError_TransportProtocolsNegotiate()
    {
        SignalRTransportException failure = await NegotiateFailureAsync(Answer("200 OK", "{\"error\":\"Hub is full\"}"));

        Assert.AreEqual("The server refused the negotiate request: Hub is full", failure.Message);
    }

    [TestMethod]
    public async Task NegotiateAsync_NoWebSocketsTransport_Throws_TransportProtocolsNegotiate()
    {
        string body = "{\"connectionId\":\"cid\",\"availableTransports\":[{\"transport\":\"LongPolling\",\"transferFormats\":[\"Text\"]}]}";

        SignalRTransportException failure = await NegotiateFailureAsync(Answer("200 OK", body));

        Assert.AreEqual("The negotiate response offers no WebSockets transport.", failure.Message);
    }

    [TestMethod]
    public async Task NegotiateAsync_MalformedBody_ThrowsAWireFormatError_TransportProtocolsNegotiate()
    {
        SignalRNegotiator negotiator = Negotiator([], Answer("200 OK", "{\"connectionId\":"));

        await Assert.ThrowsExactlyAsync<SignalRWireFormatException>(
            () => negotiator.NegotiateAsync(new Uri("http://example.com/chat"), CancellationToken.None));
    }

    private static ScriptedConnection Answer(string status, string body) =>
        ScriptedConnection.FromText($"HTTP/1.1 {status}\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\n\r\n{body}");

    private static SignalRNegotiator Negotiator(List<Uri> opened, params ScriptedConnection[] connections)
    {
        Queue<ScriptedConnection> pending = new(connections);
        return new SignalRNegotiator((url, _) =>
        {
            opened.Add(url);
            return ValueTask.FromResult<IConnection>(pending.Dequeue());
        });
    }

    private static Task<SignalRTransportException> NegotiateFailureAsync(params ScriptedConnection[] connections) =>
        Assert.ThrowsExactlyAsync<SignalRTransportException>(
            () => Negotiator([], connections).NegotiateAsync(new Uri("http://example.com/chat"), CancellationToken.None));
}
