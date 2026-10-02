using System.Net;
using Curl.Kerberos;
using Curl.Ntlm;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="NtlmHttpAuthenticator" /> to the legs both platform curls sent for
/// <c>--ntlm -u u:p</c> on 2026-09-28 (BL-526 Notes): Type 1 up front and once more for a bare
/// <c>NTLM</c>, Type 3 for a Type 2 challenge from a fresh context stepped through Type 1,
/// nothing after Type 3, and a Type 2 the context cannot answer ending on the 401 for curl's
/// own NTLM or with exit 94 for the SSPI build, and a Type 3 past curl's 1024-byte buffer
/// failing with exit 100 on curl's own NTLM (BL-849).
/// </summary>
[TestClass]
public sealed partial class NtlmHttpAuthenticatorTests
{
    private static readonly byte[] Type1 = Convert.FromBase64String(HandBuiltNtlmSecurityContextTests.CurlType1);

    private static readonly byte[] Type3 = Convert.FromBase64String(HandBuiltNtlmSecurityContextTests.MeasuredType3);

    /// <summary>
    /// The longest user whose Type 3 answer to <see cref="Type2Challenge" /> fits curl's buffer:
    /// 64 header, 24 LM, 84 NTLMv2 and 22 workstation bytes, then 2 per user character, to
    /// 1022 bytes; one character more is 1024, which curl's strict check refuses.
    /// </summary>
    private const int LargestFittingUserLength = 414;

    private static readonly string Type2Challenge = "NTLM " + HandBuiltNtlmSecurityContextTests.MeasuredChallenge;

