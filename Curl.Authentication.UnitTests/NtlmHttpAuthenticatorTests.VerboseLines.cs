using System.Net;
using Curl.Ntlm;
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

    /// <summary>
    /// Pins curl 8.21.0's own NTLM from its source, <c>lib/vauth/ntlm.c</c> at <c>curl-8_21_0</c>
    /// lines 256-288 and 363-378 (BL-1226): target information past the message end or starting
    /// inside the 48-byte header draws the target info line before the bad type-2 line. Not
    /// measured: the Windows curl reads Type 2 in SSPI, which never writes the first line.
    /// </summary>
    [TestMethod]
    [DataRow(48, 16, 48, DisplayName = "Target info runs past the message end")]
    [DataRow(40, 8, 56, DisplayName = "Target info offset inside the header")]
    public async Task CreateAuthorizationAsync_Type2TargetInfoOutOfRange_ReportsTargetInfoThenBadType2(int offset, int length, int messageLength)
    {
        List<string> lines = await LinesAsync(new HandBuiltNtlmContexts(), matchesSspiBuild: false, SentType1, sentBeforeAnyChallenge: true, ChallengeWithTargetInformationAt(offset, length, messageLength));

        CollectionAssert.AreEqual(
            new[]
            {
                "NTLM handshake failure (bad type-2 message). Target Info Offset Len is set incorrect by the peer",
                "NTLM handshake failure (bad type-2 message)",
                "NTLM authentication problem, ignoring.",
            },
            lines);
    }

    /// <summary>
    /// Pins curl 8.21.0's <c>Curl_auth_decode_ntlm_type2_message</c> (lines 363-368): a Type 2
    /// shorter than 32 bytes or without the signature and type draws only the bad type-2 line.
    /// </summary>
    [TestMethod]
    [DataRow(true, DisplayName = "Shorter than 32 bytes")]
    [DataRow(false, DisplayName = "Wrong signature")]
    public async Task CreateAuthorizationAsync_Type2TooShortOrWrongSignature_ReportsBadType2Alone(bool tooShort)
    {
        byte[] measured = Convert.FromBase64String(HandBuiltNtlmSecurityContextTests.MeasuredChallenge);
        byte[] challenge = tooShort ? measured[..(NtlmChallengeMessage.MinimumLength - 1)] : measured;
        challenge[0] = tooShort ? challenge[0] : (byte)'X';

        List<string> lines = await LinesAsync(new HandBuiltNtlmContexts(), matchesSspiBuild: false, SentType1, sentBeforeAnyChallenge: true, "NTLM " + Convert.ToBase64String(challenge));

        CollectionAssert.AreEqual(new[] { "NTLM handshake failure (bad type-2 message)", "NTLM authentication problem, ignoring." }, lines);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_Type2TargetInfoOutOfRangeWithSspi_ReportsOnlyTheType3Failure()
    {
        RecordingInfoEvents events = new();
        NtlmHttpAuthenticator authenticator = new(new HandBuiltNtlmContexts(), matchesSspiBuild: true);

        HttpAuthenticationFailedException failure = await Assert.ThrowsExactlyAsync<HttpAuthenticationFailedException>(
            () => authenticator.CreateAuthorizationAsync(Request(events), SentType1, sentBeforeAnyChallenge: true, [ChallengeWithTargetInformationAt(48, 16, 48)], CancellationToken.None).AsTask());

        Assert.AreEqual(CurlExitCode.AuthError, failure.ExitCode);
        CollectionAssert.AreEqual(new[] { "NTLM handshake failure (type-3 message): Status=0x80090308\n" }, events.Info);
    }

    /// <summary>
    /// Makes a <paramref name="messageLength" />-byte Type 2 from the measured one's header, asking
    /// for target information of <paramref name="length" /> bytes at <paramref name="offset" />.
    /// </summary>
    private static string ChallengeWithTargetInformationAt(int offset, int length, int messageLength)
    {
        byte[] measured = Convert.FromBase64String(HandBuiltNtlmSecurityContextTests.MeasuredChallenge);
        byte[] challenge = new byte[messageLength];
        measured.AsSpan(0, NtlmChallengeMessage.TargetInformationHeaderLength).CopyTo(challenge);
        uint flags = BitConverter.ToUInt32(challenge, 20) | (uint)NtlmNegotiateFlags.NegotiateTargetInfo;
        BitConverter.TryWriteBytes(challenge.AsSpan(20), flags);
        BitConverter.TryWriteBytes(challenge.AsSpan(40), (ushort)length);
        BitConverter.TryWriteBytes(challenge.AsSpan(42), (ushort)length);
        BitConverter.TryWriteBytes(challenge.AsSpan(44), (uint)offset);
        return "NTLM " + Convert.ToBase64String(challenge);
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
