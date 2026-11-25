namespace Curl.Original.Protocol.Ldap;

/// <summary>What the server answered to one BindRequest or to the StartTLS ExtendedRequest.</summary>
internal enum LdapBindReplyStatus
{
    /// <summary>A response to the request arrived; its result code says how the request went.</summary>
    Answered,

    /// <summary>The server closed the connection before a whole reply arrived.</summary>
    Closed,

    /// <summary>A reply arrived that is not a response to the request.</summary>
    Malformed,

    /// <summary>StartTLS was answered with a SearchResultEntry or SearchResultReference, which carries no result code.</summary>
    SearchResult,
}
