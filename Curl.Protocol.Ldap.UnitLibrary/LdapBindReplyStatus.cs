namespace Curl.Protocol.Ldap;

/// <summary>What the server answered to one BindRequest.</summary>
internal enum LdapBindReplyStatus
{
    /// <summary>A BindResponse to the request arrived; its result code says how the bind went.</summary>
    Answered,

    /// <summary>The server closed the connection before a whole reply arrived.</summary>
    Closed,

    /// <summary>A reply arrived that is not a BindResponse to the request.</summary>
    Malformed,
}
