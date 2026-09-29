namespace Curl.Protocol.Ldap;

/// <summary>
/// One authentication of the logged-on user, from its first token to its last, and then the
/// keys that sign and seal the rest of the session (ADR-0166).
/// </summary>
public interface ILdapLogonAuthentication : IDisposable
{
    /// <summary>Gets a value indicating whether the authentication is complete: no further token is needed.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Produces the next token to send.</summary>
    /// <param name="challenge">The server's last token; empty for the first.</param>
    /// <returns>The token; <see langword="null" /> when the package cannot produce one, or has nothing more to send.</returns>
    byte[]? NextToken(ReadOnlySpan<byte> challenge);

    /// <summary>Signs and seals one message with the complete authentication's keys.</summary>
    /// <param name="message">The message to seal.</param>
    /// <returns>The sealed message: for NTLM its 16-byte signature, then the sealed bytes.</returns>
    byte[] Wrap(ReadOnlySpan<byte> message);

    /// <summary>Checks and unseals one message the server sealed with the complete authentication's keys.</summary>
    /// <param name="wrapped">The sealed message, as <see cref="Wrap(ReadOnlySpan{byte})" /> produces them.</param>
    /// <returns>The message; <see langword="null" /> when its signature does not check or it cannot be unsealed.</returns>
    byte[]? Unwrap(ReadOnlySpan<byte> wrapped);
}
