using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Pins curl 8.21.0's answer to a <c>Negotiate</c> challenge whose token starts with <c>=</c>
/// (BL-1303): <c>Curl_auth_decode_spnego_message</c> never decodes such a token, so on the SSPI
/// and the GSS-API build alike it writes <c>SPNEGO handshake failure (empty challenge
/// message)</c> and steps no context, and the 401 is the result.
/// </summary>
[TestClass]
public sealed class NegotiateEmptyChallengeMessageTests
{
    [TestMethod]
    [DataRow(true, DisplayName = "SSPI wording")]
    [DataRow(false, DisplayName = "GSS-API wording")]
    public async Task MeasuredExchange_FirstLegHadNoCredentialsThen401WithEquals_ReportsTheLineOnceAndStepsNoContext(bool wordsAsSspi)
    {
        ScriptedSecurityContextFactory contexts = new(
            new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.NoCredentials, [])),
            new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.NoCredentials, [])));
        RecordingInfoEvents events = new();
        RankedHttpAuthenticator authenticator = Ranked(contexts, wordsAsSspi);
        HttpAuthRequest request = NegotiateRequest() with { Events = events };

        string? first = await authenticator.CreateAuthorizationAsync(request, [], CancellationToken.None);
        string? further = await authenticator.ContinueAuthorizationAsync(request, string.Empty, sentBeforeAnyChallenge: true, ["Negotiate ="], CancellationToken.None);

        Assert.IsNull(first);
        Assert.IsNull(further);
        Assert.HasCount(1, contexts.Requests);
        CollectionAssert.AreEqual(
            new[] { NegotiateFailureLines.For(SecurityContextStatus.NoCredentials, wordsAsSspi), NegotiateHttpAuthenticator.EmptyChallengeMessageLine },
            events.Info);
        Assert.AreEqual("SPNEGO handshake failure (empty challenge message)", events.Info[1]);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "SSPI wording")]
    [DataRow(false, DisplayName = "GSS-API wording")]
    public async Task ContinueAuthorizationAsync_FirstLegSucceededThen401WithEqualsAbc_ReportsTheLineAndDisposesTheContextUnstepped(bool wordsAsSspi)
    {
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]));
        RecordingInfoEvents events = new();
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(context), wordsFailuresAsSspi: wordsAsSspi);
        HttpAuthRequest request = NegotiateRequest() with { Events = events };
        string? sent = await authenticator.CreateAuthorizationAsync(request, CancellationToken.None);

        string? value = await authenticator.ContinueAuthorizationAsync(request, sent!, ["Negotiate =abc"], CancellationToken.None);

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
        ScriptedSecurityContextFactory contexts = new();
        RecordingInfoEvents events = new();

        await new NegotiateHttpAuthenticator(contexts, wordsFailuresAsSspi: wordsAsSspi).StepWithoutAnsweringAsync(NegotiateRequest() with { Events = events }, ["Negotiate ="], CancellationToken.None);

        Assert.IsEmpty(contexts.Requests);
        CollectionAssert.AreEqual(new[] { NegotiateHttpAuthenticator.EmptyChallengeMessageLine }, events.Info);
    }

    [TestMethod]
    [DataRow("Negotiate", DisplayName = "Empty Negotiate challenge")]
    [DataRow("Basic realm=\"r\"", DisplayName = "No Negotiate challenge")]
    public async Task StepWithoutAnsweringAsync_NoTokenInTheChallenge_StepsAContextAsBefore(string challenge)
    {
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.NoCredentials, []));
        RecordingInfoEvents events = new();

        await new NegotiateHttpAuthenticator(new ScriptedSecurityContextFactory(context), wordsFailuresAsSspi: true).StepWithoutAnsweringAsync(NegotiateRequest() with { Events = events }, [challenge], CancellationToken.None);

        Assert.HasCount(1, context.IncomingTokens);
        CollectionAssert.AreEqual(new[] { NegotiateFailureLines.For(SecurityContextStatus.NoCredentials, wordsAsSspi: true) }, events.Info);
    }

    [TestMethod]
    [DataRow("Negotiate", DisplayName = "Empty Negotiate challenge")]
    [DataRow("Negotiate @@@", DisplayName = "Undecodable token not starting with =")]
    [DataRow("Negotiate a=bc", DisplayName = "= not first")]
    public async Task ContinueAuthorizationAsync_TokenNotStartingWithEquals_EndsOnThe401WithoutTheLine(string challenge)
    {
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]));
        RecordingInfoEvents events = new();
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(context));
        HttpAuthRequest request = NegotiateRequest() with { Events = events };
        string? sent = await authenticator.CreateAuthorizationAsync(request, CancellationToken.None);

        string? value = await authenticator.ContinueAuthorizationAsync(request, sent!, [challenge], CancellationToken.None);

        Assert.IsNull(value);
        Assert.IsTrue(context.IsDisposed);
        Assert.HasCount(1, context.IncomingTokens);
        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    public async Task StepWithoutAnsweringAsync_NullChallenges_Throws()
    {
        NegotiateHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => authenticator.StepWithoutAnsweringAsync(NegotiateRequest(), null!, CancellationToken.None).AsTask());
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
