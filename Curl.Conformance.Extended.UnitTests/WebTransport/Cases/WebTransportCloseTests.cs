using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.WebTransport.Server;

namespace Curl.Conformance.Extended.UnitTests.WebTransport.Cases;

/// <summary>Section 6 and RFC 9297 section 3.2: ending a session with WT_CLOSE_SESSION, from either side.</summary>
[TestClass]
public sealed class WebTransportCloseTests
{
    [TestMethod]
    [SpecRequirement("WebTransport:WTH3-6-1")]
    [SpecRequirement("WebTransport:RFC9297-3.2-1")]
    [SpecRequirement("WebTransport:WTH3-6-3")]
    public Task ClientClose_AfterTheExchange_SendsWtCloseSessionCode0ThenFin() =>
        WebTransportRun.CaseAsync(["WebTransport:WTH3-6-1", "WebTransport:RFC9297-3.2-1", "WebTransport:WTH3-6-3"], async () =>
        {
            using WebTransportReferenceServer server = WebTransportRun.Server(new WebTransportServerScript { EchoBidirectionalStreams = true });

            CurlRunResult result = await WebTransportRun.RunAsync(server, WebTransportRun.Url, "-d", "bye");

            WebTransportRun.AssertSucceeded(result, "bye");
            WebTransportRecordedCapsule capsule = server.Capsules.Single();
            Assert.AreEqual(WebTransportCodepoints.CloseSessionCapsule, capsule.Type);
            CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0 }, capsule.Value);
            Assert.IsTrue(server.IsConnectStreamFinished);
        });

    [TestMethod]
    [SpecRequirement("WebTransport:WTH3-6-2")]
    public Task ServerClose_WithCodeAndMessage_EndsTheTransferWithExit56AndTheClientSendsOnlyFin() =>
        WebTransportRun.CaseAsync(["WebTransport:WTH3-6-2"], async () =>
        {
            using WebTransportReferenceServer server = WebTransportRun.Server(new WebTransportServerScript { CloseSession = new WebTransportCloseSession(7, "going away") });

            CurlRunResult result = await WebTransportRun.RunAsync(server, WebTransportRun.Url, "-m", "3", "-d", "hi");

            WebTransportRun.AssertFailed(result, 56, "The server closed the WebTransport session: code 7, going away");
            Assert.IsEmpty(server.Capsules);
            Assert.IsTrue(server.IsConnectStreamFinished);
        });
}
