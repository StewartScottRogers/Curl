using System.Security.Cryptography.X509Certificates;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.SignalR;
using Curl.Console;
using static Curl.Conformance.Extended.UnitTests.SignalR.SignalRRun;

namespace Curl.Conformance.Extended.UnitTests.SignalR;

/// <summary>TransportProtocols.md cases: the negotiate exchange and the WebSocket transport's URL.</summary>
[TestClass]
public sealed class SignalRTransportCases
{
    private static SignalRHubStep[] HandshakeOnly => [SignalRHubStep.Receive, SignalRHubStep.Records("{}"), SignalRHubStep.Records("""{"type":7}""")];

    [TestMethod]
    [SpecRequirement("SignalR:TP-Negotiate-1")]
    [SpecRequirement("SignalR:TP-Negotiate-2")]
    [SpecRequirement("SignalR:TP-WebSockets-1")]
    public async Task NegotiateVersion1_PostsNegotiateThenConnectsWithTheConnectionToken()
    {
        SignalRReferenceHub hub = new([Version1("token/1")], HandshakeOnly);

        CurlRunResult result = await RunAsync(hub, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.HasCount(2, hub.Requests);
        Assert.AreEqual("POST", hub.Requests[0].Method);
        Assert.AreEqual("/hub/negotiate?negotiateVersion=1", hub.Requests[0].Target);
        Assert.AreEqual("0", hub.Requests[0].Header("Content-Length"));
        Assert.AreEqual("GET", hub.Requests[1].Method);
        Assert.AreEqual("/hub?id=token%2F1", hub.Requests[1].Target);
        Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        Assert.AreEqual(string.Empty, Text(result.StandardError));
    }

    [TestMethod]
    [SpecRequirement("SignalR:TP-Negotiate-3")]
    public async Task NegotiateVersion0_ConnectsWithTheConnectionId()
    {
        SignalRReferenceHub hub = new([Version0], HandshakeOnly);

        CurlRunResult result = await RunAsync(hub, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("/hub?id=conn-0", hub.Requests[1].Target);
    }

    [TestMethod]
    [SpecRequirement("SignalR:TP-Negotiate-4")]
    [SpecRequirement("SignalR:TP-Negotiate-5")]
    public async Task NegotiateRedirect_NegotiatesAgainAtTheUrlWithTheAccessTokenAndConnectsThere()
    {
        SignalRReferenceHub hub = new(
            ["""{"url":"http://elsewhere:5000/other?tenant=a","accessToken":"secret-token"}""", Version1()],
            HandshakeOnly);

        CurlRunResult result = await RunAsync(hub, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.HasCount(3, hub.Requests);
        Assert.IsNull(hub.Requests[0].Header("Authorization"));
        Assert.AreEqual("/other/negotiate?tenant=a&negotiateVersion=1", hub.Requests[1].Target);
        Assert.AreEqual("elsewhere:5000", hub.Requests[1].Header("Host"));
        Assert.AreEqual("Bearer secret-token", hub.Requests[1].Header("Authorization"));
        Assert.AreEqual("/other?tenant=a&id=token-1", hub.Requests[2].Target);
        Assert.AreEqual("Bearer secret-token", hub.Requests[2].Header("Authorization"));
    }

    [TestMethod]
    [SpecRequirement("SignalR:TP-Negotiate-6")]
    public async Task NegotiateError_FailsWithExit8AndOpensNoWebSocket()
    {
        SignalRReferenceHub hub = new(["""{"error":"Negotiation is not allowed."}"""], HandshakeOnly);

        CurlRunResult result = await RunAsync(hub, Url);

        Assert.AreEqual(8, result.ExitCode);
        Assert.HasCount(1, hub.Requests);
        Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        Assert.AreEqual("curl: (8) SignalR: The server refused the negotiate request: Negotiation is not allowed.\n", Error(result));
    }

    [TestMethod]
    [SpecRequirement("SignalR:TP-Negotiate-7")]
    public async Task NegotiateOffersNoWebSockets_FailsWithExit8()
    {
        SignalRReferenceHub hub = new(
            ["""{"negotiateVersion":1,"connectionId":"c","connectionToken":"t","availableTransports":[{"transport":"LongPolling","transferFormats":["Text"]}]}"""],
            HandshakeOnly);

        CurlRunResult result = await RunAsync(hub, Url);

        Assert.AreEqual(8, result.ExitCode);
        Assert.HasCount(1, hub.Requests);
        Assert.AreEqual("curl: (8) SignalR: The negotiate response offers no WebSockets transport.\n", Error(result));
    }

    [TestMethod]
    [SpecRequirement("SignalR:TP-WebSockets-2")]
    public async Task SecureScheme_NegotiatesAndConnectsOverTls()
    {
        SignalRReferenceHub hub = new([Version1()], HandshakeOnly);
        using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");

        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(
            InProcessCurl.RunAsync, [ReferenceRoute.Tls(Port, hub, certificate)], ["-sS", "-k", "-m", "30", "signalrs://localhost:5000/hub"]);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.HasCount(2, result.Exchanges);
        Assert.IsTrue(result.Exchanges.All(exchange => exchange.Transport == ReferenceTransport.Tls));
        Assert.AreEqual("/hub?id=token-1", hub.Requests[1].Target);
    }
}
