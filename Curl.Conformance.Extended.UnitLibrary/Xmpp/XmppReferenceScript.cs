using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

namespace Curl.Conformance.Extended.Xmpp;

/// <summary>What <see cref="XmppReferenceServer" /> offers and answers in one case.</summary>
public sealed record XmppReferenceScript
{
    /// <summary>Gets whether STARTTLS is offered; any but <see cref="XmppStartTls.None" /> needs <see cref="Certificate" />.</summary>
    public XmppStartTls StartTls { get; init; }

    /// <summary>Gets the certificate the server secures the stream with after <c>&lt;proceed/&gt;</c>.</summary>
    public X509Certificate2? Certificate { get; init; }

    /// <summary>Gets the SASL mechanisms offered, in the server's order of preference (RFC 6120, 6.4.1).</summary>
    public IReadOnlyList<string> Mechanisms { get; init; } = ["SCRAM-SHA-256", "SCRAM-SHA-1", "PLAIN", "ANONYMOUS"];

    /// <summary>Gets the password the server's SCRAM side derives its keys from.</summary>
    public string Password { get; init; } = "secret";

    /// <summary>Gets the defined condition the server answers <c>&lt;auth/&gt;</c> with in <c>&lt;failure/&gt;</c>, or <see langword="null" /> to accept.</summary>
    public string? FailureCondition { get; init; }

    /// <summary>Gets a value indicating whether the SCRAM server signature sent with <c>&lt;success/&gt;</c> is altered by one bit.</summary>
    public bool ForgeServerSignature { get; init; }

    /// <summary>Gets the resource the server assigns when it binds the client's JID (RFC 6120, 7.6).</summary>
    public string Resource { get; init; } = "curl-1";

    /// <summary>Gets the chunks the server writes, one write each, after curl's stanza that follows resource binding.</summary>
    public Func<XElement, IReadOnlyList<string>> Answer { get; init; } = _ => [];
}
