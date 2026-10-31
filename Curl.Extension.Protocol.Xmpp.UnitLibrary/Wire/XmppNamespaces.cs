using System.Xml.Linq;

namespace Curl.Extension.Protocol.Xmpp.Wire;

/// <summary>The XML namespaces the client speaks (RFC 6120, 4.8.1 and 5, 6, 7; RFC 6121; XEP-0030).</summary>
internal static class XmppNamespaces
{
    /// <summary>The stream namespace, of <c>&lt;stream:stream&gt;</c>, <c>&lt;stream:features&gt;</c> and <c>&lt;stream:error&gt;</c>.</summary>
    internal static readonly XNamespace Stream = "http://etherx.jabber.org/streams";

    /// <summary>The content namespace of a client stream's stanzas.</summary>
    internal static readonly XNamespace Client = "jabber:client";

    /// <summary>STARTTLS negotiation (RFC 6120, 5).</summary>
    internal static readonly XNamespace Tls = "urn:ietf:params:xml:ns:xmpp-tls";

    /// <summary>SASL negotiation (RFC 6120, 6).</summary>
    internal static readonly XNamespace Sasl = "urn:ietf:params:xml:ns:xmpp-sasl";

    /// <summary>Resource binding (RFC 6120, 7).</summary>
    internal static readonly XNamespace Bind = "urn:ietf:params:xml:ns:xmpp-bind";

    /// <summary>Stanza error conditions (RFC 6120, 8.3.3).</summary>
    internal static readonly XNamespace Stanzas = "urn:ietf:params:xml:ns:xmpp-stanzas";

    /// <summary>Stream error conditions (RFC 6120, 4.9.3).</summary>
    internal static readonly XNamespace Streams = "urn:ietf:params:xml:ns:xmpp-streams";

    /// <summary>Service discovery's information query (XEP-0030, 3).</summary>
    internal static readonly XNamespace DiscoInfo = "http://jabber.org/protocol/disco#info";

    /// <summary>Service discovery's items query (XEP-0030, 4).</summary>
    internal static readonly XNamespace DiscoItems = "http://jabber.org/protocol/disco#items";
}
