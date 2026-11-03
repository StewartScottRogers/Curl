using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Xmpp;
using static Curl.Conformance.Extended.UnitTests.Xmpp.XmppRun;

namespace Curl.Conformance.Extended.UnitTests.Xmpp;

/// <summary>RFC 6120, 4 and 5: the stream header, STARTTLS, stream restarts, closing, stream errors and framing across reads.</summary>
[TestClass]
public sealed class XmppStreamCases
{
    private const string Disco = "<iq type='result' id='disco'><query xmlns='http://jabber.org/protocol/disco#info'><identity category='client' type='pc'/></query></iq>";

    [TestMethod]
    [SpecRequirement("Xmpp:RFC6120-4.2-1")]
    [SpecRequirement("Xmpp:RFC6120-4.7.2-1")]
    [SpecRequirement("Xmpp:RFC6120-4.7.3-1")]
    [SpecRequirement("Xmpp:RFC6120-4.7.5-1")]
    [SpecRequirement("Xmpp:RFC6120-4.8.2-1")]
    [SpecRequirement("Xmpp:RFC6120-4.4-1")]
    [SpecRequirement("Xmpp:RFC6120-6.4.6-1")]
    [SpecRequirement("Xmpp:RFC6120-11.1-1")]
    public async Task StreamHeader_PlainStream_OpensWithToAndVersionRestartsAfterSaslAndCloses()
    {
        XmppReferenceServer server = new(new XmppReferenceScript());

        CurlRunResult result = await RunAsync(server, "-d", "hi", Url);

        AssertExit(result, 0, string.Empty);
        string sent = ClientText(result);
        Assert.IsTrue(sent.StartsWith(StreamHeader, StringComparison.Ordinal), sent);
        Assert.AreEqual(2, sent.Split(StreamHeader).Length - 1, sent);
        Assert.IsTrue(sent.EndsWith("</stream:stream>", StringComparison.Ordinal), sent);
        Assert.IsFalse(sent.Contains("<!DOCTYPE", StringComparison.Ordinal), sent);
    }

    [TestMethod]
    [SpecRequirement("Xmpp:RFC6120-5.3.1-1")]
    [SpecRequirement("Xmpp:RFC6120-5.4.2.1-1")]
    [SpecRequirement("Xmpp:RFC6120-5.4.3.3-1")]
    public async Task StartTls_OfferedAsRequired_IsNegotiatedAndTheStreamRestartsBehindTls()
    {
        XmppReferenceServer server = new(WithStartTls(XmppStartTls.Required));

        CurlRunResult result = await RunAsync(server, "-u", "alice:secret", "-d", "hi", Url);

        AssertExit(result, 0, string.Empty);
        Assert.IsTrue(ClientText(result).StartsWith(StreamHeader + "<starttls xmlns=\"urn:ietf:params:xml:ns:xmpp-tls\" />", StringComparison.Ordinal));
        Assert.AreEqual("<starttls xmlns=\"urn:ietf:params:xml:ns:xmpp-tls\" />", server.Received[0]);
        Assert.IsTrue(server.Received[1].StartsWith("<auth mechanism=\"SCRAM-SHA-256\" xmlns=\"urn:ietf:params:xml:ns:xmpp-sasl\">", StringComparison.Ordinal), server.Received[1]);
        Assert.IsTrue(server.ProofVerified);
    }

    [TestMethod]
    [SpecRequirement("Xmpp:RFC6120-5.3.1-2")]
    public async Task StartTls_OfferedAsOptional_IsStillNegotiated()
    {
        XmppReferenceServer server = new(WithStartTls(XmppStartTls.Optional));

        CurlRunResult result = await RunAsync(server, "-d", "hi", Url);

        AssertExit(result, 0, string.Empty);
        Assert.AreEqual("<starttls xmlns=\"urn:ietf:params:xml:ns:xmpp-tls\" />", server.Received[0]);
        Assert.AreEqual("<message to=\"bob@localhost\"><body>hi</body></message>", server.Received[^1]);
    }

    [TestMethod]
    [SpecRequirement("Xmpp:XEP0368-1")]
    public async Task Xmpps_DirectTls_OpensTheStreamBehindTlsWithoutStartTls()
    {
        XmppReferenceServer server = new(new XmppReferenceScript());

        CurlRunResult result = await RunTlsAsync(server, "-d", "hi", "xmpps://localhost/bob@localhost");

        AssertExit(result, 0, string.Empty);
        Assert.IsTrue(ClientText(result).StartsWith(StreamHeader + "<auth ", StringComparison.Ordinal));
    }

    [TestMethod]
    [SpecRequirement("Xmpp:RFC6120-4.9.1.1-1")]
    public async Task StreamError_ServerSendsConflict_FailsWithWeirdServerReplyAndTheCondition()
    {
        XmppReferenceServer server = new(new XmppReferenceScript
        {
            Answer = _ => ["<stream:error><conflict xmlns='urn:ietf:params:xml:ns:xmpp-streams'/></stream:error></stream:stream>"],
        });

        CurlRunResult result = await RunAsync(server, Url);

        AssertExit(result, 8, "curl: (8) XMPP stream error: conflict\n");
    }

    [TestMethod]
    [SpecRequirement("Xmpp:RFC6120-4.1-1")]
    public async Task SplitStanza_DiscoAnswerAcrossThreeReads_IsReadWhole()
    {
        XmppReferenceServer server = new(new XmppReferenceScript { Answer = _ => [Disco[..9], Disco[9..60], Disco[60..]] });

        CurlRunResult result = await RunAsync(server, Url);

        AssertExit(result, 0, string.Empty, "<query xmlns=\"http://jabber.org/protocol/disco#info\"><identity category=\"client\" type=\"pc\" /></query>\n");
    }
}
