using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="NegotiateHttpAuthenticator" />: the <c>Authorization: Negotiate</c> value a
/// context's first token makes, the service principal it asks for (<c>HTTP</c> on the URL's
/// host), the credential it passes on, and the measured failure: with no ticket both platform
/// curls send nothing more (BL-527 Notes).
/// </summary>
[TestClass]
public sealed class NegotiateHttpAuthenticatorTests
{
    [TestMethod]
    public async Task CreateAuthorizationAsync_ContextMakesAToken_SendsItBase64EncodedAndDisposesTheContext()
    {
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x60, 0x82, 0x01]));

        string? value = await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context)).CreateAuthorizationAsync(Request(":"), CancellationToken.None);

        Assert.AreEqual("Negotiate YIIB", value);
        Assert.IsTrue(context.IsDisposed);
        Assert.IsEmpty(context.IncomingTokens.Single());
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_ContextCompletesAtOnce_SendsItsToken()
    {
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.Completed, [0x01]));

        string? value = await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context)).CreateAuthorizationAsync(Request(":"), CancellationToken.None);

        Assert.AreEqual("Negotiate AQ==", value);
    }

    [TestMethod]
    [DataRow(SecurityContextStatus.NoCredentials, DisplayName = "No ticket (SEC_E_NO_CREDENTIALS, gss_init_sec_context)")]
    [DataRow(SecurityContextStatus.NoMechanism, DisplayName = "No mechanism")]
    [DataRow(SecurityContextStatus.Refused, DisplayName = "KDC refused")]
    public async Task CreateAuthorizationAsync_ContextFails_SendsNothing(SecurityContextStatus status)
    {
        ScriptedSecurityContext context = new(new SecurityContextStep(status, []));

        string? value = await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context)).CreateAuthorizationAsync(Request(":"), CancellationToken.None);

        Assert.IsNull(value);
        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_ContinueNeededWithEmptyToken_SendsNothing()
    {
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, []));

        string? value = await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context)).CreateAuthorizationAsync(Request(":"), CancellationToken.None);

        Assert.IsNull(value);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_NullRequest_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory()).CreateAuthorizationAsync(null!, CancellationToken.None).AsTask());
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no -u")]
    [DataRow(":", DisplayName = "-u :")]
    [DataRow(":secret", DisplayName = "-u :secret")]
    public void ContextRequestFor_NoUserName_AsksForTheDefaultCredentials(string? userColonPassword)
    {
        SecurityContextRequest request = NegotiateHttpAuthenticator.ContextRequestFor(Request(userColonPassword));

        Assert.AreEqual(new SecurityContextRequest(SecurityMechanism.Negotiate, "HTTP", "server.example.test"), request);
    }

    [TestMethod]
    public void ContextRequestFor_IPv6Host_NamesTheHostWithoutBrackets()
    {
        HttpAuthRequest request = Request(":") with { Url = CurlUrl.Parse("http://[::1]:8080/") };

        Assert.AreEqual("::1", NegotiateHttpAuthenticator.ContextRequestFor(request).HostName);
    }

    [TestMethod]
    [DataRow("alice:pw", null, "alice", DisplayName = "user")]
    [DataRow("EXAMPLE\\alice:pw", "EXAMPLE", "alice", DisplayName = "DOMAIN\\user")]
    [DataRow("EXAMPLE/alice:pw", "EXAMPLE", "alice", DisplayName = "DOMAIN/user")]
    public void ContextRequestFor_UserName_PassesTheExplicitCredential(string userColonPassword, string? domain, string user)
    {
        SecurityContextRequest request = NegotiateHttpAuthenticator.ContextRequestFor(Request(userColonPassword));

        Assert.AreEqual(user, request.UserName);
        Assert.AreEqual("pw", request.Password);
        Assert.AreEqual(domain, request.Domain);
        Assert.AreEqual(SecurityDelegation.None, request.Delegation);
    }

    [TestMethod]
    public void ContextRequestFor_CredentialWithItsOwnDomain_KeepsIt()
    {
        HttpAuthRequest request = Request(":") with { Credential = new NetworkCredential("alice", "pw", "CORP") };

        Assert.AreEqual("CORP", NegotiateHttpAuthenticator.ContextRequestFor(request).Domain);
    }

    private static HttpAuthRequest Request(string? userColonPassword)
    {
        NetworkCredential? credential = userColonPassword is null
            ? null
            : new NetworkCredential(userColonPassword[..userColonPassword.IndexOf(':')], userColonPassword[(userColonPassword.IndexOf(':') + 1)..]);
        return new HttpAuthRequest("GET", CurlUrl.Parse("http://server.example.test/"), "/", credential, null, HttpAuthSchemes.Negotiate, IsProxy: false);
    }
}
