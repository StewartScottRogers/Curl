using System.Net;
using System.Text;
using Curl.Authentication.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void CreateAuthorization_DigestAndBasicOffered_LogsDigestChosenAtInfo()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenges", Basic + " | " + Digest);
        diagnostics.Arrange("allowed", HttpAuthSchemes.Any);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);

        string? value = Ranked(log, new ScriptedSecurityContextFactory()).CreateAuthorization(HttpRequest(HttpAuthSchemes.Any), [Basic, Digest]);

        diagnostics.Act("authorization", value);
        diagnostics.Act("log", Joined(log));
        diagnostics.Diff("info line", "server offered Basic, Digest; allowed Any; chose Digest", string.Join(" | ", log.At(DiagnosticLogLevel.Info)));
        CollectionAssert.AreEqual(new[] { "server offered Basic, Digest; allowed Any; chose Digest" }, log.At(DiagnosticLogLevel.Info));
        Assert.AreEqual(DiagnosticLogComponents.Auth, log.Lines[0].Component);
        AssertNoSecret(log, value!);
    }

    [TestMethod]
    public void CreateAuthorization_BasicChosen_LogsNoPasswordAndNoBase64Credential()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge", Basic);
        diagnostics.Arrange("password", Secret);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        string? value = Ranked(log, new ScriptedSecurityContextFactory()).CreateAuthorization(HttpRequest(HttpAuthSchemes.Basic), [Basic]);

        diagnostics.Act("authorization", value);
        diagnostics.Act("log", Joined(log));
        diagnostics.Diff("info line", "server offered Basic; allowed Basic; chose Basic", string.Join(" | ", log.At(DiagnosticLogLevel.Info)));
        CollectionAssert.AreEqual(new[] { "server offered Basic; allowed Basic; chose Basic" }, log.At(DiagnosticLogLevel.Info));
        AssertNoSecret(log, value!);
        AssertNoSecret(log, Convert.ToBase64String(Encoding.UTF8.GetBytes("u:" + Secret)));
    }

    [TestMethod]
    public void CreateAuthorization_NoOfferedSchemeAllowed_LogsAWarning()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge", Digest);
        diagnostics.Arrange("allowed", HttpAuthSchemes.Basic);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        string? value = Ranked(log, new ScriptedSecurityContextFactory()).CreateAuthorization(HttpRequest(HttpAuthSchemes.Basic), [Digest]);

        diagnostics.Act("authorization", value);
        diagnostics.Act("log", Joined(log));
        diagnostics.Diff("warning line", "server offered Digest; none of the allowed Basic can answer", string.Join(" | ", log.At(DiagnosticLogLevel.Warning)));
        CollectionAssert.AreEqual(new[] { "server offered Digest; none of the allowed Basic can answer" }, log.At(DiagnosticLogLevel.Warning));
        Assert.IsEmpty(log.At(DiagnosticLogLevel.Info));
    }

    [TestMethod]
    public void CreateAuthorization_BeforeAnyChallenge_LogsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenges", "(none)");
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        string? value = Ranked(log, new ScriptedSecurityContextFactory()).CreateAuthorization(HttpRequest(HttpAuthSchemes.Basic), []);

        diagnostics.Act("authorization", value);
        diagnostics.Act("log", Joined(log));
        diagnostics.Assert("line count", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    [DataRow("Digest realm=\"r\", nonce=\"n\", algorithm=SHA-256, qop=\"auth\"", "Digest algorithm SHA-256, qop auth", DisplayName = "SHA-256, qop auth")]
    [DataRow(Digest, "Digest algorithm MD5 (not named), qop none", DisplayName = "No algorithm, no qop")]
    public void CreateAuthorization_Digest_LogsTheAlgorithmAndQopAtVerbose(string challenge, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge", challenge);
        diagnostics.Arrange("expected", expected);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);

        string? value = Ranked(log, new ScriptedSecurityContextFactory()).CreateAuthorization(HttpRequest(HttpAuthSchemes.Digest), [challenge]);

        diagnostics.Act("authorization", value);
        diagnostics.Act("log", Joined(log));
        diagnostics.Diff("verbose line", expected, string.Join(" | ", log.At(DiagnosticLogLevel.Verbose)));
        CollectionAssert.AreEqual(new[] { expected }, log.At(DiagnosticLogLevel.Verbose));
        AssertNoSecret(log, value!);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_NegotiateMakesNoToken_LogsAWarning()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("context status", SecurityContextStatus.NoCredentials);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.NoCredentials, [])));

        string? value = await Ranked(log, contexts).CreateAuthorizationAsync(HttpRequest(HttpAuthSchemes.Negotiate), [], CancellationToken.None);

        diagnostics.Act("authorization", value ?? "(null)");
        diagnostics.Act("log", Joined(log));
        diagnostics.Diff("warning line", "Negotiate context made no token; no Negotiate answer sent", string.Join(" | ", log.At(DiagnosticLogLevel.Warning)));
        Assert.IsNull(value);
        CollectionAssert.AreEqual(new[] { "Negotiate context made no token; no Negotiate answer sent" }, log.At(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_NtlmRounds_LogsEachRoundAtVerboseWithoutTheMessages()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("type 2 challenge", Type2Challenge);
        diagnostics.Bytes("type 1", Type1);
        diagnostics.Bytes("type 3", Type3);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        ScriptedSecurityContextFactory contexts = new(
            new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Type1)),
            new ScriptedSecurityContext(
                new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Type1),
                new SecurityContextStep(SecurityContextStatus.Completed, Type3)));
        NtlmHttpAuthenticator ntlm = new(contexts, matchesSspiBuild: false, log);

        string? type1 = await ntlm.CreateAuthorizationAsync(HttpRequest(HttpAuthSchemes.Ntlm), null, sentBeforeAnyChallenge: false, [], CancellationToken.None);
        string? type3 = await ntlm.CreateAuthorizationAsync(HttpRequest(HttpAuthSchemes.Ntlm), type1, sentBeforeAnyChallenge: true, [Type2Challenge], CancellationToken.None);

        diagnostics.Act("type 1 header", type1);
        diagnostics.Act("type 3 header", type3);
        diagnostics.Act("log", Joined(log));
        diagnostics.Diff("verbose lines", "NTLM type 1 sent | NTLM type 2 received, type 3 sent", string.Join(" | ", log.At(DiagnosticLogLevel.Verbose)));
        CollectionAssert.AreEqual(new[] { "NTLM type 1 sent", "NTLM type 2 received, type 3 sent" }, log.At(DiagnosticLogLevel.Verbose));
        AssertNoSecret(log, type1!["NTLM ".Length..]);
        AssertNoSecret(log, type3!["NTLM ".Length..]);
        AssertNoSecret(log, HandBuiltNtlmSecurityContextTests.MeasuredChallenge);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_ServerChallengesTheType3Again_LogsNtlmSkipped()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge", "NTLM");
        diagnostics.Arrange("sent", "type 3");
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);
        NtlmHttpAuthenticator ntlm = new(new ScriptedSecurityContextFactory(), matchesSspiBuild: false, log);

        string? value = await ntlm.CreateAuthorizationAsync(HttpRequest(HttpAuthSchemes.Ntlm), "NTLM " + Convert.ToBase64String(Type3), sentBeforeAnyChallenge: false, ["NTLM"], CancellationToken.None);

        diagnostics.Act("authorization", value ?? "(null)");
        diagnostics.Act("log", Joined(log));
        diagnostics.Diff("warning line", "NTLM skipped: the server answered the type 3 message with another challenge", string.Join(" | ", log.At(DiagnosticLogLevel.Warning)));
        CollectionAssert.AreEqual(new[] { "NTLM skipped: the server answered the type 3 message with another challenge" }, log.At(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_NtlmContextMakesNoType1_LogsNtlmSkipped()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("context status", SecurityContextStatus.NoCredentials);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.NoCredentials, [])));

        string? value = await new NtlmHttpAuthenticator(contexts, matchesSspiBuild: false, log)
            .CreateAuthorizationAsync(HttpRequest(HttpAuthSchemes.Ntlm), null, sentBeforeAnyChallenge: false, [], CancellationToken.None);

        diagnostics.Act("authorization", value ?? "(null)");
        diagnostics.Act("log", Joined(log));
        diagnostics.Diff("warning line", "NTLM skipped: the security context made no message (NoCredentials)", string.Join(" | ", log.At(DiagnosticLogLevel.Warning)));
        CollectionAssert.AreEqual(new[] { "NTLM skipped: the security context made no message (NoCredentials)" }, log.At(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    [DataRow(DiagnosticLogLevel.Verbose, DisplayName = "verbose")]
    [DataRow(DiagnosticLogLevel.Error, DisplayName = "error")]
    public async Task CreateAuthorizationAsync_RefusedNtlmLogin_LogsTheFailureAtErrorAndNothingMoreAtError(DiagnosticLogLevel level)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("level", level);
        diagnostics.Arrange("type 2 challenge", Type2Challenge);
        RecordingDiagnosticLog log = new(level);
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Type1),
            new SecurityContextStep(SecurityContextStatus.MalformedToken, [])));
        RankedHttpAuthenticator ranked = Ranked(log, contexts, matchesSspiBuild: true);

        HttpAuthenticationFailedException exception = await Assert.ThrowsExactlyAsync<HttpAuthenticationFailedException>(
            () => ranked.ContinueAuthorizationAsync(HttpRequest(HttpAuthSchemes.Ntlm), "NTLM " + HandBuiltNtlmSecurityContextTests.CurlType1, sentBeforeAnyChallenge: true, [Type2Challenge], CancellationToken.None).AsTask());

        diagnostics.Act("exception", exception.Message);
        diagnostics.Act("log", Joined(log));
        diagnostics.Diff("error line", "NTLM authentication failed with exit 94 (AuthError): An authentication function returned an error", string.Join(" | ", log.At(DiagnosticLogLevel.Error)));
        CollectionAssert.AreEqual(new[] { "NTLM authentication failed with exit 94 (AuthError): An authentication function returned an error" }, log.At(DiagnosticLogLevel.Error));
        Assert.AreEqual(level == DiagnosticLogLevel.Error, log.Lines.TrueForAll(line => line.Level == DiagnosticLogLevel.Error));
        AssertNoSecret(log, HandBuiltNtlmSecurityContextTests.MeasuredChallenge);
    }

    [TestMethod]
    public async Task ChooseMechanism_ServerOffersSeveral_LogsThePickAtInfoAndPlainWithoutThePassword()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("offered", "LOGIN PLAIN");
        diagnostics.Arrange("password", Secret);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        SaslAuthenticator sasl = new(Encoding.UTF8, () => "c", answerDigestMd5AsSspi: false, securityContexts: null, log);
        SaslRequest request = SaslRequestFor(new NetworkCredential("u", Secret));

        string? mechanism = sasl.ChooseMechanism(request, ["LOGIN", "PLAIN"]);
        byte[]? initialResponse = await sasl.Begin(mechanism!, request).GetInitialResponseAsync(CancellationToken.None);

        diagnostics.Act("mechanism", mechanism);
        diagnostics.Bytes("initial response", initialResponse);
        diagnostics.Act("log", Joined(log));
        diagnostics.Assert("mechanism", "PLAIN", mechanism);
        Assert.AreEqual("PLAIN", mechanism);
        CollectionAssert.AreEqual(new[] { "server offered SASL LOGIN PLAIN; chose PLAIN" }, log.At(DiagnosticLogLevel.Info));
        CollectionAssert.AreEqual(new[] { "SASL PLAIN exchange begun" }, log.At(DiagnosticLogLevel.Verbose));
        AssertNoSecret(log, Convert.ToBase64String(initialResponse!));
    }

    [TestMethod]
    public void ChooseMechanism_NoneUsable_LogsAWarning()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("offered", "SCRAM-SHA-1");
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);
        SaslAuthenticator sasl = new(Encoding.UTF8, () => "c", answerDigestMd5AsSspi: false, securityContexts: null, log);

        Assert.IsNull(sasl.ChooseMechanism(SaslRequestFor(new NetworkCredential("u", Secret)), ["SCRAM-SHA-1"]));

        diagnostics.Act("log", Joined(log));
        diagnostics.Diff("warning line", "server offered SASL SCRAM-SHA-1; none can be used", string.Join(" | ", log.At(DiagnosticLogLevel.Warning)));
        CollectionAssert.AreEqual(new[] { "server offered SASL SCRAM-SHA-1; none can be used" }, log.At(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task Begin_DigestMd5ChallengeSspiRejects_LogsTheFailureAtError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "DIGEST-MD5");
        diagnostics.Arrange("challenge", "realm=\"localhost\",qop=\"auth\",algorithm=md5-sess");
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Error);
        ISaslExchange exchange = new SaslAuthenticator(Encoding.UTF8, () => "c", answerDigestMd5AsSspi: true, securityContexts: new ScriptedSecurityContextFactory(), log)
            .Begin("DIGEST-MD5", SaslRequestFor(new NetworkCredential("u", Secret)));

        SaslAuthenticationFailedException exception = await Assert.ThrowsExactlyAsync<SaslAuthenticationFailedException>(
            () => exchange.RespondAsync("realm=\"localhost\",qop=\"auth\",algorithm=md5-sess"u8.ToArray(), CancellationToken.None).AsTask());

        diagnostics.Act("exception", exception.Message);
        diagnostics.Act("log", Joined(log));
        diagnostics.Diff("error line", "DIGEST-MD5 authentication failed with exit 94 (AuthError): An authentication function returned an error", string.Join(" | ", log.At(DiagnosticLogLevel.Error)));
        CollectionAssert.AreEqual(new[] { "DIGEST-MD5 authentication failed with exit 94 (AuthError): An authentication function returned an error" }, log.At(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    [DataRow(true, SecurityMechanism.Ntlm, "Ntlm context for server.example.test: system (SSPI) (Windows, as curl's Schannel build uses SSPI)", DisplayName = "Windows")]
    [DataRow(false, SecurityMechanism.Ntlm, "Ntlm context for server.example.test: hand-built (curl's own NTLM off Windows)", DisplayName = "NTLM off Windows")]
    [DataRow(false, SecurityMechanism.Negotiate, "Negotiate context for server.example.test: system GSS-API, else hand-built (off Windows the default credentials, falling back when GSS-API is unsupported)", DisplayName = "Negotiate off Windows")]
    public void Create_Routes_LogsTheRouteAndWhyAtVerboseWithoutTheCredential(bool isWindows, SecurityMechanism mechanism, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("isWindows", isWindows);
        diagnostics.Arrange("mechanism", mechanism);
        diagnostics.Arrange("expected", expected);
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        SecurityContextRequest request = new(mechanism, "HTTP", "server.example.test") { UserName = "alice", Password = Secret, Domain = "EXAMPLE" };
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [1]));

        using ISecurityContext created = new RoutingSecurityContextFactory(isWindows, new ScriptedSecurityContextFactory(context), new ScriptedSecurityContextFactory(context), log).Create(request);

        diagnostics.Act("log", Joined(log));
        diagnostics.Diff("verbose line", expected, string.Join(" | ", log.At(DiagnosticLogLevel.Verbose)));
        CollectionAssert.AreEqual(new[] { expected }, log.At(DiagnosticLogLevel.Verbose));
        AssertNoSecret(log, "alice");
    }

    private static string Joined(RecordingDiagnosticLog log)
    {
        List<string> messages = [];
        foreach ((_, _, string message) in log.Lines)
        {
            messages.Add(message);
        }

        return string.Join(" | ", messages);
    }

    private static void AssertNoSecret(RecordingDiagnosticLog log, string forbidden)
    {
        foreach ((_, _, string message) in log.Lines)
        {
            Assert.DoesNotContain(Secret, message);
            Assert.DoesNotContain(forbidden, message);
        }
    }

    private static RankedHttpAuthenticator Ranked(IDiagnosticLog log, ScriptedSecurityContextFactory contexts, bool matchesSspiBuild = false) => new(
        new BasicAndBearerAuthenticator(Encoding.UTF8),
        new DigestAuthenticator(Encoding.UTF8, () => "c", log),
        new NegotiateHttpAuthenticator(contexts),
        new NtlmHttpAuthenticator(contexts, matchesSspiBuild, log),
        log);

    private static HttpAuthRequest HttpRequest(HttpAuthSchemes allowed) =>
        new("GET", CurlUrl.Parse("http://127.0.0.1:18923/a"), "/a", new NetworkCredential("u", Secret), null, allowed, IsProxy: false);

    private static SaslRequest SaslRequestFor(NetworkCredential credential) =>
        new(credential, null, null, null, "smtp", "127.0.0.1");
}
