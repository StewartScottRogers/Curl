using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins curl 8.21.0's answer to a <c>Negotiate</c> challenge whose token starts with <c>=</c>
/// (BL-1303): <c>Curl_auth_decode_spnego_message</c> never decodes such a token, so on the SSPI
/// and the GSS-API build alike it writes <c>SPNEGO handshake failure (empty challenge
/// message)</c> and steps no context, and the 401 is the result. When Negotiate is picked only
/// after the challenge (<c>--anyauth</c>, BL-1313) there is no context yet, so curl ignores the
/// token: no line, a context stepped, and the request asked for again.
/// </summary>
[TestClass]
public sealed class NegotiateEmptyChallengeMessageTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(true, DisplayName = "SSPI wording")]
    [DataRow(false, DisplayName = "GSS-API wording")]
    public async Task MeasuredExchange_FirstLegHadNoCredentialsThen401WithEquals_ReportsTheLineOnceAndStepsNoContext(bool wordsAsSspi)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("words failures as SSPI", wordsAsSspi);
        diagnostics.Arrange("server challenge", "Negotiate =");
        diagnostics.Arrange("scripted status", SecurityContextStatus.NoCredentials);
        ScriptedSecurityContextFactory contexts = new(
            new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.NoCredentials, [])),
            new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.NoCredentials, [])));
        RecordingInfoEvents events = new();
        RankedHttpAuthenticator authenticator = Ranked(contexts, wordsAsSspi);
        HttpAuthRequest request = NegotiateRequest() with { Events = events };

        string? first = await authenticator.CreateAuthorizationAsync(request, [], CancellationToken.None);
        string? further = await authenticator.ContinueAuthorizationAsync(request, string.Empty, sentBeforeAnyChallenge: true, ["Negotiate ="], CancellationToken.None);

        diagnostics.Act("first Authorization", first);
        diagnostics.Act("further Authorization", further);
        diagnostics.Act("contexts requested", contexts.Requests.Count);
        diagnostics.Act("verbose lines", string.Join(" | ", events.Info));
        diagnostics.Assert("first Authorization", null, first);
        diagnostics.Assert("further Authorization", null, further);
        diagnostics.Assert("contexts requested", 1, contexts.Requests.Count);
        diagnostics.Diff(
            "verbose lines",
            NegotiateFailureLines.For(SecurityContextStatus.NoCredentials, wordsAsSspi) + " | " + NegotiateHttpAuthenticator.EmptyChallengeMessageLine,
            string.Join(" | ", events.Info));
        Assert.IsNull(first);
        Assert.IsNull(further);
        Assert.HasCount(1, contexts.Requests);
        CollectionAssert.AreEqual(
            new[] { NegotiateFailureLines.For(SecurityContextStatus.NoCredentials, wordsAsSspi), NegotiateHttpAuthenticator.EmptyChallengeMessageLine },
            events.Info);
        diagnostics.Diff("second line", "SPNEGO handshake failure (empty challenge message)", events.Info[1]);
        Assert.AreEqual("SPNEGO handshake failure (empty challenge message)", events.Info[1]);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "SSPI wording")]
    [DataRow(false, DisplayName = "GSS-API wording")]
    public async Task ContinueAuthorizationAsync_FirstLegSucceededThen401WithEqualsAbc_ReportsTheLineAndDisposesTheContextUnstepped(bool wordsAsSspi)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("words failures as SSPI", wordsAsSspi);
        diagnostics.Arrange("server challenge", "Negotiate =abc");
        diagnostics.Bytes("scripted token 1", [0x01]);
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]));
        RecordingInfoEvents events = new();
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(context), wordsFailuresAsSspi: wordsAsSspi);
        HttpAuthRequest request = NegotiateRequest() with { Events = events };
        string? sent = await authenticator.CreateAuthorizationAsync(request, CancellationToken.None);

        string? value = await authenticator.ContinueAuthorizationAsync(request, sent!, ["Negotiate =abc"], CancellationToken.None);

        diagnostics.Act("first Authorization", sent);
        diagnostics.Act("second Authorization", value);
        diagnostics.Act("verbose lines", string.Join(" | ", events.Info));
        diagnostics.Assert("second Authorization", null, value);
        diagnostics.Assert("context disposed", true, context.IsDisposed);
        diagnostics.Assert("incoming token count", 1, context.IncomingTokens.Count);
        diagnostics.Diff("verbose lines", NegotiateHttpAuthenticator.EmptyChallengeMessageLine, string.Join(" | ", events.Info));
        Assert.IsNull(value);
        Assert.IsTrue(context.IsDisposed);
        Assert.HasCount(1, context.IncomingTokens);
        CollectionAssert.AreEqual(new[] { NegotiateHttpAuthenticator.EmptyChallengeMessageLine }, events.Info);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "SSPI wording")]
    [DataRow(false, DisplayName = "GSS-API wording")]
    public async Task StepWithoutAnsweringAsync_ChallengeStartsWithEquals_ReportsTheLineAndMakesNoContext(bool wordsAsSspi)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("words failures as SSPI", wordsAsSspi);
        diagnostics.Arrange("server challenge", "Negotiate =");
        ScriptedSecurityContextFactory contexts = new();
        RecordingInfoEvents events = new();

        await new NegotiateHttpAuthenticator(contexts, wordsFailuresAsSspi: wordsAsSspi).StepWithoutAnsweringAsync(NegotiateRequest() with { Events = events }, ["Negotiate ="], CancellationToken.None);

        diagnostics.Act("contexts requested", contexts.Requests.Count);
        diagnostics.Act("verbose lines", string.Join(" | ", events.Info));
        diagnostics.Assert("contexts requested", 0, contexts.Requests.Count);
        diagnostics.Diff("verbose lines", NegotiateHttpAuthenticator.EmptyChallengeMessageLine, string.Join(" | ", events.Info));
        Assert.IsEmpty(contexts.Requests);
        CollectionAssert.AreEqual(new[] { NegotiateHttpAuthenticator.EmptyChallengeMessageLine }, events.Info);
    }

    [TestMethod]
    [DataRow("Negotiate", DisplayName = "Empty Negotiate challenge")]
    [DataRow("Basic realm=\"r\"", DisplayName = "No Negotiate challenge")]
    public async Task StepWithoutAnsweringAsync_NoTokenInTheChallenge_StepsAContextAsBefore(string challenge)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("words failures as SSPI", true);
        diagnostics.Arrange("server challenge", challenge);
        diagnostics.Arrange("scripted status", SecurityContextStatus.NoCredentials);
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.NoCredentials, []));
        RecordingInfoEvents events = new();

        await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context), wordsFailuresAsSspi: true).StepWithoutAnsweringAsync(NegotiateRequest() with { Events = events }, [challenge], CancellationToken.None);

        diagnostics.Act("incoming token count", context.IncomingTokens.Count);
        diagnostics.Act("verbose lines", string.Join(" | ", events.Info));
        diagnostics.Assert("incoming token count", 1, context.IncomingTokens.Count);
        diagnostics.Diff("verbose lines", NegotiateFailureLines.For(SecurityContextStatus.NoCredentials, wordsAsSspi: true), string.Join(" | ", events.Info));
        Assert.HasCount(1, context.IncomingTokens);
        CollectionAssert.AreEqual(new[] { NegotiateFailureLines.For(SecurityContextStatus.NoCredentials, wordsAsSspi: true) }, events.Info);
    }

    [TestMethod]
    [DataRow("Negotiate", DisplayName = "Empty Negotiate challenge")]
    [DataRow("Negotiate @@@", DisplayName = "Undecodable token not starting with =")]
    [DataRow("Negotiate a=bc", DisplayName = "= not first")]
    public async Task ContinueAuthorizationAsync_TokenNotStartingWithEquals_EndsOnThe401WithoutTheLine(string challenge)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("server challenge", challenge);
        diagnostics.Bytes("scripted token 1", [0x01]);
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]));
        RecordingInfoEvents events = new();
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(context));
        HttpAuthRequest request = NegotiateRequest() with { Events = events };
        string? sent = await authenticator.CreateAuthorizationAsync(request, CancellationToken.None);

        string? value = await authenticator.ContinueAuthorizationAsync(request, sent!, [challenge], CancellationToken.None);

        diagnostics.Act("first Authorization", sent);
        diagnostics.Act("second Authorization", value);
        diagnostics.Act("verbose lines", string.Join(" | ", events.Info));
        diagnostics.Assert("second Authorization", null, value);
        diagnostics.Assert("context disposed", true, context.IsDisposed);
        diagnostics.Assert("incoming token count", 1, context.IncomingTokens.Count);
        diagnostics.Assert("verbose line count", 0, events.Info.Count);
        Assert.IsNull(value);
        Assert.IsTrue(context.IsDisposed);
        Assert.HasCount(1, context.IncomingTokens);
        Assert.IsEmpty(events.Info);
    }

    // curl -sv --anyauth -u : (and --negotiate --basic -u :) against "WWW-Authenticate: Negotiate =",
    // measured on curl 8.21.0 Schannel (BL-1313 Notes): with no context yet, curl ignores the
    // token, steps a context, reports its failure and asks for the request again without a header.
    [TestMethod]
    [DataRow(HttpAuthSchemes.Any, true, DisplayName = "--anyauth, SSPI wording")]
    [DataRow(HttpAuthSchemes.Any, false, DisplayName = "--anyauth, GSS-API wording")]
    [DataRow(HttpAuthSchemes.Negotiate | HttpAuthSchemes.Basic, true, DisplayName = "--negotiate --basic")]
    public async Task CreateAuthorizationAsync_NegotiatePickedAfterA401WithEquals_StepsAContextWithoutTheLineAndAsksAgain(HttpAuthSchemes allowed, bool wordsAsSspi)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("allowed schemes", allowed);
        diagnostics.Arrange("words failures as SSPI", wordsAsSspi);
        diagnostics.Arrange("server challenge", "Negotiate =");
        diagnostics.Arrange("scripted status", SecurityContextStatus.NoCredentials);
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.NoCredentials, []));
        ScriptedSecurityContextFactory contexts = new(context);
        RecordingInfoEvents events = new();
        HttpAuthRequest request = NegotiateRequest() with { AllowedSchemes = allowed, Events = events };

        string? value = await Ranked(contexts, wordsAsSspi).CreateAuthorizationAsync(request, ["Negotiate ="], CancellationToken.None);

        diagnostics.Act("Authorization", value);
        diagnostics.Act("contexts requested", contexts.Requests.Count);
        diagnostics.Act("verbose lines", string.Join(" | ", events.Info));
        diagnostics.Diff("Authorization", string.Empty, value ?? "(null)");
        diagnostics.Assert("contexts requested", 1, contexts.Requests.Count);
        diagnostics.Assert("incoming token count", 1, context.IncomingTokens.Count);
        diagnostics.Diff("verbose lines", NegotiateFailureLines.For(SecurityContextStatus.NoCredentials, wordsAsSspi), string.Join(" | ", events.Info));
        Assert.AreEqual(string.Empty, value);
        Assert.HasCount(1, contexts.Requests);
        Assert.HasCount(1, context.IncomingTokens);
        CollectionAssert.AreEqual(new[] { NegotiateFailureLines.For(SecurityContextStatus.NoCredentials, wordsAsSspi) }, events.Info);
        CollectionAssert.DoesNotContain(events.Info, NegotiateHttpAuthenticator.EmptyChallengeMessageLine);
    }

    [TestMethod]
    public async Task StepWithoutAnsweringAsync_NullChallenges_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("server challenges", "null");
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory());

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => authenticator.StepWithoutAnsweringAsync(NegotiateRequest(), null!, CancellationToken.None).AsTask());

        diagnostics.Act("exception", exception.Message);
        diagnostics.Assert("exception type", typeof(ArgumentNullException), exception.GetType());
    }

    private static RankedHttpAuthenticator Ranked(ScriptedSecurityContextFactory contexts, bool wordsAsSspi) => new(
        new BasicAndBearerAuthenticator(Encoding.UTF8),
        new DigestAuthenticator(Encoding.UTF8, () => "c"),
        new NegotiateHttpAuthenticator(contexts, wordsFailuresAsSspi: wordsAsSspi),
        new NtlmHttpAuthenticator(contexts, matchesSspiBuild: false));

    // curl -sv --negotiate -u : http://127.0.0.1:PORT/, as measured (BL-1303 Context).
    private static HttpAuthRequest NegotiateRequest() =>
        new("GET", CurlUrl.Parse("http://127.0.0.1:18218/"), "/", new NetworkCredential(string.Empty, string.Empty), null, HttpAuthSchemes.Negotiate, IsProxy: false);
}
