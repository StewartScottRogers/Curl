using System.Net;
using System.Text;
using Curl.Authentication.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Pins what the authenticators write to Curl's own diagnostic log, component <c>auth</c>
/// (BL-923, ADR-0222): the scheme and SASL mechanism chosen at <c>info</c>, a scheme that cannot
/// answer at <c>warning</c>, each round and the Digest parameters at <c>verbose</c>, a failure that
/// ends the transfer at <c>error</c>, and never the password <c>s3cret</c> or a token's base64.
/// </summary>
[TestClass]
public sealed class AuthDiagnosticLogTests
{
    private const string Secret = "s3cret";

    private const string Basic = "Basic realm=\"r\"";

    private const string Digest = "Digest realm=\"r\", nonce=\"n\"";

    private static readonly byte[] Type1 = Convert.FromBase64String(HandBuiltNtlmSecurityContextTests.CurlType1);

    private static readonly byte[] Type3 = [.. "NTLMSSP\0"u8, 3, 0, 0, 0, 9, 9];

    private static readonly string Type2Challenge = "NTLM " + HandBuiltNtlmSecurityContextTests.MeasuredChallenge;

    [TestMethod]
    public void CreateAuthorization_DigestAndBasicOffered_LogsDigestChosenAtInfo()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);

        string? value = Ranked(log, new ScriptedSecurityContextFactory()).CreateAuthorization(HttpRequest(HttpAuthSchemes.Any), [Basic, Digest]);

        CollectionAssert.AreEqual(new[] { "server offered Basic, Digest; allowed Any; chose Digest" }, log.At(DiagnosticLogLevel.Info));
        Assert.AreEqual(DiagnosticLogComponents.Auth, log.Lines[0].Component);
        AssertNoSecret(log, value!);
    }

    [TestMethod]
    public void CreateAuthorization_BasicChosen_LogsNoPasswordAndNoBase64Credential()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        string? value = Ranked(log, new ScriptedSecurityContextFactory()).CreateAuthorization(HttpRequest(HttpAuthSchemes.Basic), [Basic]);

        CollectionAssert.AreEqual(new[] { "server offered Basic; allowed Basic; chose Basic" }, log.At(DiagnosticLogLevel.Info));
        AssertNoSecret(log, value!);
        AssertNoSecret(log, Convert.ToBase64String(Encoding.UTF8.GetBytes("u:" + Secret)));
    }

    [TestMethod]
    public void CreateAuthorization_NoOfferedSchemeAllowed_LogsAWarning()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        Ranked(log, new ScriptedSecurityContextFactory()).CreateAuthorization(HttpRequest(HttpAuthSchemes.Basic), [Digest]);

        CollectionAssert.AreEqual(new[] { "server offered Digest; none of the allowed Basic can answer" }, log.At(DiagnosticLogLevel.Warning));
        Assert.IsEmpty(log.At(DiagnosticLogLevel.Info));
    }

    [TestMethod]
    public void CreateAuthorization_BeforeAnyChallenge_LogsNothing()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        Ranked(log, new ScriptedSecurityContextFactory()).CreateAuthorization(HttpRequest(HttpAuthSchemes.Basic), []);

        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    [DataRow("Digest realm=\"r\", nonce=\"n\", algorithm=SHA-256, qop=\"auth\"", "Digest algorithm SHA-256, qop auth", DisplayName = "SHA-256, qop auth")]
    [DataRow(Digest, "Digest algorithm MD5 (not named), qop none", DisplayName = "No algorithm, no qop")]
    public void CreateAuthorization_Digest_LogsTheAlgorithmAndQopAtVerbose(string challenge, string expected)
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        string? value = Ranked(log, new ScriptedSecurityContextFactory()).CreateAuthorization(HttpRequest(HttpAuthSchemes.Digest), [challenge]);

        CollectionAssert.AreEqual(new[] { expected }, log.At(DiagnosticLogLevel.Verbose));
        AssertNoSecret(log, value!);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_NegotiateMakesNoToken_LogsAWarning()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.NoCredentials, [])));

        string? value = await Ranked(log, contexts).CreateAuthorizationAsync(HttpRequest(HttpAuthSchemes.Negotiate), [], CancellationToken.None);

        Assert.IsNull(value);
        CollectionAssert.AreEqual(new[] { "Negotiate context made no token; no Negotiate answer sent" }, log.At(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_NtlmRounds_LogsEachRoundAtVerboseWithoutTheMessages()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        ScriptedSecurityContextFactory contexts = new(
            new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Type1)),
            new ScriptedSecurityContext(
                new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Type1),
                new SecurityContextStep(SecurityContextStatus.Completed, Type3)));
        NtlmHttpAuthenticator ntlm = new(contexts, refusedChallengeFailsTransfer: false, log);

        string? type1 = await ntlm.CreateAuthorizationAsync(HttpRequest(HttpAuthSchemes.Ntlm), null, sentBeforeAnyChallenge: false, [], CancellationToken.None);
        string? type3 = await ntlm.CreateAuthorizationAsync(HttpRequest(HttpAuthSchemes.Ntlm), type1, sentBeforeAnyChallenge: true, [Type2Challenge], CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "NTLM type 1 sent", "NTLM type 2 received, type 3 sent" }, log.At(DiagnosticLogLevel.Verbose));
        AssertNoSecret(log, type1!["NTLM ".Length..]);
        AssertNoSecret(log, type3!["NTLM ".Length..]);
        AssertNoSecret(log, HandBuiltNtlmSecurityContextTests.MeasuredChallenge);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_ServerChallengesTheType3Again_LogsNtlmSkipped()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);
        NtlmHttpAuthenticator ntlm = new(new ScriptedSecurityContextFactory(), refusedChallengeFailsTransfer: false, log);

        await ntlm.CreateAuthorizationAsync(HttpRequest(HttpAuthSchemes.Ntlm), "NTLM " + Convert.ToBase64String(Type3), sentBeforeAnyChallenge: false, ["NTLM"], CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "NTLM skipped: the server answered the type 3 message with another challenge" }, log.At(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_NtlmContextMakesNoType1_LogsNtlmSkipped()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.NoCredentials, [])));

        await new NtlmHttpAuthenticator(contexts, refusedChallengeFailsTransfer: false, log)
            .CreateAuthorizationAsync(HttpRequest(HttpAuthSchemes.Ntlm), null, sentBeforeAnyChallenge: false, [], CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "NTLM skipped: the security context made no message (NoCredentials)" }, log.At(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    [DataRow(DiagnosticLogLevel.Verbose, DisplayName = "verbose")]
    [DataRow(DiagnosticLogLevel.Error, DisplayName = "error")]
    public async Task CreateAuthorizationAsync_RefusedNtlmLogin_LogsTheFailureAtErrorAndNothingMoreAtError(DiagnosticLogLevel level)
    {
        RecordingDiagnosticLog log = new(level);
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Type1),
            new SecurityContextStep(SecurityContextStatus.MalformedToken, [])));
        RankedHttpAuthenticator ranked = Ranked(log, contexts, refusedChallengeFailsTransfer: true);

        await Assert.ThrowsExactlyAsync<HttpAuthenticationFailedException>(
            () => ranked.ContinueAuthorizationAsync(HttpRequest(HttpAuthSchemes.Ntlm), "NTLM " + HandBuiltNtlmSecurityContextTests.CurlType1, sentBeforeAnyChallenge: true, [Type2Challenge], CancellationToken.None).AsTask());

        CollectionAssert.AreEqual(new[] { "NTLM authentication failed with exit 94 (AuthError): An authentication function returned an error" }, log.At(DiagnosticLogLevel.Error));
        Assert.AreEqual(level == DiagnosticLogLevel.Error, log.Lines.TrueForAll(line => line.Level == DiagnosticLogLevel.Error));
        AssertNoSecret(log, HandBuiltNtlmSecurityContextTests.MeasuredChallenge);
    }

    [TestMethod]
    public async Task ChooseMechanism_ServerOffersSeveral_LogsThePickAtInfoAndPlainWithoutThePassword()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        SaslAuthenticator sasl = new(Encoding.UTF8, () => "c", answerDigestMd5AsSspi: false, securityContexts: null, log);
        SaslRequest request = SaslRequestFor(new NetworkCredential("u", Secret));

        string? mechanism = sasl.ChooseMechanism(request, ["LOGIN", "PLAIN"]);
        byte[]? initialResponse = await sasl.Begin(mechanism!, request).GetInitialResponseAsync(CancellationToken.None);

        Assert.AreEqual("PLAIN", mechanism);
        CollectionAssert.AreEqual(new[] { "server offered SASL LOGIN PLAIN; chose PLAIN" }, log.At(DiagnosticLogLevel.Info));
        CollectionAssert.AreEqual(new[] { "SASL PLAIN exchange begun" }, log.At(DiagnosticLogLevel.Verbose));
        AssertNoSecret(log, Convert.ToBase64String(initialResponse!));
    }

    [TestMethod]
    public void ChooseMechanism_NoneUsable_LogsAWarning()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);
        SaslAuthenticator sasl = new(Encoding.UTF8, () => "c", answerDigestMd5AsSspi: false, securityContexts: null, log);

        Assert.IsNull(sasl.ChooseMechanism(SaslRequestFor(new NetworkCredential("u", Secret)), ["SCRAM-SHA-1"]));

        CollectionAssert.AreEqual(new[] { "server offered SASL SCRAM-SHA-1; none can be used" }, log.At(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task Begin_DigestMd5ChallengeSspiRejects_LogsTheFailureAtError()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Error);
        ISaslExchange exchange = new SaslAuthenticator(Encoding.UTF8, () => "c", answerDigestMd5AsSspi: true, securityContexts: new ScriptedSecurityContextFactory(), log)
            .Begin("DIGEST-MD5", SaslRequestFor(new NetworkCredential("u", Secret)));

        await Assert.ThrowsExactlyAsync<SaslAuthenticationFailedException>(
            () => exchange.RespondAsync("realm=\"localhost\",qop=\"auth\",algorithm=md5-sess"u8.ToArray(), CancellationToken.None).AsTask());

        CollectionAssert.AreEqual(new[] { "DIGEST-MD5 authentication failed with exit 94 (AuthError): An authentication function returned an error" }, log.At(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    [DataRow(true, SecurityMechanism.Ntlm, "Ntlm context for server.example.test: system (SSPI) (Windows, as curl's Schannel build uses SSPI)", DisplayName = "Windows")]
    [DataRow(false, SecurityMechanism.Ntlm, "Ntlm context for server.example.test: hand-built (curl's own NTLM off Windows)", DisplayName = "NTLM off Windows")]
    [DataRow(false, SecurityMechanism.Negotiate, "Negotiate context for server.example.test: system GSS-API, else hand-built (off Windows the default credentials, falling back when GSS-API is unsupported)", DisplayName = "Negotiate off Windows")]
    public void Create_Routes_LogsTheRouteAndWhyAtVerboseWithoutTheCredential(bool isWindows, SecurityMechanism mechanism, string expected)
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        SecurityContextRequest request = new(mechanism, "HTTP", "server.example.test") { UserName = "alice", Password = Secret, Domain = "EXAMPLE" };
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [1]));

        using ISecurityContext created = new RoutingSecurityContextFactory(isWindows, new ScriptedSecurityContextFactory(context), new ScriptedSecurityContextFactory(context), log).Create(request);

        CollectionAssert.AreEqual(new[] { expected }, log.At(DiagnosticLogLevel.Verbose));
        AssertNoSecret(log, "alice");
    }

    private static void AssertNoSecret(RecordingDiagnosticLog log, string forbidden)
    {
        foreach ((_, _, string message) in log.Lines)
        {
            Assert.DoesNotContain(Secret, message);
            Assert.DoesNotContain(forbidden, message);
        }
    }

    private static RankedHttpAuthenticator Ranked(IDiagnosticLog log, ScriptedSecurityContextFactory contexts, bool refusedChallengeFailsTransfer = false) => new(
        new BasicAndBearerAuthenticator(Encoding.UTF8),
        new DigestAuthenticator(Encoding.UTF8, () => "c", log),
        new NegotiateHttpAuthenticator(contexts),
        new NtlmHttpAuthenticator(contexts, refusedChallengeFailsTransfer, log),
        log);

    private static HttpAuthRequest HttpRequest(HttpAuthSchemes allowed) =>
        new("GET", CurlUrl.Parse("http://127.0.0.1:18923/a"), "/a", new NetworkCredential("u", Secret), null, allowed, IsProxy: false);

    private static SaslRequest SaslRequestFor(NetworkCredential credential) =>
        new(credential, null, null, null, "smtp", "127.0.0.1");
}
