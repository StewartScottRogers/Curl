namespace Curl.Protocol.Ldap;

/// <summary>What one <see cref="LdapMessageReader.ReadMessageAsync(CancellationToken)" /> found.</summary>
internal enum LdapReadStatus
{
    /// <summary>One whole LDAPMessage was read.</summary>
    Message,

    /// <summary>The server closed the connection before a whole message arrived.</summary>
    Closed,

    /// <summary>The bytes that arrived cannot start an LDAPMessage.</summary>
    Malformed,
}
