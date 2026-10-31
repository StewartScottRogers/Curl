namespace Curl.Extension.Protocol.SignalR.Negotiate;

[TestClass]
public sealed class SignalRUrlsTests
{
    [TestMethod]
    [DataRow("https://example.com/chat", "https://example.com/chat/negotiate?negotiateVersion=1")]
    [DataRow("http://example.com:5000/chat/?room=a", "http://example.com:5000/chat/negotiate?room=a&negotiateVersion=1")]
    public void NegotiateUrl_AppendsNegotiateAndTheVersion_NegotiateRequest(string endpoint, string expected)
    {
        Assert.AreEqual(expected, SignalRUrls.NegotiateUrl(new Uri(endpoint)).AbsoluteUri);
    }

    [TestMethod]
    public void WebSocketUrl_Version1_UsesTheConnectionTokenAsId_WebSockets()
    {
        NegotiateResponse response = Response(1, connectionId: "the-id", connectionToken: "a b+c");

        Assert.AreEqual("wss://example.com/chat?id=a%20b%2Bc", SignalRUrls.WebSocketUrl(new Uri("https://example.com/chat"), response).AbsoluteUri);
    }

    [TestMethod]
    public void WebSocketUrl_Version0_UsesTheConnectionIdAsId_WebSockets()
    {
        NegotiateResponse response = Response(0, connectionId: "the-id", connectionToken: null);

        Assert.AreEqual("ws://example.com:5000/chat?room=a&id=the-id", SignalRUrls.WebSocketUrl(new Uri("http://example.com:5000/chat?room=a"), response).AbsoluteUri);
    }

    [TestMethod]
    public void WebSocketUrl_WssEndpoint_StaysSecure_WebSockets()
    {
        Assert.AreEqual("wss://example.com/chat?id=t", SignalRUrls.WebSocketUrl(new Uri("wss://example.com/chat"), Response(1, null, "t")).AbsoluteUri);
    }

    [TestMethod]
    public void WebSocketUrl_NoIdForTheVersion_Throws_WebSockets()
    {
        Assert.ThrowsExactly<SignalRWireFormatException>(() => SignalRUrls.WebSocketUrl(new Uri("https://example.com/chat"), Response(1, "the-id", null)));
    }

    private static NegotiateResponse Response(int version, string? connectionId, string? connectionToken) =>
        new(version, connectionId, connectionToken, [], null, null, null);
}
