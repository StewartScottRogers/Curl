using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.WebTransport.Server;

namespace Curl.Conformance.Extended.UnitTests.WebTransport.Cases;

/// <summary>Sections 3.2 and 3.3 and RFC 9220 section 3: the extended CONNECT that opens a session and its response.</summary>
[TestClass]
public sealed class WebTransportConnectTests
{
    [TestMethod]
    [SpecRequirement("WebTransport:WTH3-3.2-1")]
    [SpecRequirement("WebTransport:RFC9220-3-2")]
    public Task Connect_FieldSection_IsAnExtendedConnectForWebTransportH3OverHttps() =>
        WebTransportRun.CaseAsync(["WebTransport:WTH3-3.2-1", "WebTransport:RFC9220-3-2"], async () =>
        {
            using WebTransportReferenceServer server = WebTransportRun.Server(new WebTransportServerScript { EchoBidirectionalStreams = true });

            CurlRunResult result = await WebTransportRun.RunAsync(server, "webtransport://localhost:443/chat?room=1", "-d", "hi");

            WebTransportRun.AssertSucceeded(result, "hi");
            Assert.AreEqual("CONNECT", WebTransportRun.ConnectField(server, ":method"));
            Assert.AreEqual("webtransport-h3", WebTransportRun.ConnectField(server, ":protocol"));
            Assert.AreEqual("https", WebTransportRun.ConnectField(server, ":scheme"));
            Assert.AreEqual("localhost:443", WebTransportRun.ConnectField(server, ":authority"));
            Assert.AreEqual("/chat?room=1", WebTransportRun.ConnectField(server, ":path"));
            Assert.IsFalse(server.ConnectFields!.Any(field => !field.Name.StartsWith(':') && field.Name.Any(char.IsUpper)));
        });

    [TestMethod]
    [SpecRequirement("WebTransport:WTH3-3.3-1")]
    [SpecRequirement("WebTransport:WTH3-4-1")]
    public Task Connect_Status404_FailsTheSessionWithExit22AndOpensNoStream() =>
        WebTransportRun.CaseAsync(["WebTransport:WTH3-3.3-1", "WebTransport:WTH3-4-1"], async () =>
        {
            using WebTransportReferenceServer server = WebTransportRun.Server(new WebTransportServerScript { ConnectStatus = "404", EchoBidirectionalStreams = true });

            CurlRunResult result = await WebTransportRun.RunAsync(server, WebTransportRun.Url, "-d", "hi");

            WebTransportRun.AssertFailed(result, 22, "The server refused the WebTransport session: HTTP status 404");
            Assert.AreEqual("CONNECT", WebTransportRun.ConnectField(server, ":method"));
            Assert.IsEmpty(server.BidirectionalStreams);
            Assert.IsEmpty(server.Datagrams);
        });
}
