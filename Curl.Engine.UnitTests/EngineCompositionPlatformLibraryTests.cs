using Curl.Original.Protocol.Ldap;
using Curl.Ssh.Negotiation;

using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins the libraries <see cref="EngineComposition.CreateProtocolHandlers" /> gives the LDAP and SSH
/// handlers by platform, as the platform's curl is built: WinLDAP and the Schannel build's SSH
/// algorithms on Windows, OpenLDAP and the OpenSSL build's elsewhere (BL-1356).
/// </summary>
[TestClass]
public sealed class EngineCompositionPlatformLibraryTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(true, LdapDialect.WinLdap)]
    [DataRow(false, LdapDialect.OpenLdap)]
    public void LdapDialectFor_EachPlatform_IsItsCurlsLdapLibrary(bool runsOnWindows, LdapDialect expected)
    {
        Diagnostics.Arrange("runsOnWindows", runsOnWindows);
        LdapDialect dialect = EngineComposition.LdapDialectFor(runsOnWindows);
        Diagnostics.Act("LDAP dialect", dialect);

        Diagnostics.Assert("LDAP dialect", expected, dialect);
        Assert.AreEqual(expected, dialect);
    }

    [TestMethod]
    public void SshAlgorithmPreferencesFor_Windows_IsTheSchannelBuilds()
    {
        Diagnostics.Arrange("runsOnWindows", true);
        SshAlgorithmPreferences preferences = EngineComposition.SshAlgorithmPreferencesFor(runsOnWindows: true);
        bool isReference = ReferenceEquals(SshAlgorithmPreferences.WindowsReference, preferences);
        Diagnostics.Act("is the Windows reference", isReference);

        Diagnostics.Assert("is the Windows reference", true, isReference);
        Assert.AreSame(SshAlgorithmPreferences.WindowsReference, preferences);
    }

    [TestMethod]
    public void SshAlgorithmPreferencesFor_OffWindows_IsTheOpenSslBuilds()
    {
        Diagnostics.Arrange("runsOnWindows", false);
        SshAlgorithmPreferences preferences = EngineComposition.SshAlgorithmPreferencesFor(runsOnWindows: false);
        bool isReference = ReferenceEquals(SshAlgorithmPreferences.OpenSslReference, preferences);
        Diagnostics.Act("is the OpenSSL reference", isReference);

        Diagnostics.Assert("is the OpenSSL reference", true, isReference);
        Assert.AreSame(SshAlgorithmPreferences.OpenSslReference, preferences);
    }
}
