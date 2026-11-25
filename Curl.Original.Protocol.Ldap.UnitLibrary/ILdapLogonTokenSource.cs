using System.Net;
using System.Net.Security;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Starts the logged-on user's authentication for the Windows build's bind without <c>-u</c>
/// (ADR-0166): the seam through which the security package reaches
/// <see cref="LdapProtocolHandler" />, so tests need no domain and no logged-on user.
/// </summary>
public interface ILdapLogonTokenSource
{
    /// <summary>Starts one authentication with the logged-on user's credentials.</summary>
    /// <param name="package">The security package to authenticate with.</param>
    /// <param name="targetName">The service principal name, <c>ldap/</c> and the URL's host.</param>
    /// <param name="protection">
    /// The protection the authentication asks the server for: <see cref="ProtectionLevel.EncryptAndSign" />
    /// on plaintext <c>ldap</c>, whose session it seals after the bind, and
    /// <see cref="ProtectionLevel.None" /> on <c>ldaps</c>, where TLS already protects it.
    /// </param>
    /// <returns>The authentication, which produces the tokens the binds carry.</returns>
    ILdapLogonAuthentication Start(LdapLogonPackage package, string targetName, ProtectionLevel protection);

    /// <summary>
    /// Gets a source that authenticates with <paramref name="explicitCredential" />, the <c>-u</c> credentials,
    /// instead of the logged-on user; a source with no such notion returns itself.
    /// </summary>
    /// <param name="explicitCredential">The <c>-u</c> credentials.</param>
    /// <returns>The source to bind with.</returns>
    ILdapLogonTokenSource ForCredential(NetworkCredential explicitCredential) => this;
}
