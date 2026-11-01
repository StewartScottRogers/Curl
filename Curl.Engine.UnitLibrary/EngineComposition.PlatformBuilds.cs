using Curl.Original.Protocol.Ldap;
using Curl.Ssh.Negotiation;

namespace Curl.Engine;

internal static partial class EngineComposition
{
    /// <summary>
    /// Gives the LDAP library the platform's curl is built with: WinLDAP on Windows, OpenLDAP elsewhere.
    /// </summary>
    /// <param name="runsOnWindows">Whether the run is on Windows.</param>
    /// <returns>The dialect.</returns>
    internal static LdapDialect LdapDialectFor(bool runsOnWindows) =>
        runsOnWindows ? LdapDialect.WinLdap : LdapDialect.OpenLdap;

    /// <summary>
    /// Gives the SSH algorithms the platform's curl offers: the Schannel build's on Windows, the
    /// OpenSSL build's elsewhere.
    /// </summary>
    /// <param name="runsOnWindows">Whether the run is on Windows.</param>
    /// <returns>The algorithm preferences.</returns>
    internal static SshAlgorithmPreferences SshAlgorithmPreferencesFor(bool runsOnWindows) =>
        runsOnWindows ? SshAlgorithmPreferences.WindowsReference : SshAlgorithmPreferences.OpenSslReference;
}
