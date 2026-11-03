using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Xmpp;
using static Curl.Conformance.Extended.UnitTests.Xmpp.XmppRun;

namespace Curl.Conformance.Extended.UnitTests.Xmpp;

/// <summary>RFC 6120, 7 and 8, RFC 6121, 5 and XEP-0030: resource binding, a message, a disco query and a stanza error.</summary>
[TestClass]
public sealed class XmppStanzaCases
{
    [TestMethod]
    [SpecRequirement("Xmpp:RFC6120-7.6-1")]
    [SpecRequirement("Xmpp:RFC6120-8.1.3-1")]
    public async Task Bind_ServerAssignsTheResource_RequestCarriesNoResource()
    {
        XmppReferenceServer server = new(new XmppReferenceScript { Resource = "server-chosen" });

        CurlRunResult result = await RunAsync(server, "-d", "hi", Url);

        AssertExit(result, 0, string.Empty);
        Assert.AreEqual("<iq type=\"set\" id=\"bind\"><bind xmlns=\"urn:ietf:params:xml:ns:xmpp-bind\" /></iq>", server.Received[1]);
    }

    [TestMethod]
    [SpecRequirement("Xmpp:RFC6121-5.2-1")]
    [SpecRequirement("Xmpp:RFC6120-8.1.1-1")]
    public async Task Message_FromData_IsAMessageStanzaToThePathJid()
    {
        XmppReferenceServer server = new(new XmppReferenceScript());

        CurlRunResult result = await RunAsync(server, "-d", "hello <world>", Url);

        AssertExit(result, 0, string.Empty);
        Assert.AreEqual("<message to=\"bob@localhost\"><body>hello &lt;world&gt;</body></message>", server.Received[2]);
    }

    [TestMethod]
    [SpecRequirement("Xmpp:XEP0030-3-1")]
    [SpecRequirement("Xmpp:RFC6120-8.2.3-1")]
    public async Task Disco_InfoQuery_IsAnIqGetAndItsResultIsPrinted()
    {
        XmppReferenceServer server = new(new XmppReferenceScript
        {
            Answer = _ => ["<iq type='result' id='disco' from='bob@localhost'><query xmlns='http://jabber.org/protocol/disco#info'><feature var='urn:xmpp:ping'/></query></iq>"],
        });

        CurlRunResult result = await RunAsync(server, Url);

        AssertExit(result, 0, string.Empty, "<query xmlns=\"http://jabber.org/protocol/disco#info\"><feature var=\"urn:xmpp:ping\" /></query>\n");
        Assert.AreEqual("<iq type=\"get\" id=\"disco\" to=\"bob@localhost\"><query xmlns=\"http://jabber.org/protocol/disco#info\" /></iq>", server.Received[2]);
    }

    [TestMethod]
    [SpecRequirement("Xmpp:RFC6120-8.3.1-1")]
    public async Task StanzaError_DiscoAnsweredWithItemNotFound_FailsWithWeirdServerReplyAndTheCondition()
    {
        XmppReferenceServer server = new(new XmppReferenceScript
        {
            Answer = _ => ["<iq type='error' id='disco'><error type='cancel'><item-not-found xmlns='urn:ietf:params:xml:ns:xmpp-stanzas'/></error></iq>"],
        });

        CurlRunResult result = await RunAsync(server, Url);

        AssertExit(result, 8, "curl: (8) XMPP stanza error: item-not-found\n");
    }
}
