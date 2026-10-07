using System.Globalization;
using Curl.Testing;

namespace Curl.Ntlm;

/// <summary>
/// Pins <see cref="NtlmResponseComputation" /> to every value in MS-NLMP sections 4.2.2
/// (NTLMv1), 4.2.3 (NTLMv1 with client challenge) and 4.2.4 (NTLMv2): the responses, the
/// session base key, the key exchange key and the encrypted random session key, all from
/// section 4.2.1's common values.
/// </summary>
[TestClass]
public sealed class NtlmResponseComputationTests
{
    // MS-NLMP 4.2.1: common values.
    private const string Password = "Password";

    private const string User = "User";

    private const string Domain = "Domain";

    private static readonly byte[] ServerChallenge = Convert.FromHexString("0123456789abcdef");

    private static readonly byte[] ClientChallenge = Convert.FromHexString("aaaaaaaaaaaaaaaa");

    private static readonly byte[] RandomSessionKey = Convert.FromHexString("55555555555555555555555555555555");

    private static readonly DateTimeOffset Time = DateTimeOffset.FromFileTime(0);

    // MS-NLMP 4.2.4: MsvAvNbDomainName "Domain", MsvAvNbComputerName "Server", MsvAvEOL.
    private static readonly byte[] TargetInformation = Convert.FromHexString(
        "02000c0044006f006d00610069006e00" + "01000c00530065007200760065007200" + "00000000");

    // MS-NLMP 4.2.2: 0xe2028233, KEY_EXCH ... UNICODE, without LM_KEY or NON_NT_SESSION_KEY.
    private const NtlmNegotiateFlags V1Flags = (NtlmNegotiateFlags)0xe2028233;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ComputeV1_MsNlmp422_GivesItsResponsesAndKeys()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        ArrangeCommonValues(diagnostics, V1Flags);

        NtlmResponses responses = NtlmResponseComputation.ComputeV1(Password, ServerChallenge, V1Flags);
        string encryptedSessionKey = Hex(NtlmResponseComputation.EncryptSessionKey(responses.KeyExchangeKey, RandomSessionKey));
        ActResponses(diagnostics, responses);
        diagnostics.Act("encrypted session key", encryptedSessionKey);

        // 4.2.2.2.1, 4.2.2.2.2, 4.2.2.1.3.
        diagnostics.Diff("NT response", "67c43011f30298a2ad35ece64f16331c44bdbed927841f94", Hex(responses.NtChallengeResponse));
        diagnostics.Diff("LM response", "98def7b87f88aa5dafe2df779688a172def11c7d5ccdef13", Hex(responses.LmChallengeResponse));
        diagnostics.Diff("session base key", "d87262b0cde4b1cb7499becccdf10784", Hex(responses.SessionBaseKey));
        diagnostics.Diff("key exchange key", "d87262b0cde4b1cb7499becccdf10784", Hex(responses.KeyExchangeKey));
        diagnostics.Diff("encrypted session key", "518822b1b3f350c8958682ecbb3e3cb7", encryptedSessionKey);
        Assert.AreEqual("67c43011f30298a2ad35ece64f16331c44bdbed927841f94", Hex(responses.NtChallengeResponse));
        Assert.AreEqual("98def7b87f88aa5dafe2df779688a172def11c7d5ccdef13", Hex(responses.LmChallengeResponse));
        Assert.AreEqual("d87262b0cde4b1cb7499becccdf10784", Hex(responses.SessionBaseKey));
        Assert.AreEqual("d87262b0cde4b1cb7499becccdf10784", Hex(responses.KeyExchangeKey));

