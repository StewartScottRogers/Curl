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

    [TestMethod]
    public void Answer_ExtendedSessionSecurity_SendsNtlmV2AtTheWholeSecond()
    {
        NtlmNegotiateFlags flags = NtlmNegotiateFlags.NegotiateUnicode | NtlmNegotiateFlags.NegotiateNtlm |
            NtlmNegotiateFlags.NegotiateExtendedSessionSecurity | NtlmNegotiateFlags.NegotiateTargetInfo;
        NtlmChallengeMessage challenge = new(flags, ServerChallenge, [], TargetInformation, []);

        NtlmAuthenticateMessage message = CreateAnswerer().Answer(challenge, @"Domain\User", "Password");

        NtlmResponses expected = NtlmResponseComputation.ComputeV2(
            "User", "Domain", "Password", ServerChallenge, ClientChallenge, new DateTimeOffset(2026, 9, 28, 12, 34, 56, TimeSpan.Zero), TargetInformation);
        Assert.AreEqual(flags, message.Flags);
        CollectionAssert.AreEqual(expected.LmChallengeResponse, message.LmChallengeResponse);
        CollectionAssert.AreEqual(expected.NtChallengeResponse, message.NtChallengeResponse);
        Assert.AreEqual("Domain", message.Domain);
        Assert.AreEqual("User", message.User);
        Assert.AreEqual(NtlmAuthenticateMessage.CurlWorkstation, message.Workstation);
    }

    [TestMethod]
    public void Answer_NoExtendedSessionSecurity_SendsNtlmV1()
    {
        NtlmNegotiateFlags flags = NtlmNegotiateFlags.NegotiateUnicode | NtlmNegotiateFlags.NegotiateNtlm;
        NtlmChallengeMessage challenge = new(flags, ServerChallenge, [], [], []);

        NtlmAuthenticateMessage message = CreateAnswerer().Answer(challenge, "User", "Password");

        NtlmResponses expected = NtlmResponseComputation.ComputeV1("Password", ServerChallenge, flags);
        Assert.AreEqual(flags, message.Flags);
        CollectionAssert.AreEqual(expected.LmChallengeResponse, message.LmChallengeResponse);
        CollectionAssert.AreEqual(expected.NtChallengeResponse, message.NtChallengeResponse);
        Assert.AreEqual(string.Empty, message.Domain);
        Assert.AreEqual("User", message.User);
    }

    [TestMethod]
    public void Answer_NullChallenge_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CreateAnswerer().Answer(null!, "User", "Password"));
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
