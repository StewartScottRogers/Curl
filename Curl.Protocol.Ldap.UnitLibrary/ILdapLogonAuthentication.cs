namespace Curl.Protocol.Ldap;

/// <summary>One authentication of the logged-on user, from its first token to its last.</summary>
public interface ILdapLogonAuthentication : IDisposable
{
    /// <summary>Gets a value indicating whether the authentication is complete: no further token is needed.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Produces the next token to send.</summary>
    /// <param name="challenge">The server's last token; empty for the first.</param>
    /// <returns>The token; <see langword="null" /> when the package cannot produce one, or has nothing more to send.</returns>
    byte[]? NextToken(ReadOnlySpan<byte> challenge);
}
