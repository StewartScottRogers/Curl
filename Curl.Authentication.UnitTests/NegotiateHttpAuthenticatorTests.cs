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
        SecurityContextRequest request = Default.ContextRequestFor(Request(userColonPassword));

        Assert.AreEqual(new SecurityContextRequest(SecurityMechanism.Negotiate, "HTTP", "server.example.test"), request);
    }

    [TestMethod]
    public void ContextRequestFor_IPv6Host_NamesTheHostWithoutBrackets()
    {
        HttpAuthRequest request = Request(":") with { Url = CurlUrl.Parse("http://[::1]:8080/") };

        Assert.AreEqual("::1", Default.ContextRequestFor(request).HostName);
    }

    [TestMethod]
    [DataRow("alice:pw", null, "alice", DisplayName = "user")]
    [DataRow("EXAMPLE\\alice:pw", "EXAMPLE", "alice", DisplayName = "DOMAIN\\user")]
    [DataRow("EXAMPLE/alice:pw", "EXAMPLE", "alice", DisplayName = "DOMAIN/user")]
    public void ContextRequestFor_UserName_PassesTheExplicitCredential(string userColonPassword, string? domain, string user)
    {
        SecurityContextRequest request = Default.ContextRequestFor(Request(userColonPassword));

        Assert.AreEqual(user, request.UserName);
        Assert.AreEqual("pw", request.Password);
        Assert.AreEqual(domain, request.Domain);
        Assert.AreEqual(SecurityDelegation.None, request.Delegation);
    }

    [TestMethod]
    public void ContextRequestFor_CredentialWithItsOwnDomain_KeepsIt()
    {
        HttpAuthRequest request = Request(":") with { Credential = new NetworkCredential("alice", "pw", "CORP") };

        Assert.AreEqual("CORP", Default.ContextRequestFor(request).Domain);
    }

    [TestMethod]
    [DataRow(false, "HTTP", DisplayName = "server, no --service-name")]
    [DataRow(true, "HTTP", DisplayName = "proxy, no --proxy-service-name")]
    public void ContextRequestFor_NoServiceNames_AsksForHttpWithoutDelegation(bool isProxy, string serviceName)
    {
        SecurityContextRequest request = Default.ContextRequestFor(Request(":") with { IsProxy = isProxy });

        Assert.AreEqual(serviceName, request.ServiceName);
        Assert.AreEqual(SecurityDelegation.None, request.Delegation);
    }

    [TestMethod]
    [DataRow(false, "svc", DisplayName = "server takes --service-name")]
    [DataRow(true, "proxysvc", DisplayName = "proxy takes --proxy-service-name")]
    public void ContextRequestFor_BothServiceNames_AsksForTheOneForItsPeer(bool isProxy, string serviceName)
    {
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(), new NegotiateOptions("svc", "proxysvc", SecurityDelegation.None));

        SecurityContextRequest request = authenticator.ContextRequestFor(Request(":") with { IsProxy = isProxy });

        Assert.AreEqual(new SecurityContextRequest(SecurityMechanism.Negotiate, serviceName, "server.example.test"), request);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "server, only --proxy-service-name")]
    [DataRow(true, DisplayName = "proxy, only --service-name")]
    public void ContextRequestFor_OnlyTheOtherPeersServiceName_AsksForHttp(bool isProxy)
    {
        NegotiateOptions options = isProxy ? new NegotiateOptions("svc", null, SecurityDelegation.None) : new NegotiateOptions(null, "proxysvc", SecurityDelegation.None);

        SecurityContextRequest request = new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(), options).ContextRequestFor(Request(":") with { IsProxy = isProxy });

        Assert.AreEqual("HTTP", request.ServiceName);
    }

    [TestMethod]
    [DataRow(false, SecurityDelegation.Policy)]
    [DataRow(false, SecurityDelegation.Always)]
    [DataRow(true, SecurityDelegation.Policy)]
    [DataRow(true, SecurityDelegation.Always)]
    public void ContextRequestFor_Delegation_PassesTheLevelForServerAndProxy(bool isProxy, SecurityDelegation delegation)
    {
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(), NegotiateOptions.Default with { Delegation = delegation });

        Assert.AreEqual(delegation, authenticator.ContextRequestFor(Request("alice:pw") with { IsProxy = isProxy }).Delegation);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_ServiceNameAndDelegation_ReachTheFactory()
    {
        ScriptedSecurityContextFactory factory = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01])));

        await new NegotiateHttpAuthenticator(factory, new NegotiateOptions("svc", null, SecurityDelegation.Always)).CreateAuthorizationAsync(Request(":"), CancellationToken.None);

        Assert.AreEqual(new SecurityContextRequest(SecurityMechanism.Negotiate, "svc", "server.example.test") { Delegation = SecurityDelegation.Always }, factory.Requests.Single());
    }

    private static NegotiateHttpAuthenticator Default { get; } = new(new ScriptedSecurityContextFactory());

    private static HttpAuthRequest Request(string? userColonPassword)
    {
        NetworkCredential? credential = userColonPassword is null
            ? null
            : new NetworkCredential(userColonPassword[..userColonPassword.IndexOf(':')], userColonPassword[(userColonPassword.IndexOf(':') + 1)..]);
        return new HttpAuthRequest("GET", CurlUrl.Parse("http://server.example.test/"), "/", credential, null, HttpAuthSchemes.Negotiate, IsProxy: false);
    }
}