        // 4.2.2.2.3: RC4 of the RandomSessionKey under the KeyExchangeKey.
        Assert.AreEqual("518822b1b3f350c8958682ecbb3e3cb7", encryptedSessionKey);
    }

    [TestMethod]
    public void ComputeV1_MsNlmp422WithLmKey_GivesItsKeyExchangeKeyAndEncryptedSessionKey()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        ArrangeCommonValues(diagnostics, V1Flags | NtlmNegotiateFlags.NegotiateLmKey);

        NtlmResponses responses = NtlmResponseComputation.ComputeV1(Password, ServerChallenge, V1Flags | NtlmNegotiateFlags.NegotiateLmKey);
        string encryptedSessionKey = Hex(NtlmResponseComputation.EncryptSessionKey(responses.KeyExchangeKey, RandomSessionKey));
        ActResponses(diagnostics, responses);
        diagnostics.Act("encrypted session key", encryptedSessionKey);

        // 4.2.2.2.2 (the LM_KEY KeyExchangeKey) and 4.2.2.2.3.
        diagnostics.Diff("key exchange key", "b09e379f7fbecb1eaf0afdcb0383c8a0", Hex(responses.KeyExchangeKey));
        diagnostics.Diff("encrypted session key", "4cd7bb57d697ef9b549f02b8f9b37864", encryptedSessionKey);
        Assert.AreEqual("b09e379f7fbecb1eaf0afdcb0383c8a0", Hex(responses.KeyExchangeKey));
        Assert.AreEqual("4cd7bb57d697ef9b549f02b8f9b37864", encryptedSessionKey);
    }

    [TestMethod]
    public void ComputeV1_MsNlmp422WithNonNtSessionKey_GivesItsEncryptedSessionKey()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        ArrangeCommonValues(diagnostics, V1Flags | NtlmNegotiateFlags.RequestNonNtSessionKey);

        NtlmResponses responses = NtlmResponseComputation.ComputeV1(Password, ServerChallenge, V1Flags | NtlmNegotiateFlags.RequestNonNtSessionKey);
        string encryptedSessionKey = Hex(NtlmResponseComputation.EncryptSessionKey(responses.KeyExchangeKey, RandomSessionKey));
        ActResponses(diagnostics, responses);
        diagnostics.Act("encrypted session key", encryptedSessionKey);

        // 3.4.5.1: LMOWFv1[0..7] and eight zero bytes; 4.2.2.2.3.
        diagnostics.Diff("key exchange key", "e52cac67419a9a220000000000000000", Hex(responses.KeyExchangeKey));
        diagnostics.Diff("encrypted session key", "7452ca55c225a1ca04b48fae32cf56fc", encryptedSessionKey);
        Assert.AreEqual("e52cac67419a9a220000000000000000", Hex(responses.KeyExchangeKey));
        Assert.AreEqual("7452ca55c225a1ca04b48fae32cf56fc", encryptedSessionKey);
    }

    [TestMethod]
    public void ComputeV1WithExtendedSessionSecurity_MsNlmp423_GivesItsResponsesAndKeys()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        ArrangeCommonValues(diagnostics, null);

        NtlmResponses responses = NtlmResponseComputation.ComputeV1WithExtendedSessionSecurity(Password, ServerChallenge, ClientChallenge);
        ActResponses(diagnostics, responses);

        // 4.2.3.2.1, 4.2.3.2.2, 4.2.3.1.2, 4.2.3.1.3.
        diagnostics.Diff("LM response", "aaaaaaaaaaaaaaaa00000000000000000000000000000000", Hex(responses.LmChallengeResponse));
        diagnostics.Diff("NT response", "7537f803ae367128ca458204bde7caf81e97ed2683267232", Hex(responses.NtChallengeResponse));
        diagnostics.Diff("session base key", "d87262b0cde4b1cb7499becccdf10784", Hex(responses.SessionBaseKey));
        diagnostics.Diff("key exchange key", "eb93429a8bd952f8b89c55b87f475edc", Hex(responses.KeyExchangeKey));
        Assert.AreEqual("aaaaaaaaaaaaaaaa00000000000000000000000000000000", Hex(responses.LmChallengeResponse));
        Assert.AreEqual("7537f803ae367128ca458204bde7caf81e97ed2683267232", Hex(responses.NtChallengeResponse));
        Assert.AreEqual("d87262b0cde4b1cb7499becccdf10784", Hex(responses.SessionBaseKey));
        Assert.AreEqual("eb93429a8bd952f8b89c55b87f475edc", Hex(responses.KeyExchangeKey));
    }

    [TestMethod]
    public void ComputeV2_MsNlmp424_GivesItsResponsesAndKeys()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        ArrangeCommonValues(diagnostics, null);
        diagnostics.Arrange("timestamp", Time.ToFileTime());
        diagnostics.Bytes("target information", TargetInformation);

        NtlmResponses responses = NtlmResponseComputation.ComputeV2(User, Domain, Password, ServerChallenge, ClientChallenge, Time, TargetInformation);
        string encryptedSessionKey = Hex(NtlmResponseComputation.EncryptSessionKey(responses.KeyExchangeKey, RandomSessionKey));
        ActResponses(diagnostics, responses);
        diagnostics.Act("encrypted session key", encryptedSessionKey);

        // 4.2.4.2.1, 4.2.4.2.2 (NTProofStr, then 4.2.4.1.3's temp), 4.2.4.1.2.
        string expectedNtResponse =
            "68cd0ab851e51c96aabc927bebef6a1c" +
            "0101000000000000" + "0000000000000000" + "aaaaaaaaaaaaaaaa" + "00000000" +
            Hex(TargetInformation) + "00000000";
        diagnostics.Diff("LM response", "86c35097ac9cec102554764a57cccc19aaaaaaaaaaaaaaaa", Hex(responses.LmChallengeResponse));
        diagnostics.Diff("NT response", Convert.FromHexString(expectedNtResponse), responses.NtChallengeResponse);
        diagnostics.Diff("session base key", "8de40ccadbc14a82f15cb0ad0de95ca3", Hex(responses.SessionBaseKey));
        diagnostics.Diff("key exchange key", "8de40ccadbc14a82f15cb0ad0de95ca3", Hex(responses.KeyExchangeKey));
        diagnostics.Diff("encrypted session key", "c5dad2544fc9799094ce1ce90bc9d03e", encryptedSessionKey);
        Assert.AreEqual("86c35097ac9cec102554764a57cccc19aaaaaaaaaaaaaaaa", Hex(responses.LmChallengeResponse));
        Assert.AreEqual(expectedNtResponse, Hex(responses.NtChallengeResponse));
        Assert.AreEqual("8de40ccadbc14a82f15cb0ad0de95ca3", Hex(responses.SessionBaseKey));
        Assert.AreEqual("8de40ccadbc14a82f15cb0ad0de95ca3", Hex(responses.KeyExchangeKey));

        // 4.2.4.2.3.
        Assert.AreEqual("c5dad2544fc9799094ce1ce90bc9d03e", encryptedSessionKey);
    }

    [TestMethod]
    public void ComputeV2_Timestamp_IsWrittenAsALittleEndianFileTime()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        DateTimeOffset timestamp = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        ArrangeCommonValues(diagnostics, null);
        diagnostics.Arrange("timestamp", string.Create(CultureInfo.InvariantCulture, $"{timestamp:O} (file time {timestamp.ToFileTime()})"));

        NtlmResponses responses = NtlmResponseComputation.ComputeV2(User, Domain, Password, ServerChallenge, ClientChallenge, timestamp, []);
        ActResponses(diagnostics, responses);
        long writtenTime = BitConverter.ToInt64(responses.NtChallengeResponse, 24);
        diagnostics.Act("file time at offset 24", writtenTime);

        diagnostics.Assert("file time at offset 24", timestamp.ToFileTime(), writtenTime);
        diagnostics.Assert("NT response length", 16 + 28 + 4, responses.NtChallengeResponse.Length);
        Assert.AreEqual(timestamp.ToFileTime(), writtenTime);
        Assert.HasCount(16 + 28 + 4, responses.NtChallengeResponse);
    }

    [TestMethod]
    public void Compute_ChallengeNotEightBytes_Throws()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] seven = new byte[7];
        diagnostics.Arrange("short challenge length", seven.Length);

        ArgumentException[] exceptions =
        [
            Assert.ThrowsExactly<ArgumentException>(() => NtlmResponseComputation.ComputeV1(Password, seven, V1Flags)),
            Assert.ThrowsExactly<ArgumentException>(() => NtlmResponseComputation.ComputeV1WithExtendedSessionSecurity(Password, seven, ClientChallenge)),
            Assert.ThrowsExactly<ArgumentException>(() => NtlmResponseComputation.ComputeV1WithExtendedSessionSecurity(Password, ServerChallenge, seven)),
            Assert.ThrowsExactly<ArgumentException>(() => NtlmResponseComputation.ComputeV2(User, Domain, Password, seven, ClientChallenge, Time, [])),
            Assert.ThrowsExactly<ArgumentException>(() => NtlmResponseComputation.ComputeV2(User, Domain, Password, ServerChallenge, seven, Time, [])),
        ];
        diagnostics.Act("exception parameters", string.Join(", ", exceptions.Select(exception => exception.ParamName)));

        diagnostics.Assert("exceptions thrown", 5, exceptions.Length);
    }

    [TestMethod]
    public void EncryptSessionKey_KeyNotSixteenBytes_Throws()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("key lengths", "15-byte key exchange key; 17-byte random session key");

        ArgumentException[] exceptions =
        [
            Assert.ThrowsExactly<ArgumentException>(() => NtlmResponseComputation.EncryptSessionKey(new byte[15], RandomSessionKey)),
            Assert.ThrowsExactly<ArgumentException>(() => NtlmResponseComputation.EncryptSessionKey(RandomSessionKey, new byte[17])),
        ];
        diagnostics.Act("exception parameters", string.Join(", ", exceptions.Select(exception => exception.ParamName)));

        diagnostics.Assert("exceptions thrown", 2, exceptions.Length);
    }

    private static void ArrangeCommonValues(TestDiagnostics diagnostics, NtlmNegotiateFlags? flags)
    {
        diagnostics.Arrange("user, domain, password", $"{User}, {Domain}, {Password}");
        if (flags is { } value)
        {
            diagnostics.Arrange("flags", string.Create(CultureInfo.InvariantCulture, $"0x{(uint)value:X8} ({value})"));
        }

        diagnostics.Bytes("server challenge", ServerChallenge);
        diagnostics.Bytes("client challenge", ClientChallenge);
        diagnostics.Bytes("random session key", RandomSessionKey);
    }

    private static void ActResponses(TestDiagnostics diagnostics, NtlmResponses responses)
    {
        diagnostics.Act("LM response", Hex(responses.LmChallengeResponse));
        diagnostics.Bytes("NT response", responses.NtChallengeResponse);
        diagnostics.Act("session base key", Hex(responses.SessionBaseKey));
        diagnostics.Act("key exchange key", Hex(responses.KeyExchangeKey));
    }

    private static string Hex(byte[] bytes) => Convert.ToHexStringLower(bytes);
}