    [TestMethod]
    public async Task CreateAuthorizationAsync_BeforeAnyChallenge_SendsType1()
    {
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Type1));
        ScriptedSecurityContextFactory contexts = new(context);

        string? value = await Authenticator(contexts).CreateAuthorizationAsync(Request("u:p"), null, sentBeforeAnyChallenge: false, [], CancellationToken.None);

        Assert.AreEqual("NTLM " + HandBuiltNtlmSecurityContextTests.CurlType1, value);
        Assert.AreEqual(new SecurityContextRequest(SecurityMechanism.Ntlm, "HTTP", "127.0.0.1") { UserName = "u", Password = "p" }, contexts.Requests.Single());
        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    [DataRow(null, false, DisplayName = "Nothing sent: --anyauth's first request")]
    [DataRow("NTLM " + HandBuiltNtlmSecurityContextTests.CurlType1, true, DisplayName = "Type 1 sent before any challenge")]
    public async Task CreateAuthorizationAsync_BareNtlmChallenge_SendsType1(string? sent, bool sentBeforeAnyChallenge)
    {
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Type1)));

        string? value = await Authenticator(contexts).CreateAuthorizationAsync(Request("u:p"), sent, sentBeforeAnyChallenge, ["Basic realm=\"r\"", "NTLM"], CancellationToken.None);

        Assert.AreEqual("NTLM " + HandBuiltNtlmSecurityContextTests.CurlType1, value);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_BareNtlmChallengeToType1AnsweringAChallenge_SendsNothing()
    {
        ScriptedSecurityContextFactory contexts = new();

        string? value = await Authenticator(contexts).CreateAuthorizationAsync(Request("u:p"), "NTLM " + HandBuiltNtlmSecurityContextTests.CurlType1, sentBeforeAnyChallenge: false, ["NTLM"], CancellationToken.None);

        Assert.IsNull(value);
        Assert.IsEmpty(contexts.Requests);
    }

    [TestMethod]
    [DataRow("NTLM", DisplayName = "Bare NTLM: curl's \"NTLM handshake rejected\"")]
    [DataRow("NTLM " + HandBuiltNtlmSecurityContextTests.MeasuredChallenge, DisplayName = "Another Type 2")]
    public async Task CreateAuthorizationAsync_AfterType3_SendsNothing(string challenge)
    {
        ScriptedSecurityContextFactory contexts = new();

        string? value = await Authenticator(contexts).CreateAuthorizationAsync(Request("u:p"), "NTLM " + HandBuiltNtlmSecurityContextTests.MeasuredType3, sentBeforeAnyChallenge: false, [challenge], CancellationToken.None);

        Assert.IsNull(value);
        Assert.IsEmpty(contexts.Requests);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "Nothing sent")]
    [DataRow("NTLM " + HandBuiltNtlmSecurityContextTests.CurlType1, DisplayName = "Type 1 sent")]
    [DataRow("Basic dTpw", DisplayName = "Another scheme sent")]
    [DataRow("NTLM !", DisplayName = "An NTLM value that is not base64")]
    [DataRow("NTLM AAAA", DisplayName = "An NTLM value too short to be a message")]
    public async Task CreateAuthorizationAsync_Type2Challenge_SendsType3FromAFreshContextSteppedThroughType1(string? sent)
    {
        ScriptedSecurityContext context = new(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Type1),
            new SecurityContextStep(SecurityContextStatus.Completed, Type3));

        string? value = await Authenticator(new ScriptedSecurityContextFactory(context)).CreateAuthorizationAsync(Request("u:p"), sent, sentBeforeAnyChallenge: false, [Type2Challenge], CancellationToken.None);

        Assert.AreEqual("NTLM " + HandBuiltNtlmSecurityContextTests.MeasuredType3, value);
        Assert.IsEmpty(context.IncomingTokens[0]);
        CollectionAssert.AreEqual(Convert.FromBase64String(HandBuiltNtlmSecurityContextTests.MeasuredChallenge), context.IncomingTokens[1]);
        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "curl's own NTLM")]
    [DataRow(true, DisplayName = "SSPI: curl's base64 decoder rejects it first")]
    public async Task CreateAuthorizationAsync_ChallengeNotBase64_SendsNothing(bool matchesSspiBuild)
    {
        ScriptedSecurityContextFactory contexts = new();

        string? value = await new NtlmHttpAuthenticator(contexts, matchesSspiBuild).CreateAuthorizationAsync(Request("u:p"), "NTLM " + HandBuiltNtlmSecurityContextTests.CurlType1, sentBeforeAnyChallenge: true, ["NTLM @@@notbase64"], CancellationToken.None);

        Assert.IsNull(value);
        Assert.IsEmpty(contexts.Requests);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_Type2CurlsOwnNtlmCannotRead_SendsNothing()
    {
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Type1),
            new SecurityContextStep(SecurityContextStatus.MalformedToken, [])));

        string? value = await Authenticator(contexts).CreateAuthorizationAsync(Request("u:p"), "NTLM " + HandBuiltNtlmSecurityContextTests.CurlType1, sentBeforeAnyChallenge: true, [Type2Challenge], CancellationToken.None);

        Assert.IsNull(value);
    }

    /// <summary>
    /// Pins curl 8.18.0 on Ubuntu (BL-849 Notes): <c>--ntlm -u &lt;600 a&gt;:p</c> against the
    /// measured Type 2 sent one request and failed with <c>curl: (100) user + domain + hostname
    /// too big for NTLM</c>.
    /// </summary>
    [TestMethod]
    public async Task CreateAuthorizationAsync_Type3PastCurlsBufferWithCurlsOwnNtlm_FailsWithExit100()
    {
        NtlmHttpAuthenticator authenticator = Authenticator(new HandBuiltNtlmContexts());

        HttpAuthenticationFailedException failure = await Assert.ThrowsExactlyAsync<HttpAuthenticationFailedException>(
            () => authenticator.CreateAuthorizationAsync(Request(new string('a', 600) + ":p"), "NTLM " + HandBuiltNtlmSecurityContextTests.CurlType1, sentBeforeAnyChallenge: true, [Type2Challenge], CancellationToken.None).AsTask());

        Assert.AreEqual(CurlExitCode.TooLarge, failure.ExitCode);
        Assert.AreEqual("user + domain + hostname too big for NTLM", failure.Message);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_LargestType3ThatFitsWithCurlsOwnNtlm_AnswersIt()
    {
        NtlmHttpAuthenticator authenticator = Authenticator(new HandBuiltNtlmContexts());

        string? value = await authenticator.CreateAuthorizationAsync(Request(new string('a', LargestFittingUserLength) + ":p"), "NTLM " + HandBuiltNtlmSecurityContextTests.CurlType1, sentBeforeAnyChallenge: true, [Type2Challenge], CancellationToken.None);

        Assert.IsNotNull(value);
        Assert.HasCount(NtlmAuthenticateMessage.CurlBufferSize - 2, Convert.FromBase64String(value["NTLM ".Length..]));
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_OneCharacterPastTheLargestType3_FailsWithExit100()
    {
        NtlmHttpAuthenticator authenticator = Authenticator(new HandBuiltNtlmContexts());

        HttpAuthenticationFailedException failure = await Assert.ThrowsExactlyAsync<HttpAuthenticationFailedException>(
            () => authenticator.CreateAuthorizationAsync(Request(new string('a', LargestFittingUserLength + 1) + ":p"), "NTLM " + HandBuiltNtlmSecurityContextTests.CurlType1, sentBeforeAnyChallenge: true, [Type2Challenge], CancellationToken.None).AsTask());

        Assert.AreEqual(CurlExitCode.TooLarge, failure.ExitCode);
    }

    /// <summary>
    /// curl 8.21.0's <c>lib/vauth/ntlm.c</c> (BL-1114): when the NTLMv2 response, which carries
    /// the challenge's target information, alone ends past the 1024-byte buffer, curl fails
    /// with exit 100 and <c>incoming NTLM message too big</c>, not the names message.
    /// </summary>
    [TestMethod]
    public async Task CreateAuthorizationAsync_TargetInformationPushesResponsesPastCurlsBuffer_FailsWithIncomingMessageTooBig()
    {
        NtlmHttpAuthenticator authenticator = Authenticator(new HandBuiltNtlmContexts());

        HttpAuthenticationFailedException failure = await Assert.ThrowsExactlyAsync<HttpAuthenticationFailedException>(
            () => authenticator.CreateAuthorizationAsync(Request("u:p"), "NTLM " + HandBuiltNtlmSecurityContextTests.CurlType1, sentBeforeAnyChallenge: true, [ChallengeWithTargetInformation(1000)], CancellationToken.None).AsTask());

        Assert.AreEqual(CurlExitCode.TooLarge, failure.ExitCode);
        Assert.AreEqual("incoming NTLM message too big", failure.Message);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_OtherContextRefusesType3_FailsWithTheNamesMessage()
    {
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Type1),
            new SecurityContextStep(SecurityContextStatus.Refused, [])));

        HttpAuthenticationFailedException failure = await Assert.ThrowsExactlyAsync<HttpAuthenticationFailedException>(
            () => Authenticator(contexts).CreateAuthorizationAsync(Request("u:p"), "NTLM " + HandBuiltNtlmSecurityContextTests.CurlType1, sentBeforeAnyChallenge: true, [Type2Challenge], CancellationToken.None).AsTask());

        Assert.AreEqual(CurlExitCode.TooLarge, failure.ExitCode);
        Assert.AreEqual(NtlmHttpAuthenticator.Type3TooLargeMessage, failure.Message);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_ContextRefusesType2WithSspi_FailsWithExit94()
    {
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Type1),
            new SecurityContextStep(SecurityContextStatus.MalformedToken, [])));
        NtlmHttpAuthenticator authenticator = new(contexts, matchesSspiBuild: true);

        HttpAuthenticationFailedException failure = await Assert.ThrowsExactlyAsync<HttpAuthenticationFailedException>(
            () => authenticator.CreateAuthorizationAsync(Request("u:p"), "NTLM x", sentBeforeAnyChallenge: true, [Type2Challenge], CancellationToken.None).AsTask());

        Assert.AreEqual(CurlExitCode.AuthError, failure.ExitCode);
        Assert.AreEqual("An authentication function returned an error", failure.Message);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "curl's own NTLM")]
    [DataRow(true, DisplayName = "SSPI")]
    public async Task CreateAuthorizationAsync_NoType1ForAType2Challenge_SendsNothingOrFails(bool matchesSspiBuild)
    {
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.NoCredentials, []));
        NtlmHttpAuthenticator authenticator = new(new ScriptedSecurityContextFactory(context), matchesSspiBuild);

        Task<string?> answer = authenticator.CreateAuthorizationAsync(Request("u:p"), null, sentBeforeAnyChallenge: false, [Type2Challenge], CancellationToken.None).AsTask();

        if (matchesSspiBuild)
        {
            await Assert.ThrowsExactlyAsync<HttpAuthenticationFailedException>(() => answer);
        }
        else
        {
            Assert.IsNull(await answer);
        }

        Assert.HasCount(1, context.IncomingTokens);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_NoType1BeforeAnyChallenge_SendsNothing()
    {
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.NoCredentials, [])));

        string? value = await new NtlmHttpAuthenticator(contexts, matchesSspiBuild: true).CreateAuthorizationAsync(Request("u:p"), null, sentBeforeAnyChallenge: false, [], CancellationToken.None);

        Assert.IsNull(value);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_NullArguments_Throw()
    {
        NtlmHttpAuthenticator authenticator = Authenticator(new ScriptedSecurityContextFactory());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => authenticator.CreateAuthorizationAsync(null!, null, false, [], CancellationToken.None).AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => authenticator.CreateAuthorizationAsync(Request("u:p"), null, false, null!, CancellationToken.None).AsTask());
    }

    [TestMethod]
    [DataRow("u:p", null, "u", DisplayName = "No domain")]
    [DataRow("CORP\\u:p", "CORP", "u", DisplayName = "DOMAIN\\user")]
    [DataRow("CORP/u:p", "CORP", "u", DisplayName = "DOMAIN/user")]
    [DataRow("a/b\\c:p", "a/b", "c", DisplayName = "The backslash splits first, as curl's ntlm.c splits")]
    public void ContextRequestFor_UserName_SplitsTheDomainAsCurlDoes(string userColonPassword, string? domain, string user)
    {
        SecurityContextRequest request = NtlmHttpAuthenticator.ContextRequestFor(Request(userColonPassword));

        Assert.AreEqual(SecurityMechanism.Ntlm, request.Mechanism);
        Assert.AreEqual(domain, request.Domain);
        Assert.AreEqual(user, request.UserName);
        Assert.AreEqual("p", request.Password);
    }

    [TestMethod]
    [DataRow(":", DisplayName = "-u :")]
    [DataRow(":p", DisplayName = "-u :p")]
    public void ContextRequestFor_NoUserName_AsksForTheDefaultCredentials(string userColonPassword)
    {
        SecurityContextRequest request = NtlmHttpAuthenticator.ContextRequestFor(Request(userColonPassword));

        Assert.AreEqual(new SecurityContextRequest(SecurityMechanism.Ntlm, "HTTP", "127.0.0.1"), request);
    }

    [TestMethod]
    public void ContextRequestFor_CredentialWithItsOwnDomain_KeepsIt()
    {
        HttpAuthRequest request = Request("u:p") with { Credential = new NetworkCredential("u", "p", "CORP"), Url = CurlUrl.Parse("http://[::1]:18526/x") };

        SecurityContextRequest contextRequest = NtlmHttpAuthenticator.ContextRequestFor(request);

        Assert.AreEqual("CORP", contextRequest.Domain);
        Assert.AreEqual("::1", contextRequest.HostName);
    }

    private static NtlmHttpAuthenticator Authenticator(ISecurityContextFactory contexts) => new(contexts, matchesSspiBuild: false);

    /// <summary>
    /// Makes the measured Type 2 challenge with <paramref name="targetInformationLength" /> zero
    /// bytes of target information after its 48-byte header in place of the measured ones.
    /// </summary>
    private static string ChallengeWithTargetInformation(int targetInformationLength)
    {
        byte[] measured = Convert.FromBase64String(HandBuiltNtlmSecurityContextTests.MeasuredChallenge);
        byte[] challenge = new byte[NtlmChallengeMessage.TargetInformationHeaderLength + targetInformationLength];
        measured.AsSpan(0, NtlmChallengeMessage.TargetInformationHeaderLength).CopyTo(challenge);
        uint flags = BitConverter.ToUInt32(challenge, 20) | (uint)NtlmNegotiateFlags.NegotiateTargetInfo;
        BitConverter.TryWriteBytes(challenge.AsSpan(20), flags);
        BitConverter.TryWriteBytes(challenge.AsSpan(12), 0u);
        BitConverter.TryWriteBytes(challenge.AsSpan(40), (ushort)targetInformationLength);
        BitConverter.TryWriteBytes(challenge.AsSpan(42), (ushort)targetInformationLength);
        BitConverter.TryWriteBytes(challenge.AsSpan(44), (uint)NtlmChallengeMessage.TargetInformationHeaderLength);
        return "NTLM " + Convert.ToBase64String(challenge);
    }

    private static HttpAuthRequest Request(string userColonPassword)
    {
        int colon = userColonPassword.IndexOf(':', StringComparison.Ordinal);
        NetworkCredential credential = new(userColonPassword[..colon], userColonPassword[(colon + 1)..]);
        return new("GET", CurlUrl.Parse("http://127.0.0.1:18526/x"), "/x", credential, null, HttpAuthSchemes.Ntlm, IsProxy: false);
    }

    /// <summary>Makes curl's own NTLM contexts, with the measured client challenge and time.</summary>
    private sealed class HandBuiltNtlmContexts : ISecurityContextFactory
    {
        public ISecurityContext Create(SecurityContextRequest request) =>
            new HandBuiltNtlmSecurityContext(request, new NtlmChallengeAnswerer(
                new FixedTimeProvider(HandBuiltNtlmSecurityContextTests.MeasuredTime),
                new FixedNtlmRandomSource(HandBuiltNtlmSecurityContextTests.MeasuredClientChallenge)));
    }
}
