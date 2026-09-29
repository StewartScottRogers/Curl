namespace Curl.Protocol.Ldap;

/// <summary>What the server sent while a search was outstanding.</summary>
internal enum LdapSearchReplyKind
{
    /// <summary>The SearchResultDone that ends the search; its result code says how it went.</summary>
    Done,

    /// <summary>A SearchResultEntry for the search.</summary>
    Entry,

    /// <summary>A reply to the search that is neither an entry nor the SearchResultDone, such as a SearchResultReference.</summary>
    OtherResponse,

    /// <summary>A whole LDAPMessage for another messageID.</summary>
    OtherMessage,

    /// <summary>The server closed the connection, or sent bytes that are not an LDAPMessage, before the SearchResultDone.</summary>
    Lost,
}
