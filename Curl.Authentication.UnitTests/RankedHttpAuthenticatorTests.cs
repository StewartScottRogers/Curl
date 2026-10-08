using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="RankedHttpAuthenticator" /> to the scheme curl 8.21.0 (mingw) answered
/// when a loopback server offered several (BL-218 Notes), and to when NTLM answers and goes
/// on after a credential was sent (BL-526 Notes).
/// </summary>
[TestClass]
public sealed class RankedHttpAuthenticatorTests
{
    // The response curl sent for -u u:p, GET /a, realm "r", nonce "n", MD5, no qop.
    private const string MeasuredDigestResponse = "response=\"544c035f0f40d9ebf0295157d8041f8c\"";

    private const string Basic = "Basic realm=\"r\"";
    private const string Digest = "Digest realm=\"r\", nonce=\"n\"";

    private static readonly RankedHttpAuthenticator Authenticator = WithContexts(new ScriptedSecurityContextFactory());

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(HttpAuthSchemes.Any, new[] { Basic, Digest }, DisplayName = "--anyauth, Basic then Digest")]
    [DataRow(HttpAuthSchemes.Any, new[] { "Basic realm=\"r\", Digest realm=\"r\", nonce=\"n\"" }, DisplayName = "--anyauth, both in one header")]
    [DataRow(HttpAuthSchemes.Basic | HttpAuthSchemes.Digest, new[] { Basic, Digest }, DisplayName = "--basic --digest, Basic then Digest")]
    [DataRow(HttpAuthSchemes.Digest, new[] { Digest }, DisplayName = "--digest, Digest")]
    public void CreateAuthorization_DigestRanksFirst_AnswersDigest(HttpAuthSchemes allowed, string[] challenges)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("allowed", allowed);
        diagnostics.Arrange("challenges", string.Join(" | ", challenges));

        string? value = Authenticator.CreateAuthorization(Request(allowed), challenges);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Assert("digest response present", true, value!.Contains(MeasuredDigestResponse, StringComparison.Ordinal));
        StringAssert.StartsWith(value, "Digest username=\"u\"");
        StringAssert.Contains(value, MeasuredDigestResponse);
    }

    [TestMethod]
    public void RepeatAuthorization_DigestAnswer_CountsItsNonceOn()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge", Digest + ", qop=\"auth\"");
        string sent = Authenticator.CreateAuthorization(Request(HttpAuthSchemes.Digest), [Digest + ", qop=\"auth\""])!;

        string value = Authenticator.RepeatAuthorization(Request(HttpAuthSchemes.Digest), sent);
        diagnostics.Act("sent", sent);
        diagnostics.Act("repeated", value);

        diagnostics.Assert("first nc", true, sent.Contains("nc=00000001", StringComparison.Ordinal));
        diagnostics.Assert("second nc", true, value.Contains("nc=00000002", StringComparison.Ordinal));
        StringAssert.Contains(sent, "cnonce=\"c\", nc=00000001", StringComparison.Ordinal);
        StringAssert.Contains(value, "cnonce=\"c\", nc=00000002", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task EndAuthorization_NegotiateContextKeptForTheNextLeg_DisposesOfItWithoutSteppingIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]));
        RankedHttpAuthenticator authenticator = WithContexts(new ScriptedSecurityContextFactory(context));
        diagnostics.Arrange("scheme", HttpAuthSchemes.Negotiate);
        string? sent = await authenticator.CreateAuthorizationAsync(Request(HttpAuthSchemes.Negotiate), [], CancellationToken.None);

        authenticator.EndAuthorization(sent!);
        diagnostics.Act("sent", sent ?? "<null>");
        diagnostics.Act("disposed", context.IsDisposed);

        diagnostics.Assert("disposed", true, context.IsDisposed);
        diagnostics.Assert("incoming token count", 1, context.IncomingTokens.Count());
        Assert.IsTrue(context.IsDisposed);
        Assert.HasCount(1, context.IncomingTokens);
    }

    [TestMethod]
    public void RepeatAuthorization_BasicAnswer_SendsItAsSent()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("sent", "Basic dTpw");

        string repeated = Authenticator.RepeatAuthorization(Request(HttpAuthSchemes.Basic), "Basic dTpw");
        diagnostics.Act("repeated", repeated);

        diagnostics.Diff("repeated", "Basic dTpw", repeated);
        Assert.AreEqual("Basic dTpw", Authenticator.RepeatAuthorization(Request(HttpAuthSchemes.Basic), "Basic dTpw"));
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Any, DisplayName = "--anyauth, Basic only")]
    [DataRow(HttpAuthSchemes.Basic | HttpAuthSchemes.Digest, DisplayName = "--basic --digest, Basic only")]
    public void CreateAuthorization_OnlyBasicOfferedAndAllowed_AnswersBasic(HttpAuthSchemes allowed)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("allowed", allowed);

        string? value = Authenticator.CreateAuthorization(Request(allowed), [Basic]);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Diff("authorization", "Basic dTpw", value ?? "<null>");
        Assert.AreEqual("Basic dTpw", Authenticator.CreateAuthorization(Request(allowed), [Basic]));
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Any, new[] { Basic, "NTLM" }, DisplayName = "--anyauth, Basic and NTLM: NTLM picked")]
    [DataRow(HttpAuthSchemes.Any, new[] { "NTLM" }, DisplayName = "--anyauth, NTLM only")]
    [DataRow(HttpAuthSchemes.Any, new[] { "Negotiate", Basic }, DisplayName = "--anyauth, Negotiate and Basic: Negotiate picked")]
    [DataRow(HttpAuthSchemes.Any, new[] { "Negotiate", Digest }, DisplayName = "--anyauth, Negotiate and Digest: Negotiate picked")]
    [DataRow(HttpAuthSchemes.Any, new[] { "Digest realm=\"r\"", Basic }, DisplayName = "--anyauth, Digest without nonce and Basic")]
    [DataRow(HttpAuthSchemes.Any, new[] { "Foo realm=\"r\"" }, DisplayName = "--anyauth, unknown scheme only")]
    [DataRow(HttpAuthSchemes.Digest, new[] { Basic }, DisplayName = "--digest, Basic only")]
    [DataRow(HttpAuthSchemes.Basic, new[] { Digest }, DisplayName = "--basic, Digest only")]
    public void CreateAuthorization_PickIsNotAnswerable_SendsNothingAndDoesNotFallBack(HttpAuthSchemes allowed, string[] challenges)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("allowed", allowed);
        diagnostics.Arrange("challenges", string.Join(" | ", challenges));

        string? value = Authenticator.CreateAuthorization(Request(allowed), challenges);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Assert("authorization is null", true, value is null);
        Assert.IsNull(Authenticator.CreateAuthorization(Request(allowed), challenges));
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Basic, "Basic dTpw", DisplayName = "-u u:p")]
    [DataRow(HttpAuthSchemes.Any, null, DisplayName = "--anyauth")]
    [DataRow(HttpAuthSchemes.Digest, null, DisplayName = "--digest")]
    public void CreateAuthorization_BeforeAnyChallenge_AnswersAsCurlDoes(HttpAuthSchemes allowed, string? expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("allowed", allowed);
        diagnostics.Arrange("expected", expected ?? "<null>");

        string? value = Authenticator.CreateAuthorization(Request(allowed), []);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Diff("authorization", expected ?? "<null>", value ?? "<null>");
        Assert.AreEqual(expected, Authenticator.CreateAuthorization(Request(allowed), []));
    }

    [TestMethod]
    public void CreateAuthorization_BearerOutranksDigest_AnswersBearer()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        HttpAuthRequest request = Request(HttpAuthSchemes.Digest | HttpAuthSchemes.Bearer) with { BearerToken = "tok" };
        diagnostics.Arrange("bearer token", "tok");

        string? value = Authenticator.CreateAuthorization(request, [Digest, "Bearer realm=\"r\""]);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Diff("authorization", "Bearer tok", value ?? "<null>");
        Assert.AreEqual("Bearer tok", Authenticator.CreateAuthorization(request, [Digest, "Bearer realm=\"r\""]));
    }

    [TestMethod]
    public void CreateAuthorization_NullArguments_Throw()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("arguments", "null request, then null challenges");

        var first = Assert.ThrowsExactly<ArgumentNullException>(() => Authenticator.CreateAuthorization(null!, []));
        var second = Assert.ThrowsExactly<ArgumentNullException>(() => Authenticator.CreateAuthorization(Request(HttpAuthSchemes.Any), null!));
        diagnostics.Act("first parameter", first.ParamName);
        diagnostics.Act("second parameter", second.ParamName);

        diagnostics.Assert("exception type", typeof(ArgumentNullException), first.GetType());
        diagnostics.Assert("exception type", typeof(ArgumentNullException), second.GetType());
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Negotiate, DisplayName = "--negotiate")]
    [DataRow(HttpAuthSchemes.Any, DisplayName = "--anyauth")]
    public async Task CreateAuthorizationAsync_NegotiatePickedWithCredential_AnswersNegotiate(HttpAuthSchemes allowed)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01, 0x02])));
        diagnostics.Arrange("allowed", allowed);
        diagnostics.Bytes("scripted token", new byte[] { 0x01, 0x02 });

        string? value = await WithContexts(contexts).CreateAuthorizationAsync(Request(allowed), ["Negotiate", Basic], CancellationToken.None);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Diff("authorization", "Negotiate AQI=", value ?? "<null>");
        diagnostics.Assert("host", "127.0.0.1", contexts.Requests.Single().HostName);
        Assert.AreEqual("Negotiate AQI=", value);
        Assert.AreEqual("127.0.0.1", contexts.Requests.Single().HostName);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_NegotiateAloneBeforeAnyChallenge_TriesNegotiateEvenWithoutCredential()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01])));
        HttpAuthRequest request = Request(HttpAuthSchemes.Negotiate) with { Credential = null };
        diagnostics.Arrange("credential", "none");

        string? value = await WithContexts(contexts).CreateAuthorizationAsync(request, [], CancellationToken.None);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Diff("authorization", "Negotiate AQ==", value ?? "<null>");
        Assert.AreEqual("Negotiate AQ==", value);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_NoTicket_SendsNothingAsBothPlatformCurlsDo()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.NoCredentials, [])));
        diagnostics.Arrange("challenge", "Negotiate");

        string? value = await WithContexts(contexts).CreateAuthorizationAsync(Request(HttpAuthSchemes.Negotiate), ["Negotiate"], CancellationToken.None);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Assert("authorization is null", true, value is null);
        Assert.IsNull(value);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Any, DisplayName = "--anyauth")]
    [DataRow(HttpAuthSchemes.Negotiate | HttpAuthSchemes.Basic, DisplayName = "--negotiate --basic")]
    public async Task CreateAuthorizationAsync_NegotiatePickedAfterTheChallengeWithNoTicket_AsksForTheRequestAgainWithoutAHeader(HttpAuthSchemes allowed)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.NoCredentials, [])));
        RecordingInfoEvents events = new();
        diagnostics.Arrange("allowed", allowed);

        string? value = await WithContexts(contexts).CreateAuthorizationAsync(Request(allowed) with { Events = events }, ["Negotiate"], CancellationToken.None);
        diagnostics.Act("authorization", value ?? "<null>");
        diagnostics.Act("info count", events.Info.Count());

        diagnostics.Diff("authorization", string.Empty, value ?? "<null>");
        diagnostics.Assert("info count", 1, events.Info.Count());
        Assert.AreEqual(string.Empty, value);
        Assert.HasCount(1, events.Info);
    }

    [TestMethod]
    public async Task ContinueAuthorizationAsync_401ToTheRequestSentAgainWithoutAHeader_StepsAContextAndTakesTheResponse()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.NoCredentials, []));
        ScriptedSecurityContextFactory contexts = new(context);
        RecordingInfoEvents events = new();
        diagnostics.Arrange("sent", "<empty>");
        diagnostics.Arrange("challenge", "Negotiate");

        string? value = await WithContexts(contexts).ContinueAuthorizationAsync(Request(HttpAuthSchemes.Any) with { Events = events }, string.Empty, sentBeforeAnyChallenge: false, ["Negotiate"], CancellationToken.None);
        diagnostics.Act("authorization", value ?? "<null>");
        diagnostics.Act("info count", events.Info.Count());

        diagnostics.Assert("disposed", true, context.IsDisposed);
        diagnostics.Assert("info count", 1, events.Info.Count());
        Assert.IsNull(value);
        Assert.HasCount(1, events.Info);
        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    [DataRow(false, "Basic realm=\"r\"", DisplayName = "Negotiate no longer offered")]
    [DataRow(true, "Negotiate", DisplayName = "No -u")]
    public async Task ContinueAuthorizationAsync_401ToTheRequestSentAgainWithoutAHeaderNotNegotiates_StepsNoContext(bool withoutCredential, string challenge)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContextFactory contexts = new();
        diagnostics.Arrange("withoutCredential", withoutCredential);
        diagnostics.Arrange("challenge", challenge);
        HttpAuthRequest request = Request(HttpAuthSchemes.Any) with { Credential = withoutCredential ? null : new NetworkCredential("u", "p") };

        string? value = await WithContexts(contexts).ContinueAuthorizationAsync(request, string.Empty, sentBeforeAnyChallenge: false, [challenge], CancellationToken.None);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Assert("context requests", 0, contexts.Requests.Count());
        Assert.IsNull(value);
        Assert.IsEmpty(contexts.Requests);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_NegotiateAloneWithoutCredential_StepsAContextButSendsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.Completed, [0x01]));
        ScriptedSecurityContextFactory contexts = new(context);
        HttpAuthRequest request = Request(HttpAuthSchemes.Negotiate) with { Credential = null };
        diagnostics.Arrange("credential", "none");

        string? value = await WithContexts(contexts).CreateAuthorizationAsync(request, ["Negotiate"], CancellationToken.None);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Assert("context requests", 1, contexts.Requests.Count());
        diagnostics.Assert("disposed", true, context.IsDisposed);
        Assert.IsNull(value);
        Assert.HasCount(1, contexts.Requests);
        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_NegotiateAloneWithoutCredentialAndNoTicket_ReportsTheFailure()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.NoCredentials, [])));
        RecordingInfoEvents events = new();
        HttpAuthRequest request = Request(HttpAuthSchemes.Negotiate) with { Credential = null, Events = events };
        diagnostics.Arrange("credential", "none");

        string? value = await WithContexts(contexts).CreateAuthorizationAsync(request, ["Negotiate"], CancellationToken.None);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Assert("info count", 1, events.Info.Count());
        Assert.HasCount(1, events.Info);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Any, false, "Negotiate", DisplayName = "--anyauth: no pick before the challenge")]
    [DataRow(HttpAuthSchemes.Negotiate, false, "Basic realm=\"r\"", DisplayName = "Negotiate not offered")]
    public async Task CreateAuthorizationAsync_NegotiateNotTheOnePickWithoutCredential_StepsNoContext(HttpAuthSchemes allowed, bool isProxy, string challenge)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContextFactory contexts = new();
        diagnostics.Arrange("allowed", allowed);
        diagnostics.Arrange("isProxy", isProxy);
        diagnostics.Arrange("challenge", challenge);
        HttpAuthRequest request = Request(allowed) with { Credential = null, IsProxy = isProxy };

        string? value = await WithContexts(contexts).CreateAuthorizationAsync(request, [challenge], CancellationToken.None);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Assert("context requests", 0, contexts.Requests.Count());
        Assert.IsNull(value);
        Assert.IsEmpty(contexts.Requests);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Any, DisplayName = "--anyauth, before any challenge")]
    [DataRow(HttpAuthSchemes.Negotiate | HttpAuthSchemes.Basic, DisplayName = "--negotiate --basic, before any challenge")]
    public async Task CreateAuthorizationAsync_SeveralSchemesBeforeAnyChallenge_TriesNoNegotiate(HttpAuthSchemes allowed)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContextFactory contexts = new();
        diagnostics.Arrange("allowed", allowed);

        string? value = await WithContexts(contexts).CreateAuthorizationAsync(Request(allowed), [], CancellationToken.None);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Assert("context requests", 0, contexts.Requests.Count());
        Assert.IsNull(value);
        Assert.IsEmpty(contexts.Requests);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_ProxyOfferingNegotiate_AnswersForHttpOnTheProxysHost()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01])));
        diagnostics.Arrange("challenge", "Negotiate");

        string? value = await WithContexts(contexts).CreateAuthorizationAsync(ProxyRequest(HttpAuthSchemes.Negotiate), ["Negotiate"], CancellationToken.None);
        diagnostics.Act("authorization", value ?? "<null>");
        diagnostics.Act("host", contexts.Requests.Single().HostName);

        diagnostics.Diff("authorization", "Negotiate AQ==", value ?? "<null>");
        diagnostics.Assert("service", "HTTP", contexts.Requests.Single().ServiceName);
        Assert.AreEqual("Negotiate AQ==", value);
        Assert.AreEqual(SecurityMechanism.Negotiate, contexts.Requests.Single().Mechanism);
        Assert.AreEqual("HTTP", contexts.Requests.Single().ServiceName);
        Assert.AreEqual("proxy.test", contexts.Requests.Single().HostName);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_ProxyNegotiateAloneBeforeAnyChallenge_TriesIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.NoCredentials, [])));
        HttpAuthRequest request = ProxyRequest(HttpAuthSchemes.Negotiate) with { Credential = null };
        diagnostics.Arrange("credential", "none");

        string? value = await WithContexts(contexts).CreateAuthorizationAsync(request, [], CancellationToken.None);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Assert("context requests", 1, contexts.Requests.Count());
        Assert.IsNull(value);
        Assert.HasCount(1, contexts.Requests);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_Proxy407NegotiateWithoutCredential_StepsAContextAndSendsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.NoCredentials, [])));
        HttpAuthRequest request = ProxyRequest(HttpAuthSchemes.Negotiate) with { Credential = null };
        diagnostics.Arrange("credential", "none");

        string? value = await WithContexts(contexts).CreateAuthorizationAsync(request, ["Negotiate"], CancellationToken.None);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Assert("context requests", 1, contexts.Requests.Count());
        Assert.IsNull(value);
        Assert.HasCount(1, contexts.Requests);
    }

    [TestMethod]
    public async Task ContinueAuthorizationAsync_ProxyNegotiateTokenInThe407_SendsTheContextsNextToken()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContext context = new(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]),
            new SecurityContextStep(SecurityContextStatus.Completed, [0x02]));
        RankedHttpAuthenticator authenticator = WithContexts(new ScriptedSecurityContextFactory(context));
        HttpAuthRequest request = ProxyRequest(HttpAuthSchemes.Negotiate);
        diagnostics.Bytes("scripted tokens", new byte[] { 0x01, 0x02 });
        diagnostics.Arrange("challenge", "Negotiate BA==");
        string? sent = await authenticator.CreateAuthorizationAsync(request, [], CancellationToken.None);

        string? value = await authenticator.ContinueAuthorizationAsync(request, sent!, sentBeforeAnyChallenge: true, ["Negotiate BA=="], CancellationToken.None);
        diagnostics.Act("sent", sent ?? "<null>");
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Diff("authorization", "Negotiate Ag==", value ?? "<null>");
        Assert.AreEqual("Negotiate Ag==", value);
    }

    [TestMethod]
    public async Task ProxyNtlm_Type1ThenType2_SendsType3ForHttpOnTheProxysHost()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContextFactory contexts = new(
            new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01])),
            new ScriptedSecurityContext(
                new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]),
                new SecurityContextStep(SecurityContextStatus.Completed, [0x03])));
        RankedHttpAuthenticator authenticator = WithContexts(contexts);
        HttpAuthRequest request = ProxyRequest(HttpAuthSchemes.Ntlm);
        diagnostics.Arrange("scheme", HttpAuthSchemes.Ntlm);

        string? type1 = await authenticator.CreateAuthorizationAsync(request, [], CancellationToken.None);
        string? type3 = await authenticator.ContinueAuthorizationAsync(request, type1!, sentBeforeAnyChallenge: true, ["NTLM " + HandBuiltNtlmSecurityContextTests.MeasuredChallenge], CancellationToken.None);
        diagnostics.Act("type1", type1 ?? "<null>");
        diagnostics.Act("type3", type3 ?? "<null>");

        diagnostics.Diff("type1", "NTLM AQ==", type1 ?? "<null>");
        diagnostics.Diff("type3", "NTLM Aw==", type3 ?? "<null>");
        Assert.AreEqual("NTLM AQ==", type1);
        Assert.AreEqual("NTLM Aw==", type3);
        Assert.IsTrue(contexts.Requests.All(sent => sent is { Mechanism: SecurityMechanism.Ntlm, ServiceName: "HTTP", HostName: "proxy.test" }));
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_DigestPicked_AnswersAsTheSynchronousCallDoes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenges", Basic + " | " + Digest);

        string? value = await Authenticator.CreateAuthorizationAsync(Request(HttpAuthSchemes.Any), [Basic, Digest], CancellationToken.None);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Assert("digest response present", true, value!.Contains(MeasuredDigestResponse, StringComparison.Ordinal));
        StringAssert.Contains(value, MeasuredDigestResponse);
    }

    [TestMethod]
    public void CreateAuthorization_NegotiatePicked_SendsNothingWithoutIo()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge", "Negotiate");

        string? value = Authenticator.CreateAuthorization(Request(HttpAuthSchemes.Negotiate), ["Negotiate"]);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Assert("authorization is null", true, value is null);
        Assert.IsNull(Authenticator.CreateAuthorization(Request(HttpAuthSchemes.Negotiate), ["Negotiate"]));
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_NullArguments_Throw()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("arguments", "null request, then null challenges");

        var first = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Authenticator.CreateAuthorizationAsync(null!, [], CancellationToken.None).AsTask());
        var second = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Authenticator.CreateAuthorizationAsync(Request(HttpAuthSchemes.Any), null!, CancellationToken.None).AsTask());
        diagnostics.Act("first parameter", first.ParamName);
        diagnostics.Act("second parameter", second.ParamName);

        diagnostics.Assert("exception type", typeof(ArgumentNullException), first.GetType());
        diagnostics.Assert("exception type", typeof(ArgumentNullException), second.GetType());
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Ntlm, new string[0], DisplayName = "--ntlm, before any challenge")]
    [DataRow(HttpAuthSchemes.Any, new[] { Basic, "NTLM" }, DisplayName = "--anyauth, Basic and NTLM: NTLM picked")]
    [DataRow(HttpAuthSchemes.Ntlm | HttpAuthSchemes.Basic, new[] { "NTLM" }, DisplayName = "--ntlm --basic, NTLM")]
    public async Task CreateAuthorizationAsync_NtlmAnswers_SendsType1(HttpAuthSchemes allowed, string[] challenges)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01])));
        diagnostics.Arrange("allowed", allowed);
        diagnostics.Arrange("challenges", string.Join(" | ", challenges));
        diagnostics.Bytes("scripted token", new byte[] { 0x01 });

        string? value = await WithContexts(contexts).CreateAuthorizationAsync(Request(allowed), challenges, CancellationToken.None);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Diff("authorization", "NTLM AQ==", value ?? "<null>");
        Assert.AreEqual("NTLM AQ==", value);
        Assert.AreEqual(SecurityMechanism.Ntlm, contexts.Requests.Single().Mechanism);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Ntlm, new string[0], false, true, DisplayName = "--ntlm without -u, before any challenge")]
    [DataRow(HttpAuthSchemes.Any, new[] { "NTLM" }, false, true, DisplayName = "--anyauth without -u, NTLM")]
    [DataRow(HttpAuthSchemes.Any, new string[0], false, false, DisplayName = "--anyauth, before any challenge")]
    public async Task CreateAuthorizationAsync_NtlmDoesNotAnswer_SendsNothing(HttpAuthSchemes allowed, string[] challenges, bool isProxy, bool withoutCredential)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContextFactory contexts = new();
        diagnostics.Arrange("allowed", allowed);
        diagnostics.Arrange("challenges", string.Join(" | ", challenges));
        diagnostics.Arrange("isProxy", isProxy);
        diagnostics.Arrange("withoutCredential", withoutCredential);
        HttpAuthRequest request = Request(allowed) with { IsProxy = isProxy, Credential = withoutCredential ? null : new NetworkCredential("u", "p") };

        string? value = await WithContexts(contexts).CreateAuthorizationAsync(request, challenges, CancellationToken.None);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Assert("context requests", 0, contexts.Requests.Count());
        Assert.IsNull(value);
        Assert.IsEmpty(contexts.Requests);
    }

    [TestMethod]
    public async Task ContinueAuthorizationAsync_Type2AfterType1_SendsType3()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContext context = new(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]),
            new SecurityContextStep(SecurityContextStatus.Completed, [0x03]));
        diagnostics.Arrange("sent", "NTLM AQ==");

        string? value = await WithContexts(new ScriptedSecurityContextFactory(context))
            .ContinueAuthorizationAsync(Request(HttpAuthSchemes.Ntlm), "NTLM AQ==", sentBeforeAnyChallenge: true, ["NTLM " + HandBuiltNtlmSecurityContextTests.MeasuredChallenge], CancellationToken.None);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Diff("authorization", "NTLM Aw==", value ?? "<null>");
        Assert.AreEqual("NTLM Aw==", value);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Basic, "Basic dTpw", new[] { Basic }, false, DisplayName = "Basic refused")]
    [DataRow(HttpAuthSchemes.Any, "Digest x", new[] { Digest, "NTLM" }, false, DisplayName = "Digest refused, Digest still the pick")]
    [DataRow(HttpAuthSchemes.Ntlm, "NTLM AQ==", new string[0], false, DisplayName = "No challenge")]
    public async Task ContinueAuthorizationAsync_NotAnNtlmLeg_SendsNothing(HttpAuthSchemes allowed, string sent, string[] challenges, bool isProxy)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContextFactory contexts = new();
        diagnostics.Arrange("allowed", allowed);
        diagnostics.Arrange("sent", sent);
        diagnostics.Arrange("challenges", string.Join(" | ", challenges));
        diagnostics.Arrange("isProxy", isProxy);

        string? value = await WithContexts(contexts).ContinueAuthorizationAsync(Request(allowed) with { IsProxy = isProxy }, sent, sentBeforeAnyChallenge: true, challenges, CancellationToken.None);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Assert("context requests", 0, contexts.Requests.Count());
        Assert.IsNull(value);
        Assert.IsEmpty(contexts.Requests);
    }

    [TestMethod]
    public async Task ContinueAuthorizationAsync_NegotiateTokenInThe401_SendsTheContextsNextToken()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContext context = new(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]),
            new SecurityContextStep(SecurityContextStatus.Completed, [0x02]));
        RankedHttpAuthenticator authenticator = WithContexts(new ScriptedSecurityContextFactory(context));
        HttpAuthRequest request = Request(HttpAuthSchemes.Negotiate);
        diagnostics.Bytes("scripted tokens", new byte[] { 0x01, 0x02 });
        diagnostics.Arrange("challenge", "Negotiate BA==");
        string? sent = await authenticator.CreateAuthorizationAsync(request, [], CancellationToken.None);

        string? value = await authenticator.ContinueAuthorizationAsync(request, sent!, sentBeforeAnyChallenge: true, ["Negotiate BA=="], CancellationToken.None);
        diagnostics.Act("sent", sent ?? "<null>");
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Diff("authorization", "Negotiate Ag==", value ?? "<null>");
        Assert.AreEqual("Negotiate Ag==", value);
    }

    [TestMethod]
    [DataRow(true, false, DisplayName = "No -u")]
    public async Task ContinueAuthorizationAsync_NotANegotiateLeg_SendsNothing(bool withoutCredential, bool isProxy)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]));
        RankedHttpAuthenticator authenticator = WithContexts(new ScriptedSecurityContextFactory(context));
        diagnostics.Arrange("withoutCredential", withoutCredential);
        diagnostics.Arrange("isProxy", isProxy);
        HttpAuthRequest request = Request(HttpAuthSchemes.Negotiate) with { Credential = withoutCredential ? null : new NetworkCredential("u", "p") };
        string? sent = await authenticator.CreateAuthorizationAsync(request, [], CancellationToken.None);

        string? value = await authenticator.ContinueAuthorizationAsync(request with { IsProxy = isProxy }, sent!, sentBeforeAnyChallenge: true, ["Negotiate BA=="], CancellationToken.None);
        diagnostics.Act("authorization", value ?? "<null>");

        diagnostics.Assert("incoming token count", 1, context.IncomingTokens.Count());
        Assert.IsNull(value);
        Assert.HasCount(1, context.IncomingTokens);
    }

    [TestMethod]
    public async Task ContinueAuthorizationAsync_NullArguments_Throw()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        HttpAuthRequest request = Request(HttpAuthSchemes.Ntlm);
        diagnostics.Arrange("arguments", "null request, null sent, null challenges in turn");

        var first = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Authenticator.ContinueAuthorizationAsync(null!, "NTLM AQ==", true, ["NTLM"], CancellationToken.None).AsTask());
        var second = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Authenticator.ContinueAuthorizationAsync(request, null!, true, ["NTLM"], CancellationToken.None).AsTask());
        var third = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Authenticator.ContinueAuthorizationAsync(request, "NTLM AQ==", true, null!, CancellationToken.None).AsTask());
        diagnostics.Act("first parameter", first.ParamName);
        diagnostics.Act("second parameter", second.ParamName);
        diagnostics.Act("third parameter", third.ParamName);

        diagnostics.Assert("exception type", typeof(ArgumentNullException), first.GetType());
        diagnostics.Assert("exception type", typeof(ArgumentNullException), second.GetType());
        diagnostics.Assert("exception type", typeof(ArgumentNullException), third.GetType());
    }

    private static RankedHttpAuthenticator WithContexts(ScriptedSecurityContextFactory contexts) => new(
        new BasicAndBearerAuthenticator(Encoding.UTF8),
        new DigestAuthenticator(Encoding.UTF8, () => "c"),
        new NegotiateHttpAuthenticator(contexts),
        new NtlmHttpAuthenticator(contexts, matchesSspiBuild: false));

    private static HttpAuthRequest Request(HttpAuthSchemes allowed) =>
        new("GET", CurlUrl.Parse("http://127.0.0.1:18218/a"), "/a", new NetworkCredential("u", "p"), null, allowed, IsProxy: false);

    private static HttpAuthRequest ProxyRequest(HttpAuthSchemes allowed) =>
        new("CONNECT", CurlUrl.Parse("http://proxy.test:3128/"), "example.test:80", new NetworkCredential("u", "p"), null, allowed, IsProxy: true);
}
