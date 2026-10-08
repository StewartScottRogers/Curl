using Curl.Testing;

namespace Curl.Ntlm;

/// <summary>
/// Checks <see cref="NtlmChallengeAnswerer" /> answers as curl 8.21.0's
/// <c>Curl_auth_create_ntlm_type3_message</c> does: NTLMv2 with the injected client
/// challenge and whole-second time when the challenge sets extended session security,
/// NTLMv1 with that flag cleared otherwise.
/// </summary>
[TestClass]
public sealed class NtlmChallengeAnswererTests
{
    private static readonly byte[] ServerChallenge = Convert.FromHexString("0123456789abcdef");

    private static readonly byte[] ClientChallenge = Convert.FromHexString("aaaaaaaaaaaaaaaa");

    private static readonly byte[] TargetInformation = Convert.FromHexString("02000c0044006f006d00610069006e0000000000");

    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 34, 56, 789, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Answer_ExtendedSessionSecurity_SendsNtlmV2AtTheWholeSecond()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        NtlmNegotiateFlags flags = NtlmNegotiateFlags.NegotiateUnicode | NtlmNegotiateFlags.NegotiateNtlm |
            NtlmNegotiateFlags.NegotiateExtendedSessionSecurity | NtlmNegotiateFlags.NegotiateTargetInfo;
        NtlmChallengeMessage challenge = new(flags, ServerChallenge, [], TargetInformation, []);
        ArrangeChallenge(diagnostics, challenge, @"Domain\User", "Password");

        NtlmAuthenticateMessage message = CreateAnswerer().Answer(challenge, @"Domain\User", "Password");
        ActMessage(diagnostics, message);

        NtlmResponses expected = NtlmResponseComputation.ComputeV2(
            "User", "Domain", "Password", ServerChallenge, ClientChallenge, new DateTimeOffset(2026, 9, 28, 12, 34, 56, TimeSpan.Zero), TargetInformation);
        diagnostics.Assert("flags", flags, message.Flags);
        diagnostics.Diff("LM response", expected.LmChallengeResponse, message.LmChallengeResponse);
        diagnostics.Diff("NT response", expected.NtChallengeResponse, message.NtChallengeResponse);
        Assert.AreEqual(flags, message.Flags);
        CollectionAssert.AreEqual(expected.LmChallengeResponse, message.LmChallengeResponse);
        CollectionAssert.AreEqual(expected.NtChallengeResponse, message.NtChallengeResponse);
        Assert.AreEqual("Domain", message.Domain);
        Assert.AreEqual("User", message.User);
        Assert.AreEqual(NtlmAuthenticateMessage.CurlWorkstation, message.Workstation);
    }

    [TestMethod]
    public void Answer_ClockBefore1601_SendsNegativeFileTimeInsteadOfThrowing()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        NtlmChallengeMessage challenge = new(NtlmNegotiateFlags.NegotiateExtendedSessionSecurity, new byte[8], [], [], []);
        ArrangeChallenge(diagnostics, challenge, "u", "pw");
        diagnostics.Arrange("clock", "DateTimeOffset.MinValue (0001-01-01 UTC)");
        NtlmChallengeAnswerer answerer = new(new FixedTimeProvider(DateTimeOffset.MinValue), new FixedRandomSource(ClientChallenge));

        NtlmAuthenticateMessage message = answerer.Answer(challenge, "u", "pw");
        ActMessage(diagnostics, message);
        long writtenTime = BitConverter.ToInt64(message.NtChallengeResponse, 24);
        diagnostics.Act("file time at offset 24", writtenTime);

        // curl's (time + 11644473600) * 10000000 for 0001-01-01: -62135596800 + 11644473600 seconds.
        long expectedTime = (-62135596800L + 11644473600L) * 10000000L;
        diagnostics.Assert("file time at offset 24", expectedTime, writtenTime);
        Assert.AreEqual(expectedTime, writtenTime);
    }

    [TestMethod]
    public void Answer_NoExtendedSessionSecurity_SendsNtlmV1()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        NtlmNegotiateFlags flags = NtlmNegotiateFlags.NegotiateUnicode | NtlmNegotiateFlags.NegotiateNtlm;
        NtlmChallengeMessage challenge = new(flags, ServerChallenge, [], [], []);
        ArrangeChallenge(diagnostics, challenge, "User", "Password");

        NtlmAuthenticateMessage message = CreateAnswerer().Answer(challenge, "User", "Password");
        ActMessage(diagnostics, message);

        NtlmResponses expected = NtlmResponseComputation.ComputeV1("Password", ServerChallenge, flags);
        diagnostics.Assert("flags", flags, message.Flags);
        diagnostics.Diff("LM response", expected.LmChallengeResponse, message.LmChallengeResponse);
        diagnostics.Diff("NT response", expected.NtChallengeResponse, message.NtChallengeResponse);
        Assert.AreEqual(flags, message.Flags);
        CollectionAssert.AreEqual(expected.LmChallengeResponse, message.LmChallengeResponse);
        CollectionAssert.AreEqual(expected.NtChallengeResponse, message.NtChallengeResponse);
        Assert.AreEqual(string.Empty, message.Domain);
        Assert.AreEqual("User", message.User);
    }

    [TestMethod]
    public void Answer_NullChallenge_Throws()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge", "null");
        diagnostics.Arrange("user name and password", "User, Password");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => CreateAnswerer().Answer(null!, "User", "Password"));

        diagnostics.Act("exception", $"{exception.GetType().Name} for {exception.ParamName}");
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    private static void ArrangeChallenge(TestDiagnostics diagnostics, NtlmChallengeMessage challenge, string userName, string password)
    {
        diagnostics.Arrange("challenge flags", $"0x{(uint)challenge.Flags:X8} ({challenge.Flags})");
        diagnostics.Bytes("server challenge", challenge.ServerChallenge);
        diagnostics.Bytes("target information", challenge.TargetInformation);
        diagnostics.Arrange("user name", userName);
        diagnostics.Arrange("password", password);
        diagnostics.Arrange("time", Now.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        diagnostics.Bytes("client challenge", ClientChallenge);
    }

    private static void ActMessage(TestDiagnostics diagnostics, NtlmAuthenticateMessage message)
    {
        diagnostics.Act("flags", $"0x{(uint)message.Flags:X8} ({message.Flags})");
        diagnostics.Act("domain, user, workstation", $"'{message.Domain}', '{message.User}', '{message.Workstation}'");
        diagnostics.Bytes("LM response", message.LmChallengeResponse);
        diagnostics.Bytes("NT response", message.NtChallengeResponse);
    }

    private static NtlmChallengeAnswerer CreateAnswerer() => new(new FixedTimeProvider(Now), new FixedRandomSource(ClientChallenge));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FixedRandomSource(byte[] bytes) : INtlmRandomSource
    {
        public void Fill(Span<byte> destination) => bytes.AsSpan(0, destination.Length).CopyTo(destination);
    }
}
