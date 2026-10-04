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
    public async Task CreateAuthorizationAsync_ContextMakesAToken_SendsItBase64EncodedAndKeepsTheContextForTheNextLeg()
    {
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x60, 0x82, 0x01]));

        string? value = await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context)).CreateAuthorizationAsync(Request(":"), CancellationToken.None);

        Assert.AreEqual("Negotiate YIIB", value);
        Assert.IsFalse(context.IsDisposed);
        Assert.IsEmpty(context.IncomingTokens.Single());
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_ContextCompletesAtOnce_SendsItsTokenAndDisposesTheContext()
    {
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.Completed, [0x01]));

        string? value = await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context)).CreateAuthorizationAsync(Request(":"), CancellationToken.None);

        Assert.AreEqual("Negotiate AQ==", value);
        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_StepThrows_DisposesTheContextAndRethrows()
    {
        ScriptedSecurityContext context = new();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context)).CreateAuthorizationAsync(Request(":"), CancellationToken.None).AsTask());

        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_SameTokenTwice_DisposesTheContextItReplaces()
    {
        ScriptedSecurityContext first = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]));
        ScriptedSecurityContext second = new(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]),
            new SecurityContextStep(SecurityContextStatus.Completed, [0x02]));
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(first, second));

        await authenticator.CreateAuthorizationAsync(Request(":"), CancellationToken.None);
        await authenticator.CreateAuthorizationAsync(Request(":"), CancellationToken.None);
        string? value = await authenticator.ContinueAuthorizationAsync(Request(":"), "Negotiate AQ==", ["Negotiate BA=="], CancellationToken.None);

        Assert.IsTrue(first.IsDisposed);
        Assert.AreEqual("Negotiate Ag==", value);
        Assert.IsTrue(second.IsDisposed);
    }

    [TestMethod]
    public async Task ContinueAuthorizationAsync_AcceptorsToken_StepsTheSameContextThroughEveryLeg()
    {
        ScriptedSecurityContext context = new(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]),
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x02]),
            new SecurityContextStep(SecurityContextStatus.Completed, [0x03]));
        ScriptedSecurityContextFactory factory = new(context);
        NegotiateHttpAuthenticator authenticator = new(factory);

        string? first = await authenticator.CreateAuthorizationAsync(Request(":"), CancellationToken.None);
        string? second = await authenticator.ContinueAuthorizationAsync(Request(":"), first!, ["Basic realm=\"r\", negotiate  BA==  "], CancellationToken.None);
        string? third = await authenticator.ContinueAuthorizationAsync(Request(":"), second!, ["Negotiate BQ=="], CancellationToken.None);

        Assert.AreEqual("Negotiate AQ==", first);
        Assert.AreEqual("Negotiate Ag==", second);
        Assert.AreEqual("Negotiate Aw==", third);
        Assert.HasCount(1, factory.Requests);
        CollectionAssert.AreEqual(new byte[] { 0x04 }, context.IncomingTokens[1]);
        CollectionAssert.AreEqual(new byte[] { 0x05 }, context.IncomingTokens[2]);
        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    [DataRow("Negotiate", DisplayName = "Bare Negotiate: curl's LOGIN_DENIED")]
    [DataRow("Negotiate @@@", DisplayName = "Token that is not base64")]
    [DataRow("Basic realm=\"r\"", DisplayName = "No Negotiate challenge")]
    public async Task ContinueAuthorizationAsync_NoTokenToStepWith_EndsOnThe401AndDisposesTheContext(string challenge)
    {
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]));
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(context));
        string? sent = await authenticator.CreateAuthorizationAsync(Request(":"), CancellationToken.None);

        string? value = await authenticator.ContinueAuthorizationAsync(Request(":"), sent!, [challenge], CancellationToken.None);

        Assert.IsNull(value);
        Assert.IsTrue(context.IsDisposed);
        Assert.HasCount(1, context.IncomingTokens);
    }

    [TestMethod]
    public async Task ContinueAuthorizationAsync_ContextRefusesTheToken_SendsNothingAndDisposesIt()
    {
        ScriptedSecurityContext context = new(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]),
            new SecurityContextStep(SecurityContextStatus.Refused, []));
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(context));
        string? sent = await authenticator.CreateAuthorizationAsync(Request(":"), CancellationToken.None);

        string? value = await authenticator.ContinueAuthorizationAsync(Request(":"), sent!, ["Negotiate BA=="], CancellationToken.None);

        Assert.IsNull(value);
        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    [DataRow("Negotiate AQ==", DisplayName = "A value no context awaiting a leg made")]
    [DataRow("Basic dTpw", DisplayName = "Not a Negotiate value")]
    public async Task ContinueAuthorizationAsync_NoContextAwaitsTheValueSent_SendsNothing(string sent)
    {
        string? value = await Default.ContinueAuthorizationAsync(Request(":"), sent, ["Negotiate BA=="], CancellationToken.None);

        Assert.IsNull(value);
    }

    [TestMethod]
    public async Task EndAuthorization_A200CarriedTheAcceptorsFinalToken_DisposesTheKeptContextWithoutSteppingIt()
    {
        ScriptedSecurityContext context = new(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]),
            new SecurityContextStep(SecurityContextStatus.Refused, []));
        RecordingInfoEvents events = new();
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(context));
        string? sent = await authenticator.CreateAuthorizationAsync(Request(":"), CancellationToken.None);

        authenticator.EndAuthorization(sent!);
        string? afterwards = await authenticator.ContinueAuthorizationAsync(Request(":") with { Events = events }, sent!, ["Negotiate BA=="], CancellationToken.None);

        Assert.IsTrue(context.IsDisposed);
        Assert.HasCount(1, context.IncomingTokens);
        Assert.IsNull(afterwards);
        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    [DataRow("Negotiate AQ==", DisplayName = "A value no context awaiting a leg made")]
    [DataRow("", DisplayName = "Sent without a header")]
    public void EndAuthorization_NoContextKeptForTheValue_DoesNothing(string sent)
    {
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]));
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(context));

        authenticator.EndAuthorization(sent);

        Assert.IsFalse(context.IsDisposed);
    }

    [TestMethod]
    public void EndAuthorization_NullValue_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Default.EndAuthorization(null!));
    }

    [TestMethod]
    public async Task ContinueAuthorizationAsync_NullArguments_Throw()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Default.ContinueAuthorizationAsync(Request(":"), null!, [], CancellationToken.None).AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Default.ContinueAuthorizationAsync(Request(":"), "Negotiate AQ==", null!, CancellationToken.None).AsTask());
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
    public void ContextRequestFor_ServerCertificate_PassesItForChannelBindings()
    {
        byte[] certificate = [0x30, 0x03, 0x02, 0x01, 0x01];
        HttpAuthRequest request = Request(":") with { ServerCertificate = certificate };

        CollectionAssert.AreEqual(certificate, Default.ContextRequestFor(request).ServerCertificate.ToArray());
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

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task CreateAuthorizationAsync_NoTicketOnWindows_ReportsSspisNoCredentialsLine()
    {
        RecordingInfoEvents events = new();
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.NoCredentials, []));

        await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context)).CreateAuthorizationAsync(Request(":") with { Events = events }, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package" }, events.Info);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task CreateAuthorizationAsync_NoTicketOffWindows_ReportsGssApisNoCredentialsLine()
    {
        RecordingInfoEvents events = new();
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.NoCredentials, []));

        await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context)).CreateAuthorizationAsync(Request(":") with { Events = events }, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "gss_init_sec_context() failed: No credentials were supplied, or the credentials were unavailable or inaccessible. SPNEGO cannot find mechanisms to negotiate. " }, events.Info);
    }

    [TestMethod]
    [DataRow(true, "InitializeSecurityContext failed: SEC_E_LOGON_DENIED (0x8009030c) - The logon attempt failed", DisplayName = "SSPI wording")]
    [DataRow(false, "gss_init_sec_context() failed: Unspecified GSS failure.  Minor code may provide more information. ", DisplayName = "GSS-API wording")]
    public async Task ContinueAuthorizationAsync_ContextRefusesTheToken_ReportsTheFailureInTheWordingAsked(bool wordsAsSspi, string expected)
    {
        RecordingInfoEvents events = new();
        ScriptedSecurityContext context = new(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]),
            new SecurityContextStep(SecurityContextStatus.Refused, []));
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(context), wordsFailuresAsSspi: wordsAsSspi);
        string? sent = await authenticator.CreateAuthorizationAsync(Request(":") with { Events = events }, CancellationToken.None);

        await authenticator.ContinueAuthorizationAsync(Request(":") with { Events = events }, sent!, ["Negotiate BA=="], CancellationToken.None);

        CollectionAssert.AreEqual(new[] { expected }, events.Info);
    }

    [TestMethod]
    [DataRow(SecurityContextStatus.ContinueNeeded, DisplayName = "Goes on")]
    [DataRow(SecurityContextStatus.Completed, DisplayName = "Completes")]
    public async Task CreateAuthorizationAsync_ContextMakesAToken_ReportsNothing(SecurityContextStatus status)
    {
        RecordingInfoEvents events = new();
        ScriptedSecurityContext context = new(new SecurityContextStep(status, [0x01]));

        await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context)).CreateAuthorizationAsync(Request(":") with { Events = events }, CancellationToken.None);

        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    public async Task StepWithoutAnsweringAsync_ContextFails_ReportsTheFailureAndDisposesTheContext()
    {
        RecordingInfoEvents events = new();
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.NoCredentials, []));
        ScriptedSecurityContextFactory factory = new(context);

        await new NegotiateHttpAuthenticator(factory, wordsFailuresAsSspi: true).StepWithoutAnsweringAsync(Request(null) with { Events = events }, ["Negotiate"], CancellationToken.None);

        CollectionAssert.AreEqual(new[] { NegotiateFailureLines.For(SecurityContextStatus.NoCredentials, wordsAsSspi: true) }, events.Info);
        Assert.IsTrue(context.IsDisposed);
        Assert.AreEqual(new SecurityContextRequest(SecurityMechanism.Negotiate, "HTTP", "server.example.test"), factory.Requests.Single());
    }

    [TestMethod]
    public async Task StepWithoutAnsweringAsync_ContextMakesAToken_ReportsNothingAndKeepsNoContext()
    {
        RecordingInfoEvents events = new();
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]));
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(context));

        await authenticator.StepWithoutAnsweringAsync(Request(null) with { Events = events }, ["Negotiate"], CancellationToken.None);

        Assert.IsEmpty(events.Info);
        Assert.IsTrue(context.IsDisposed);
        Assert.IsNull(await authenticator.ContinueAuthorizationAsync(Request(null), "Negotiate AQ==", ["Negotiate BA=="], CancellationToken.None));
    }

    [TestMethod]
    public async Task StepWithoutAnsweringAsync_StepThrows_DisposesTheContextAndRethrows()
    {
        ScriptedSecurityContext context = new();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context)).StepWithoutAnsweringAsync(Request(null), ["Negotiate"], CancellationToken.None).AsTask());

        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    public async Task StepWithoutAnsweringAsync_NullRequest_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Default.StepWithoutAnsweringAsync(null!, ["Negotiate"], CancellationToken.None).AsTask());
    }

    [TestMethod]
    public async Task ContinueAuthorizationAsync_NullRequest_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Default.ContinueAuthorizationAsync(null!, "Negotiate AQ==", [], CancellationToken.None).AsTask());
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
