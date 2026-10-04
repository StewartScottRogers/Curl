using Curl.Protocol.Ldap;
using Curl.Protocol.Ssh.Negotiation;

namespace Curl.Console;

/// <summary>
/// Pins the libraries <see cref="CurlComposition.CreateProtocolHandlers" /> gives the LDAP and SSH
/// handlers by platform, as the platform's curl is built: WinLDAP and the Schannel build's SSH
/// algorithms on Windows, OpenLDAP and the OpenSSL build's elsewhere (BL-1356).
/// </summary>
[TestClass]
public sealed class CurlCompositionPlatformLibraryTests
{
    [TestMethod]
    [DataRow(true, LdapDialect.WinLdap)]
    [DataRow(false, LdapDialect.OpenLdap)]
    public void LdapDialectFor_EachPlatform_IsItsCurlsLdapLibrary(bool runsOnWindows, LdapDialect expected)
    {
        Assert.AreEqual(expected, CurlComposition.LdapDialectFor(runsOnWindows));
    }

    [TestMethod]
    public void SshAlgorithmPreferencesFor_Windows_IsTheSchannelBuilds()
    {
        Assert.AreSame(SshAlgorithmPreferences.WindowsReference, CurlComposition.SshAlgorithmPreferencesFor(runsOnWindows: true));
    }

    [TestMethod]
    public void SshAlgorithmPreferencesFor_OffWindows_IsTheOpenSslBuilds()
    {
        Assert.AreSame(SshAlgorithmPreferences.OpenSslReference, CurlComposition.SshAlgorithmPreferencesFor(runsOnWindows: false));
    }
}