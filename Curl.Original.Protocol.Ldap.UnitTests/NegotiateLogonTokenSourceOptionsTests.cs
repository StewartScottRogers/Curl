using System.Net;
using System.Net.Security;
using Curl.Testing;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Pins the options <see cref="NegotiateLogonTokenSource" /> authenticates with. They are read
/// without starting a security package, so these tests hold on every platform and whether or
/// not the machine is in a domain.
/// </summary>
[TestClass]
public sealed class NegotiateLogonTokenSourceOptionsTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(LdapLogonPackage.Ntlm, "NTLM")]
    [DataRow(LdapLogonPackage.Negotiate, "Negotiate")]
    public void ClientOptions_LogonPackage_NamesThatSecurityPackage(LdapLogonPackage package, string expectedName)
    {
        Diagnostics.Arrange("package", package);

        NegotiateAuthenticationClientOptions options = new NegotiateLogonTokenSource().ClientOptions(package, "ldap/server", ProtectionLevel.EncryptAndSign);

        Diagnostics.Act("package name", options.Package);
        Diagnostics.Assert("package name", expectedName, options.Package);
        Assert.AreEqual(expectedName, options.Package);
    }

    [TestMethod]
    public void ClientOptions_NoExplicitCredential_UsesTheLoggedOnUsersWithTheTargetAndProtection()
    {
        Diagnostics.Arrange("target name", "ldap/server");

        NegotiateAuthenticationClientOptions options = new NegotiateLogonTokenSource().ClientOptions(LdapLogonPackage.Negotiate, "ldap/server", ProtectionLevel.None);

        Diagnostics.Act("target name", options.TargetName);
        Diagnostics.Act("protection", options.RequiredProtectionLevel);
        Diagnostics.Assert("target name", "ldap/server", options.TargetName);
        Diagnostics.Assert("protection", ProtectionLevel.None, options.RequiredProtectionLevel);
        Assert.AreSame(CredentialCache.DefaultNetworkCredentials, options.Credential);
        Assert.AreEqual("ldap/server", options.TargetName);
        Assert.AreEqual(ProtectionLevel.None, options.RequiredProtectionLevel);
    }

    [TestMethod]
    public void ForCredential_ExplicitCredential_IsTheCredentialAuthenticatedWith()
    {
        var credential = new NetworkCredential("user", "secret", "domain");
        Diagnostics.Arrange("user", credential.UserName);
        var source = (NegotiateLogonTokenSource)new NegotiateLogonTokenSource().ForCredential(credential);

        NegotiateAuthenticationClientOptions options = source.ClientOptions(LdapLogonPackage.Ntlm, "ldap/server", ProtectionLevel.EncryptAndSign);

        Diagnostics.Act("credential user", options.Credential.UserName);
        Diagnostics.Assert("credential user", credential.UserName, options.Credential.UserName);
        Assert.AreSame(credential, options.Credential);
    }
}
