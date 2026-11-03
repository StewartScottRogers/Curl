namespace Curl.Conformance.Extended.Xmpp;

/// <summary>Whether <see cref="XmppReferenceServer" /> offers STARTTLS in its first stream features (RFC 6120, 5.3.1).</summary>
public enum XmppStartTls
{
    /// <summary>No <c>&lt;starttls/&gt;</c> feature.</summary>
    None,

    /// <summary><c>&lt;starttls/&gt;</c> without <c>&lt;required/&gt;</c>.</summary>
    Optional,

    /// <summary><c>&lt;starttls&gt;&lt;required/&gt;&lt;/starttls&gt;</c>.</summary>
    Required,
}
