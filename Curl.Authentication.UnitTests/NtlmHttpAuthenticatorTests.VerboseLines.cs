using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <content>
/// Pins the <c>-v</c> lines <see cref="NtlmHttpAuthenticator" /> reports to
/// <see cref="HttpAuthRequest.Events" />, as curl 8.21.0 (Windows, SSPI) and curl 8.18.0
/// (Ubuntu, its own NTLM) wrote them for <c>--ntlm -u u:p -v</c> on 2026-09-30 (BL-848 Notes).
/// </content>
public sealed partial class NtlmHttpAuthenticatorTests
{
    private const string SentType1 = "NTLM " + HandBuiltNtlmSecurityContextTests.CurlType1;

    private const string SentType3 = "NTLM " + HandBuiltNtlmSecurityContextTests.MeasuredType3;

    [TestMethod]
    [DataRow(false, DisplayName = "curl's own NTLM")]
    [DataRow(true, DisplayName = "SSPI")]
    public async Task CreateAuthorizationAsync_BareNtlmAfterType3_ReportsHandshakeRejectedThenProblem(bool matchesSspiBuild)
    {
        List<string> lines = await LinesAsync(new ScriptedSecurityContextFactory(), matchesSspiBuild, SentType3, sentBeforeAnyChallenge: false, "NTLM");

        CollectionAssert.AreEqual(new[] { "NTLM handshake rejected", "NTLM authentication problem, ignoring." }, lines);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_BareNtlmAfterType1AnsweringAChallenge_ReportsInternalErrorThenProblem()
    {
        List<string> lines = await LinesAsync(new ScriptedSecurityContextFactory(), matchesSspiBuild: true, SentType1, sentBeforeAnyChallenge: false, "NTLM");

        CollectionAssert.AreEqual(new[] { "NTLM handshake failure (internal error)", "NTLM authentication problem, ignoring." }, lines);
    }

    [TestMethod]
    [DataRow("NTLM " + HandBuiltNtlmSecurityContextTests.MeasuredChallenge, DisplayName = "Another Type 2 after Type 3")]
    [DataRow("Basic realm=\"r\"", DisplayName = "No NTLM challenge after Type 3")]
    public async Task CreateAuthorizationAsync_NonBareChallengeAfterType3_ReportsNothing(string challenge)
    {
        List<string> lines = await LinesAsync(new ScriptedSecurityContextFactory(), matchesSspiBuild: false, SentType3, sentBeforeAnyChallenge: false, challenge);

        Assert.IsEmpty(lines);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_NoNtlmChallengeAfterType1AnsweringAChallenge_ReportsNothing()
    {
        List<string> lines = await LinesAsync(new ScriptedSecurityContextFactory(), matchesSspiBuild: false, SentType1, sentBeforeAnyChallenge: false, "Basic realm=\"r\"");

        Assert.IsEmpty(lines);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_BareNtlmToType1SentBeforeAnyChallenge_ReportsNothing()
    {
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Type1)));

        List<string> lines = await LinesAsync(contexts, matchesSspiBuild: false, SentType1, sentBeforeAnyChallenge: true, "NTLM");

        Assert.IsEmpty(lines);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "curl's own NTLM")]
    [DataRow(true, DisplayName = "SSPI")]
    public async Task CreateAuthorizationAsync_ChallengeNotBase64_ReportsProblemAlone(bool matchesSspiBuild)
    {
        List<string> lines = await LinesAsync(new ScriptedSecurityContextFactory(), matchesSspiBuild, SentType1, sentBeforeAnyChallenge: true, "NTLM @@@notbase64");

        CollectionAssert.AreEqual(new[] { "NTLM authentication problem, ignoring." }, lines);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_Type2CurlsOwnNtlmCannotRead_ReportsBadType2ThenProblem()
    {
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Type1),
            new SecurityContextStep(SecurityContextStatus.MalformedToken, [])));

        List<string> lines = await LinesAsync(contexts, matchesSspiBuild: false, SentType1, sentBeforeAnyChallenge: true, Type2Challenge);

        CollectionAssert.AreEqual(new[] { "NTLM handshake failure (bad type-2 message)", "NTLM authentication problem, ignoring." }, lines);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_Type2Answered_ReportsNothing()
    {
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Type1),
            new SecurityContextStep(SecurityContextStatus.Completed, [1, 2, 3])));

        List<string> lines = await LinesAsync(contexts, matchesSspiBuild: true, SentType1, sentBeforeAnyChallenge: true, Type2Challenge);

        Assert.IsEmpty(lines);
    }

    /// <summary>
    /// Measured on Windows: a Type 2 cut short (<c>NTLM TlRMTVNTUAACAAAA</c>) drew
    /// <c>* NTLM handshake failure (type-3 message): Status=0x80090308</c>, an empty line, and
    /// <c>curl: (94) An authentication function returned an error</c>. The other statuses are the
    /// SSPI codes each failure maps to.
    /// </summary>
    [TestMethod]
    [DataRow(SecurityContextStatus.MalformedToken, "0x80090308", DisplayName = "SEC_E_INVALID_TOKEN, measured")]
    [DataRow(SecurityContextStatus.NoCredentials, "0x8009030e", DisplayName = "SEC_E_NO_CREDENTIALS")]
    [DataRow(SecurityContextStatus.NoMechanism, "0x80090305", DisplayName = "SEC_E_SECPKG_NOT_FOUND")]
    [DataRow(SecurityContextStatus.Refused, "0x8009030c", DisplayName = "SEC_E_LOGON_DENIED")]
    public async Task CreateAuthorizationAsync_SspiRefusesType2_ReportsType3FailureBeforeExit94(SecurityContextStatus status, string code)
    {
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Type1),
            new SecurityContextStep(status, [])));
        RecordingInfoEvents events = new();
        NtlmHttpAuthenticator authenticator = new(contexts, matchesSspiBuild: true);

        HttpAuthenticationFailedException failure = await Assert.ThrowsExactlyAsync<HttpAuthenticationFailedException>(
            () => authenticator.CreateAuthorizationAsync(Request(events), SentType1, sentBeforeAnyChallenge: true, [Type2Challenge], CancellationToken.None).AsTask());

        Assert.AreEqual(CurlExitCode.AuthError, failure.ExitCode);
        CollectionAssert.AreEqual(new[] { $"NTLM handshake failure (type-3 message): Status={code}\n" }, events.Info);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_Type3TooLargeForCurlsOwnNtlm_ReportsNothingBeforeExit100()
    {
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Type1),
            new SecurityContextStep(SecurityContextStatus.Refused, [])));
        RecordingInfoEvents events = new();
        NtlmHttpAuthenticator authenticator = new(contexts, matchesSspiBuild: false);

        await Assert.ThrowsExactlyAsync<HttpAuthenticationFailedException>(
            () => authenticator.CreateAuthorizationAsync(Request(events), SentType1, sentBeforeAnyChallenge: true, [Type2Challenge], CancellationToken.None).AsTask());

        Assert.IsEmpty(events.Info);
    }

    private static async Task<List<string>> LinesAsync(ISecurityContextFactory contexts, bool matchesSspiBuild, string sent, bool sentBeforeAnyChallenge, string challenge)
    {
        RecordingInfoEvents events = new();
        await new NtlmHttpAuthenticator(contexts, matchesSspiBuild).CreateAuthorizationAsync(Request(events), sent, sentBeforeAnyChallenge, [challenge], CancellationToken.None);
        return events.Info;
    }

    private static HttpAuthRequest Request(ITransferEvents events) =>
        new("GET", CurlUrl.Parse("http://127.0.0.1:18526/x"), "/x", new NetworkCredential("u", "p"), null, HttpAuthSchemes.Ntlm, IsProxy: false) { Events = events };
}
