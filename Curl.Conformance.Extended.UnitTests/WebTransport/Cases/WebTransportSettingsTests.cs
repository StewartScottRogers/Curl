using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.WebTransport.Server;

namespace Curl.Conformance.Extended.UnitTests.WebTransport.Cases;

/// <summary>Section 3.1, RFC 9220 section 3 and RFC 9297 section 2.1.1: the SETTINGS each side sends before a session.</summary>
[TestClass]
public sealed class WebTransportSettingsTests
{
    private const string NotOffered = "The server does not offer WebTransport over HTTP/3";

    [TestMethod]
    [SpecRequirement("WebTransport:WTH3-3.1-2")]
    [SpecRequirement("WebTransport:WTH3-3.1-3")]
    public Task Session_ClientSettings_CarryWebTransportEnabledAndH3Datagram() =>
        WebTransportRun.CaseAsync(["WebTransport:WTH3-3.1-2", "WebTransport:WTH3-3.1-3"], async () =>
        {
            using WebTransportReferenceServer server = WebTransportRun.Server(new WebTransportServerScript { EchoBidirectionalStreams = true });

            CurlRunResult result = await WebTransportRun.RunAsync(server, WebTransportRun.Url, "-d", "hi");

            WebTransportRun.AssertSucceeded(result, "hi");
            Assert.AreEqual(1L, WebTransportRun.ClientSetting(server, WebTransportCodepoints.EnabledSetting));
            Assert.AreEqual(1L, WebTransportRun.ClientSetting(server, 0x33));
        });

    [TestMethod]
    [SpecRequirement("WebTransport:WTH3-3.1-1")]
    public Task Session_ServerLeavesOutWebTransportEnabled_SendsNoConnectAndExits8() =>
        WebTransportRun.CaseAsync(["WebTransport:WTH3-3.1-1"], () => AssertRefusedAsync(new WebTransportServerScript { WebTransportEnabled = null }));

    [TestMethod]
    [SpecRequirement("WebTransport:RFC9220-3-1")]
    public Task Session_ServerLeavesOutEnableConnectProtocol_SendsNoConnectAndExits8() =>
        WebTransportRun.CaseAsync(["WebTransport:RFC9220-3-1"], () => AssertRefusedAsync(new WebTransportServerScript { SendEnableConnectProtocol = false }));

    [TestMethod]
    [SpecRequirement("WebTransport:RFC9297-2.1.1-1")]
    public Task Session_ServerLeavesOutH3Datagram_SendsNoConnectAndExits8() =>
        WebTransportRun.CaseAsync(["WebTransport:RFC9297-2.1.1-1"], () => AssertRefusedAsync(new WebTransportServerScript { SendH3Datagram = false }));

    private static async Task AssertRefusedAsync(WebTransportServerScript script)
    {
        using WebTransportReferenceServer server = WebTransportRun.Server(script with { EchoBidirectionalStreams = true, EchoDatagrams = true });

        CurlRunResult result = await WebTransportRun.RunAsync(server, WebTransportRun.Url, "-d", "hi");

        WebTransportRun.AssertFailed(result, 8, NotOffered);
        Assert.IsNull(server.ConnectFields);
        Assert.IsEmpty(server.BidirectionalStreams);
        Assert.IsEmpty(server.Datagrams);
    }
}
