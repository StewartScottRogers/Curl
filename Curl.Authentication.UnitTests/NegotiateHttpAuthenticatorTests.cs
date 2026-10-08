using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CreateAuthorizationAsync_ContextMakesAToken_SendsItBase64EncodedAndKeepsTheContextForTheNextLeg()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("credential", ":");
        diagnostics.Bytes("scripted token 1", [0x60, 0x82, 0x01]);
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x60, 0x82, 0x01]));

        string? value = await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context)).CreateAuthorizationAsync(Request(":"), CancellationToken.None);

        diagnostics.Act("Authorization", value);
        diagnostics.Act("context disposed", context.IsDisposed);
        diagnostics.Assert("Authorization", "Negotiate YIIB", value);
        diagnostics.Assert("context disposed", false, context.IsDisposed);
        diagnostics.Bytes("incoming token", context.IncomingTokens.Single());
        Assert.AreEqual("Negotiate YIIB", value);
        Assert.IsFalse(context.IsDisposed);
        Assert.IsEmpty(context.IncomingTokens.Single());
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_ContextCompletesAtOnce_SendsItsTokenAndDisposesTheContext()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("credential", ":");
        diagnostics.Bytes("scripted token 1", [0x01]);
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.Completed, [0x01]));

        string? value = await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context)).CreateAuthorizationAsync(Request(":"), CancellationToken.None);

        diagnostics.Act("Authorization", value);
        diagnostics.Act("context disposed", context.IsDisposed);
        diagnostics.Assert("Authorization", "Negotiate AQ==", value);
        diagnostics.Assert("context disposed", true, context.IsDisposed);
        Assert.AreEqual("Negotiate AQ==", value);
        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_StepThrows_DisposesTheContextAndRethrows()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("credential", ":");
        diagnostics.Arrange("scripted steps", "none (the context throws InvalidOperationException)");
        ScriptedSecurityContext context = new();

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context)).CreateAuthorizationAsync(Request(":"), CancellationToken.None).AsTask());

        diagnostics.Act("exception", exception.Message);
        diagnostics.Act("context disposed", context.IsDisposed);
        diagnostics.Assert("context disposed", true, context.IsDisposed);
        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_SameTokenTwice_DisposesTheContextItReplaces()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("credential", ":");
        diagnostics.Arrange("server challenge", "Negotiate BA==");
        diagnostics.Bytes("scripted token first context", [0x01]);
        diagnostics.Bytes("scripted token second context 1", [0x01]);
        diagnostics.Bytes("scripted token second context 2", [0x02]);
        ScriptedSecurityContext first = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]));
        ScriptedSecurityContext second = new(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]),
            new SecurityContextStep(SecurityContextStatus.Completed, [0x02]));
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(first, second));

        await authenticator.CreateAuthorizationAsync(Request(":"), CancellationToken.None);
        await authenticator.CreateAuthorizationAsync(Request(":"), CancellationToken.None);
        string? value = await authenticator.ContinueAuthorizationAsync(Request(":"), "Negotiate AQ==", ["Negotiate BA=="], CancellationToken.None);

        diagnostics.Act("Authorization", value);
        diagnostics.Act("first disposed", first.IsDisposed);
        diagnostics.Act("second disposed", second.IsDisposed);
        diagnostics.Assert("first disposed", true, first.IsDisposed);
        diagnostics.Assert("Authorization", "Negotiate Ag==", value);
        diagnostics.Assert("second disposed", true, second.IsDisposed);
        Assert.IsTrue(first.IsDisposed);
        Assert.AreEqual("Negotiate Ag==", value);
        Assert.IsTrue(second.IsDisposed);
    }

    [TestMethod]
    public async Task ContinueAuthorizationAsync_AcceptorsToken_StepsTheSameContextThroughEveryLeg()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("credential", ":");
        diagnostics.Arrange("server challenge 1", "Basic realm=\"r\", negotiate  BA==  ");
        diagnostics.Arrange("server challenge 2", "Negotiate BQ==");
        diagnostics.Bytes("scripted token 1", [0x01]);
        diagnostics.Bytes("scripted token 2", [0x02]);
        diagnostics.Bytes("scripted token 3", [0x03]);
        ScriptedSecurityContext context = new(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]),
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x02]),
            new SecurityContextStep(SecurityContextStatus.Completed, [0x03]));
        ScriptedSecurityContextFactory factory = new(context);
        NegotiateHttpAuthenticator authenticator = new(factory);

        string? first = await authenticator.CreateAuthorizationAsync(Request(":"), CancellationToken.None);
        string? second = await authenticator.ContinueAuthorizationAsync(Request(":"), first!, ["Basic realm=\"r\", negotiate  BA==  "], CancellationToken.None);
        string? third = await authenticator.ContinueAuthorizationAsync(Request(":"), second!, ["Negotiate BQ=="], CancellationToken.None);

        diagnostics.Act("Authorization 1", first);
        diagnostics.Act("Authorization 2", second);
        diagnostics.Act("Authorization 3", third);
        diagnostics.Act("contexts requested", factory.Requests.Count);
        diagnostics.Bytes("incoming token 2", context.IncomingTokens[1]);
        diagnostics.Bytes("incoming token 3", context.IncomingTokens[2]);
        diagnostics.Assert("Authorization 1", "Negotiate AQ==", first);
        diagnostics.Assert("Authorization 2", "Negotiate Ag==", second);
        diagnostics.Assert("Authorization 3", "Negotiate Aw==", third);
        diagnostics.Assert("contexts requested", 1, factory.Requests.Count);
        diagnostics.Assert("context disposed", true, context.IsDisposed);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("credential", ":");
        diagnostics.Arrange("server challenge", challenge);
        diagnostics.Bytes("scripted token 1", [0x01]);
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]));
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(context));
        string? sent = await authenticator.CreateAuthorizationAsync(Request(":"), CancellationToken.None);

        string? value = await authenticator.ContinueAuthorizationAsync(Request(":"), sent!, [challenge], CancellationToken.None);

        diagnostics.Act("first Authorization", sent);
        diagnostics.Act("second Authorization", value);
        diagnostics.Act("incoming token count", context.IncomingTokens.Count);
        diagnostics.Assert("second Authorization", null, value);
        diagnostics.Assert("context disposed", true, context.IsDisposed);
        diagnostics.Assert("incoming token count", 1, context.IncomingTokens.Count);
        Assert.IsNull(value);
        Assert.IsTrue(context.IsDisposed);
        Assert.HasCount(1, context.IncomingTokens);
    }

    [TestMethod]
    public async Task ContinueAuthorizationAsync_ContextRefusesTheToken_SendsNothingAndDisposesIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("credential", ":");
        diagnostics.Arrange("server challenge", "Negotiate BA==");
        diagnostics.Arrange("scripted statuses", "ContinueNeeded, Refused");
        diagnostics.Bytes("scripted token 1", [0x01]);
        ScriptedSecurityContext context = new(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]),
            new SecurityContextStep(SecurityContextStatus.Refused, []));
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(context));
        string? sent = await authenticator.CreateAuthorizationAsync(Request(":"), CancellationToken.None);

        string? value = await authenticator.ContinueAuthorizationAsync(Request(":"), sent!, ["Negotiate BA=="], CancellationToken.None);

        diagnostics.Act("first Authorization", sent);
        diagnostics.Act("second Authorization", value);
        diagnostics.Assert("second Authorization", null, value);
        diagnostics.Assert("context disposed", true, context.IsDisposed);
        Assert.IsNull(value);
        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    [DataRow("Negotiate AQ==", DisplayName = "A value no context awaiting a leg made")]
    [DataRow("Basic dTpw", DisplayName = "Not a Negotiate value")]
    public async Task ContinueAuthorizationAsync_NoContextAwaitsTheValueSent_SendsNothing(string sent)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("value sent", sent);
        diagnostics.Arrange("server challenge", "Negotiate BA==");

        string? value = await Default.ContinueAuthorizationAsync(Request(":"), sent, ["Negotiate BA=="], CancellationToken.None);

        diagnostics.Act("Authorization", value);
        diagnostics.Assert("Authorization", null, value);
        Assert.IsNull(value);
    }

    [TestMethod]
    public async Task EndAuthorization_A200CarriedTheAcceptorsFinalToken_DisposesTheKeptContextWithoutSteppingIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("credential", ":");
        diagnostics.Arrange("server challenge after end", "Negotiate BA==");
        diagnostics.Bytes("scripted token 1", [0x01]);
        ScriptedSecurityContext context = new(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]),
            new SecurityContextStep(SecurityContextStatus.Refused, []));
        RecordingInfoEvents events = new();
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(context));
        string? sent = await authenticator.CreateAuthorizationAsync(Request(":"), CancellationToken.None);

        authenticator.EndAuthorization(sent!);
        string? afterwards = await authenticator.ContinueAuthorizationAsync(Request(":") with { Events = events }, sent!, ["Negotiate BA=="], CancellationToken.None);

        diagnostics.Act("Authorization sent", sent);
        diagnostics.Act("Authorization afterwards", afterwards);
        diagnostics.Act("info lines", string.Join(" | ", events.Info));
        diagnostics.Assert("context disposed", true, context.IsDisposed);
        diagnostics.Assert("incoming token count", 1, context.IncomingTokens.Count);
        diagnostics.Assert("Authorization afterwards", null, afterwards);
        diagnostics.Assert("info line count", 0, events.Info.Count);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("value sent", sent);
        diagnostics.Bytes("scripted token 1", [0x01]);
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]));
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(context));

        authenticator.EndAuthorization(sent);

        diagnostics.Act("context disposed", context.IsDisposed);
        diagnostics.Assert("context disposed", false, context.IsDisposed);
        Assert.IsFalse(context.IsDisposed);
    }

    [TestMethod]
    public void EndAuthorization_NullValue_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("value sent", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => Default.EndAuthorization(null!));

        diagnostics.Act("exception", exception.Message);
        diagnostics.Assert("exception type", typeof(ArgumentNullException), exception.GetType());
    }

    [TestMethod]
    public async Task ContinueAuthorizationAsync_NullArguments_Throw()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("sent", "null, then Negotiate AQ== with null challenges");

        var first = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Default.ContinueAuthorizationAsync(Request(":"), null!, [], CancellationToken.None).AsTask());
        var second = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Default.ContinueAuthorizationAsync(Request(":"), "Negotiate AQ==", null!, CancellationToken.None).AsTask());

        diagnostics.Act("exception 1", first.Message);
        diagnostics.Act("exception 2", second.Message);
        diagnostics.Assert("exception 1 type", typeof(ArgumentNullException), first.GetType());
        diagnostics.Assert("exception 2 type", typeof(ArgumentNullException), second.GetType());
    }

    [TestMethod]
    [DataRow(SecurityContextStatus.NoCredentials, DisplayName = "No ticket (SEC_E_NO_CREDENTIALS, gss_init_sec_context)")]
    [DataRow(SecurityContextStatus.NoMechanism, DisplayName = "No mechanism")]
    [DataRow(SecurityContextStatus.Refused, DisplayName = "KDC refused")]
    public async Task CreateAuthorizationAsync_ContextFails_SendsNothing(SecurityContextStatus status)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("credential", ":");
        diagnostics.Arrange("scripted status", status);
        ScriptedSecurityContext context = new(new SecurityContextStep(status, []));

        string? value = await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context)).CreateAuthorizationAsync(Request(":"), CancellationToken.None);

        diagnostics.Act("Authorization", value);
        diagnostics.Assert("Authorization", null, value);
        diagnostics.Assert("context disposed", true, context.IsDisposed);
        Assert.IsNull(value);
        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_ContinueNeededWithEmptyToken_SendsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("credential", ":");
        diagnostics.Bytes("scripted token 1", []);
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, []));

        string? value = await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context)).CreateAuthorizationAsync(Request(":"), CancellationToken.None);

        diagnostics.Act("Authorization", value);
        diagnostics.Assert("Authorization", null, value);
        Assert.IsNull(value);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_NullRequest_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("request", "null");

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory()).CreateAuthorizationAsync(null!, CancellationToken.None).AsTask());

        diagnostics.Act("exception", exception.Message);
        diagnostics.Assert("exception type", typeof(ArgumentNullException), exception.GetType());
    }

    [TestMethod]
    public void ContextRequestFor_ServerCertificate_PassesItForChannelBindings()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] certificate = [0x30, 0x03, 0x02, 0x01, 0x01];
        diagnostics.Arrange("server certificate length", certificate.Length);
        diagnostics.Bytes("server certificate", certificate);
        HttpAuthRequest request = Request(":") with { ServerCertificate = certificate };

        byte[] passed = Default.ContextRequestFor(request).ServerCertificate.ToArray();

        diagnostics.Act("certificate passed", Convert.ToHexString(passed));
        diagnostics.Diff("certificate passed", Convert.ToHexString(certificate), Convert.ToHexString(passed));
        CollectionAssert.AreEqual(certificate, passed);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no -u")]
    [DataRow(":", DisplayName = "-u :")]
    [DataRow(":secret", DisplayName = "-u :secret")]
    public void ContextRequestFor_NoUserName_AsksForTheDefaultCredentials(string? userColonPassword)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("-u", userColonPassword);

        SecurityContextRequest request = Default.ContextRequestFor(Request(userColonPassword));

        SecurityContextRequest expected = new(SecurityMechanism.Negotiate, "HTTP", "server.example.test");
        diagnostics.Act("request", request);
        diagnostics.Assert("request", expected, request);
        Assert.AreEqual(new SecurityContextRequest(SecurityMechanism.Negotiate, "HTTP", "server.example.test"), request);
    }

    [TestMethod]
    public void ContextRequestFor_IPv6Host_NamesTheHostWithoutBrackets()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "http://[::1]:8080/");
        HttpAuthRequest request = Request(":") with { Url = CurlUrl.Parse("http://[::1]:8080/") };

        string hostName = Default.ContextRequestFor(request).HostName;

        diagnostics.Act("host name", hostName);
        diagnostics.Assert("host name", "::1", hostName);
        Assert.AreEqual("::1", Default.ContextRequestFor(request).HostName);
    }

    [TestMethod]
    [DataRow("alice:pw", null, "alice", DisplayName = "user")]
    [DataRow("EXAMPLE\\alice:pw", "EXAMPLE", "alice", DisplayName = "DOMAIN\\user")]
    [DataRow("EXAMPLE/alice:pw", "EXAMPLE", "alice", DisplayName = "DOMAIN/user")]
    public void ContextRequestFor_UserName_PassesTheExplicitCredential(string userColonPassword, string? domain, string user)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("-u", userColonPassword);
        diagnostics.Arrange("expected domain", domain);
        diagnostics.Arrange("expected user", user);

        SecurityContextRequest request = Default.ContextRequestFor(Request(userColonPassword));

        diagnostics.Act("user name", request.UserName);
        diagnostics.Act("password", request.Password);
        diagnostics.Act("domain", request.Domain);
        diagnostics.Act("delegation", request.Delegation);
        diagnostics.Assert("user name", user, request.UserName);
        diagnostics.Assert("password", "pw", request.Password);
        diagnostics.Assert("domain", domain, request.Domain);
        diagnostics.Assert("delegation", SecurityDelegation.None, request.Delegation);
        Assert.AreEqual(user, request.UserName);
        Assert.AreEqual("pw", request.Password);
        Assert.AreEqual(domain, request.Domain);
        Assert.AreEqual(SecurityDelegation.None, request.Delegation);
    }

    [TestMethod]
    public void ContextRequestFor_CredentialWithItsOwnDomain_KeepsIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("credential", "CORP\\alice:pw");
        HttpAuthRequest request = Request(":") with { Credential = new NetworkCredential("alice", "pw", "CORP") };

        string? domain = Default.ContextRequestFor(request).Domain;

        diagnostics.Act("domain", domain);
        diagnostics.Assert("domain", "CORP", domain);
        Assert.AreEqual("CORP", Default.ContextRequestFor(request).Domain);
    }

    [TestMethod]
    [DataRow(false, "HTTP", DisplayName = "server, no --service-name")]
    [DataRow(true, "HTTP", DisplayName = "proxy, no --proxy-service-name")]
    public void ContextRequestFor_NoServiceNames_AsksForHttpWithoutDelegation(bool isProxy, string serviceName)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("proxy", isProxy);
        diagnostics.Arrange("options", "none");

        SecurityContextRequest request = Default.ContextRequestFor(Request(":") with { IsProxy = isProxy });

        diagnostics.Act("service name", request.ServiceName);
        diagnostics.Act("delegation", request.Delegation);
        diagnostics.Assert("service name", serviceName, request.ServiceName);
        diagnostics.Assert("delegation", SecurityDelegation.None, request.Delegation);
        Assert.AreEqual(serviceName, request.ServiceName);
        Assert.AreEqual(SecurityDelegation.None, request.Delegation);
    }

    [TestMethod]
    [DataRow(false, "svc", DisplayName = "server takes --service-name")]
    [DataRow(true, "proxysvc", DisplayName = "proxy takes --proxy-service-name")]
    public void ContextRequestFor_BothServiceNames_AsksForTheOneForItsPeer(bool isProxy, string serviceName)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("proxy", isProxy);
        diagnostics.Arrange("options", "service name svc, proxy service name proxysvc");
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(), new NegotiateOptions("svc", "proxysvc", SecurityDelegation.None));

        SecurityContextRequest request = authenticator.ContextRequestFor(Request(":") with { IsProxy = isProxy });

        SecurityContextRequest expected = new(SecurityMechanism.Negotiate, serviceName, "server.example.test");
        diagnostics.Act("request", request);
        diagnostics.Assert("request", expected, request);
        Assert.AreEqual(new SecurityContextRequest(SecurityMechanism.Negotiate, serviceName, "server.example.test"), request);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "server, only --proxy-service-name")]
    [DataRow(true, DisplayName = "proxy, only --service-name")]
    public void ContextRequestFor_OnlyTheOtherPeersServiceName_AsksForHttp(bool isProxy)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("proxy", isProxy);
        NegotiateOptions options = isProxy ? new NegotiateOptions("svc", null, SecurityDelegation.None) : new NegotiateOptions(null, "proxysvc", SecurityDelegation.None);
        diagnostics.Arrange("options", options);

        SecurityContextRequest request = new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(), options).ContextRequestFor(Request(":") with { IsProxy = isProxy });

        diagnostics.Act("service name", request.ServiceName);
        diagnostics.Assert("service name", "HTTP", request.ServiceName);
        Assert.AreEqual("HTTP", request.ServiceName);
    }

    [TestMethod]
    [DataRow(false, SecurityDelegation.Policy)]
    [DataRow(false, SecurityDelegation.Always)]
    [DataRow(true, SecurityDelegation.Policy)]
    [DataRow(true, SecurityDelegation.Always)]
    public void ContextRequestFor_Delegation_PassesTheLevelForServerAndProxy(bool isProxy, SecurityDelegation delegation)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("proxy", isProxy);
        diagnostics.Arrange("delegation", delegation);
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(), NegotiateOptions.Default with { Delegation = delegation });

        SecurityDelegation passed = authenticator.ContextRequestFor(Request("alice:pw") with { IsProxy = isProxy }).Delegation;

        diagnostics.Act("delegation passed", passed);
        diagnostics.Assert("delegation passed", delegation, passed);
        Assert.AreEqual(delegation, authenticator.ContextRequestFor(Request("alice:pw") with { IsProxy = isProxy }).Delegation);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_ServiceNameAndDelegation_ReachTheFactory()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("options", "service name svc, delegation Always");
        diagnostics.Bytes("scripted token 1", [0x01]);
        ScriptedSecurityContextFactory factory = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01])));

        await new NegotiateHttpAuthenticator(factory, new NegotiateOptions("svc", null, SecurityDelegation.Always)).CreateAuthorizationAsync(Request(":"), CancellationToken.None);

        SecurityContextRequest expected = new(SecurityMechanism.Negotiate, "svc", "server.example.test") { Delegation = SecurityDelegation.Always };
        diagnostics.Act("factory request", factory.Requests.Single());
        diagnostics.Assert("factory request", expected, factory.Requests.Single());
        Assert.AreEqual(new SecurityContextRequest(SecurityMechanism.Negotiate, "svc", "server.example.test") { Delegation = SecurityDelegation.Always }, factory.Requests.Single());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task CreateAuthorizationAsync_NoTicketOnWindows_ReportsSspisNoCredentialsLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("scripted status", SecurityContextStatus.NoCredentials);
        RecordingInfoEvents events = new();
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.NoCredentials, []));

        await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context)).CreateAuthorizationAsync(Request(":") with { Events = events }, CancellationToken.None);

        string expected = "InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package";
        diagnostics.Act("info lines", string.Join(" | ", events.Info));
        diagnostics.Diff("info lines", expected, string.Join(" | ", events.Info));
        CollectionAssert.AreEqual(new[] { "InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package" }, events.Info);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task CreateAuthorizationAsync_NoTicketOffWindows_ReportsGssApisNoCredentialsLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("scripted status", SecurityContextStatus.NoCredentials);
        RecordingInfoEvents events = new();
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.NoCredentials, []));

        await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context)).CreateAuthorizationAsync(Request(":") with { Events = events }, CancellationToken.None);

        string expected = "gss_init_sec_context() failed: No credentials were supplied, or the credentials were unavailable or inaccessible. SPNEGO cannot find mechanisms to negotiate. ";
        diagnostics.Act("info lines", string.Join(" | ", events.Info));
        diagnostics.Diff("info lines", expected, string.Join(" | ", events.Info));
        CollectionAssert.AreEqual(new[] { "gss_init_sec_context() failed: No credentials were supplied, or the credentials were unavailable or inaccessible. SPNEGO cannot find mechanisms to negotiate. " }, events.Info);
    }

    [TestMethod]
    [DataRow(true, "InitializeSecurityContext failed: SEC_E_LOGON_DENIED (0x8009030c) - The logon attempt failed", DisplayName = "SSPI wording")]
    [DataRow(false, "gss_init_sec_context() failed: Unspecified GSS failure.  Minor code may provide more information. ", DisplayName = "GSS-API wording")]
    public async Task ContinueAuthorizationAsync_ContextRefusesTheToken_ReportsTheFailureInTheWordingAsked(bool wordsAsSspi, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("words failures as SSPI", wordsAsSspi);
        diagnostics.Arrange("server challenge", "Negotiate BA==");
        diagnostics.Bytes("scripted token 1", [0x01]);
        RecordingInfoEvents events = new();
        ScriptedSecurityContext context = new(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]),
            new SecurityContextStep(SecurityContextStatus.Refused, []));
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(context), wordsFailuresAsSspi: wordsAsSspi);
        string? sent = await authenticator.CreateAuthorizationAsync(Request(":") with { Events = events }, CancellationToken.None);

        await authenticator.ContinueAuthorizationAsync(Request(":") with { Events = events }, sent!, ["Negotiate BA=="], CancellationToken.None);

        diagnostics.Act("info lines", string.Join(" | ", events.Info));
        diagnostics.Diff("info lines", expected, string.Join(" | ", events.Info));
        CollectionAssert.AreEqual(new[] { expected }, events.Info);
    }

    [TestMethod]
    [DataRow(SecurityContextStatus.ContinueNeeded, DisplayName = "Goes on")]
    [DataRow(SecurityContextStatus.Completed, DisplayName = "Completes")]
    public async Task CreateAuthorizationAsync_ContextMakesAToken_ReportsNothing(SecurityContextStatus status)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("scripted status", status);
        diagnostics.Bytes("scripted token 1", [0x01]);
        RecordingInfoEvents events = new();
        ScriptedSecurityContext context = new(new SecurityContextStep(status, [0x01]));

        await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context)).CreateAuthorizationAsync(Request(":") with { Events = events }, CancellationToken.None);

        diagnostics.Act("info lines", string.Join(" | ", events.Info));
        diagnostics.Assert("info line count", 0, events.Info.Count);
        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    public async Task StepWithoutAnsweringAsync_ContextFails_ReportsTheFailureAndDisposesTheContext()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("scripted status", SecurityContextStatus.NoCredentials);
        diagnostics.Arrange("words failures as SSPI", true);
        diagnostics.Arrange("server challenge", "Negotiate");
        RecordingInfoEvents events = new();
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.NoCredentials, []));
        ScriptedSecurityContextFactory factory = new(context);

        await new NegotiateHttpAuthenticator(factory, wordsFailuresAsSspi: true).StepWithoutAnsweringAsync(Request(null) with { Events = events }, ["Negotiate"], CancellationToken.None);

        SecurityContextRequest expectedRequest = new(SecurityMechanism.Negotiate, "HTTP", "server.example.test");
        diagnostics.Act("info lines", string.Join(" | ", events.Info));
        diagnostics.Act("factory request", factory.Requests.Single());
        diagnostics.Diff("info lines", NegotiateFailureLines.For(SecurityContextStatus.NoCredentials, wordsAsSspi: true), string.Join(" | ", events.Info));
        diagnostics.Assert("context disposed", true, context.IsDisposed);
        diagnostics.Assert("factory request", expectedRequest, factory.Requests.Single());
        CollectionAssert.AreEqual(new[] { NegotiateFailureLines.For(SecurityContextStatus.NoCredentials, wordsAsSspi: true) }, events.Info);
        Assert.IsTrue(context.IsDisposed);
        Assert.AreEqual(new SecurityContextRequest(SecurityMechanism.Negotiate, "HTTP", "server.example.test"), factory.Requests.Single());
    }

    [TestMethod]
    public async Task StepWithoutAnsweringAsync_ContextMakesAToken_ReportsNothingAndKeepsNoContext()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("server challenge", "Negotiate");
        diagnostics.Bytes("scripted token 1", [0x01]);
        RecordingInfoEvents events = new();
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]));
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(context));

        await authenticator.StepWithoutAnsweringAsync(Request(null) with { Events = events }, ["Negotiate"], CancellationToken.None);

        diagnostics.Act("info lines", string.Join(" | ", events.Info));
        diagnostics.Assert("info line count", 0, events.Info.Count);
        diagnostics.Assert("context disposed", true, context.IsDisposed);
        Assert.IsEmpty(events.Info);
        Assert.IsTrue(context.IsDisposed);
        string? afterwards = await authenticator.ContinueAuthorizationAsync(Request(null), "Negotiate AQ==", ["Negotiate BA=="], CancellationToken.None);
        diagnostics.Act("Authorization afterwards", afterwards);
        diagnostics.Assert("Authorization afterwards", null, afterwards);
        Assert.IsNull(afterwards);
    }

    [TestMethod]
    public async Task StepWithoutAnsweringAsync_StepThrows_DisposesTheContextAndRethrows()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("server challenge", "Negotiate");
        diagnostics.Arrange("scripted steps", "none (the context throws InvalidOperationException)");
        ScriptedSecurityContext context = new();

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context)).StepWithoutAnsweringAsync(Request(null), ["Negotiate"], CancellationToken.None).AsTask());

        diagnostics.Act("exception", exception.Message);
        diagnostics.Assert("context disposed", true, context.IsDisposed);
        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    public async Task StepWithoutAnsweringAsync_NullRequest_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("request", "null");

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Default.StepWithoutAnsweringAsync(null!, ["Negotiate"], CancellationToken.None).AsTask());

        diagnostics.Act("exception", exception.Message);
        diagnostics.Assert("exception type", typeof(ArgumentNullException), exception.GetType());
    }

    [TestMethod]
    public async Task ContinueAuthorizationAsync_NullRequest_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("request", "null");

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Default.ContinueAuthorizationAsync(null!, "Negotiate AQ==", [], CancellationToken.None).AsTask());

        diagnostics.Act("exception", exception.Message);
        diagnostics.Assert("exception type", typeof(ArgumentNullException), exception.GetType());
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
