using System.Net;
using System.Net.Security;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins the Kerberos context a SASL <c>GSSAPI</c> login asks for with
/// <c>-u tester@SURL.TEST:surl-test-password --login-options AUTH=GSSAPI</c> over SMTP, IMAP and
/// POP3 (BL-5281): the mail service on the server's host, the explicit credential, and no required
/// message protection, as curl 8.21.0's SSPI build asks <c>InitializeSecurityContext</c> for no
/// flags. Asking <see cref="ProtectionLevel.Sign" /> adds <c>ISC_REQ_INTEGRITY</c>, with which
/// SSPI's Kerberos wraps the security-layer answer with sequence number 0, and an RFC 4121
/// acceptor (Surl with a test KDC) answers <c>535</c>, exit 67.
/// </summary>
[TestClass]
public sealed class SaslGssapiLoginTests
{
    private const string Host = "mail.surl.test";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("smtp", DisplayName = "SMTP")]
    [DataRow("imap", DisplayName = "IMAP")]
    [DataRow("pop", DisplayName = "POP3")]
    public void Begin_GssapiWithRealmUser_AsksAKerberosContextWithNoRequiredMessageProtection(string serviceName)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("service name", serviceName);
        diagnostics.Arrange("credential", "tester@SURL.TEST:surl-test-password");
        ScriptedSecurityContextFactory factory = new(new ScriptedSecurityContext());
        SaslAuthenticator authenticator = new(Encoding.UTF8, factory);

        authenticator.Begin("GSSAPI", Request(serviceName));

        SecurityContextRequest asked = factory.Requests.Single();
        SecurityContextRequest expected = new(SecurityMechanism.Kerberos, serviceName, Host)
        {
            UserName = "tester@SURL.TEST",
            Password = "surl-test-password",
            Domain = null,
            MessageProtection = ProtectionLevel.None,
        };
        diagnostics.Act("requested context", asked);
        diagnostics.Assert("requested context", expected, asked);
        Assert.AreEqual(expected, asked);
    }

    [TestMethod]
    [DataRow("smtp", DisplayName = "SMTP")]
    [DataRow("imap", DisplayName = "IMAP")]
    [DataRow("pop", DisplayName = "POP3")]
    public void OptionsFor_GssapiLoginRequest_TargetsTheMailServiceAndRequiresNoProtection(string serviceName)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("service name", serviceName);
        ScriptedSecurityContextFactory factory = new(new ScriptedSecurityContext());
        new SaslAuthenticator(Encoding.UTF8, factory).Begin("GSSAPI", Request(serviceName));

        NegotiateAuthenticationClientOptions options = SystemSecurityContextFactory.OptionsFor(factory.Requests.Single());

        diagnostics.Act("package", options.Package);
        diagnostics.Act("target name", options.TargetName);
        diagnostics.Act("required protection level", options.RequiredProtectionLevel);
        diagnostics.Assert("package", "Kerberos", options.Package);
        diagnostics.Assert("target name", $"{serviceName}/{Host}", options.TargetName);
        diagnostics.Assert("required protection level", ProtectionLevel.None, options.RequiredProtectionLevel);
        Assert.AreEqual("Kerberos", options.Package);
        Assert.AreEqual($"{serviceName}/{Host}", options.TargetName);
        Assert.AreEqual(ProtectionLevel.None, options.RequiredProtectionLevel);
        Assert.AreEqual("tester@SURL.TEST", options.Credential.UserName);
    }

    private static SaslRequest Request(string serviceName) =>
        new(new NetworkCredential("tester@SURL.TEST", "surl-test-password"), AuthorizationIdentity: null, BearerToken: null, RequiredMechanism: null, serviceName, Host);
}
