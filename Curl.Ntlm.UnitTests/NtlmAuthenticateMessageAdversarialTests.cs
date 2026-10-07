using System.Buffers.Binary;

namespace Curl.Ntlm;

/// <summary>
/// Adversarial black-box tests (BL-1503, by the method in
/// <c>Documentation/Wiki/Adversarial-Testing.md</c>) that attack the AUTHENTICATE side -
/// <see cref="NtlmChallengeAnswerer.Answer" />, <see cref="NtlmAuthenticateMessage.TryEncode(out byte[])" />
/// and <see cref="NtlmResponseComputation" /> - with empty, very long and non-ASCII
/// credentials, every size limit on both sides, wrong-length challenges, and repeated and
/// concurrent calls. The oracle is curl 8.21.0's <c>lib/vauth/ntlm.c</c> as the types'
/// doc comments describe it.
/// </summary>
[TestClass]
public sealed class NtlmAuthenticateMessageAdversarialTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static readonly byte[] ServerChallenge = Convert.FromHexString("0123456789ABCDEF");

    [TestMethod]
    [DataRow(NtlmNegotiateFlags.NegotiateUnicode)]
    [DataRow(NtlmNegotiateFlags.NegotiateUnicode | NtlmNegotiateFlags.NegotiateExtendedSessionSecurity)]
    public void Answer_EmptyUserAndPassword_EncodesEmptyDomainAndUserBuffers(NtlmNegotiateFlags flags)
    {
        NtlmAuthenticateMessage answer = CreateAnswerer().Answer(Challenge(flags), string.Empty, string.Empty);

        bool encoded = answer.TryEncode(out byte[]? message);

        Assert.IsTrue(encoded);
        Assert.AreEqual(string.Empty, answer.Domain);
        Assert.AreEqual(string.Empty, answer.User);
        Assert.AreEqual(0, BinaryPrimitives.ReadUInt16LittleEndian(message!.AsSpan(28)));
        Assert.AreEqual(0, BinaryPrimitives.ReadUInt16LittleEndian(message!.AsSpan(36)));
    }

    [TestMethod]
    [DataRow(@"\", "", "")]
    [DataRow("/", "", "")]
    [DataRow(@"\\user", "", @"\user")]
    [DataRow(@"a/b\c", "a/b", "c")]
    [DataRow(@"dom\", "dom", "")]
    public void Answer_UserNameMadeOfSeparators_SplitsAtTheFirstBackslashElseSlash(string userName, string domain, string user)
    {
        NtlmAuthenticateMessage answer = CreateAnswerer().Answer(Challenge(NtlmNegotiateFlags.NegotiateUnicode), userName, "pw");

        Assert.AreEqual(domain, answer.Domain);
        Assert.AreEqual(user, answer.User);
    }

    [TestMethod]
    public void Answer_NullUserName_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CreateAnswerer().Answer(Challenge(NtlmNegotiateFlags.None), null!, "pw"));
    }

    [TestMethod]
    [DataRow(NtlmNegotiateFlags.None)]
    [DataRow(NtlmNegotiateFlags.NegotiateExtendedSessionSecurity)]
    public void Answer_NullPassword_ThrowsArgumentNullException(NtlmNegotiateFlags flags)
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CreateAnswerer().Answer(Challenge(flags), "user", null!));
    }

    [TestMethod]
    public void Answer_VeryLongPassword_StillFitsCurlsBuffer()
    {
        NtlmAuthenticateMessage answer = CreateAnswerer().Answer(Challenge(NtlmNegotiateFlags.NegotiateUnicode), "user", new string('p', 100_000));

        Assert.IsTrue(answer.TryEncode(out byte[]? message));
        Assert.HasCount(24, answer.NtChallengeResponse);
        Assert.IsLessThan(NtlmAuthenticateMessage.CurlBufferSize, message!.Length);
    }

    [TestMethod]
    public void Answer_VeryLongUser_IsRefusedAsNamesTooLargeWithoutThrowing()
    {
        NtlmAuthenticateMessage answer = CreateAnswerer().Answer(Challenge(NtlmNegotiateFlags.NegotiateUnicode), new string('u', 100_000), "pw");

        bool encoded = answer.TryEncode(out byte[]? message, out NtlmMessageFailure failure);

        Assert.IsFalse(encoded);
        Assert.IsNull(message);
        Assert.AreEqual(NtlmMessageFailure.NamesTooLarge, failure);
    }

    [TestMethod]
    public void Answer_TargetInformationFillingCurlsBuffer_IsRefusedAsResponsesTooLarge()
    {
        NtlmChallengeMessage challenge = Challenge(NtlmNegotiateFlags.NegotiateExtendedSessionSecurity) with { TargetInformation = new byte[ushort.MaxValue] };

        NtlmAuthenticateMessage answer = CreateAnswerer().Answer(challenge, "user", "pw");
        bool encoded = answer.TryEncode(out _, out NtlmMessageFailure failure);

        Assert.IsFalse(encoded);
        Assert.AreEqual(NtlmMessageFailure.ResponsesTooLarge, failure);
    }

    [TestMethod]
    public void TryEncode_NonAsciiUserWithUnicodeFlag_WidensEachUtf8ByteAsCurlDoes()
    {
        NtlmAuthenticateMessage answer = Message(NtlmNegotiateFlags.NegotiateUnicode, user: "é");

        answer.TryEncode(out byte[]? message);

        Assert.AreEqual("C300A900", Convert.ToHexString(Payload(message!, 36)));
    }

    [TestMethod]
    public void TryEncode_NonAsciiUserWithUnicodeFlagClear_WritesItsUtf8Bytes()
    {
        NtlmAuthenticateMessage answer = Message(NtlmNegotiateFlags.NegotiateOem, user: "é€");

        answer.TryEncode(out byte[]? message);

        Assert.AreEqual("C3A9E282AC", Convert.ToHexString(Payload(message!, 36)));
    }

    [TestMethod]
    public void TryEncode_UnpairedSurrogateInUser_WritesTheUtf8ReplacementCharacter()
    {
        NtlmAuthenticateMessage answer = Message(NtlmNegotiateFlags.None, user: "\uD800");

        answer.TryEncode(out byte[]? message);

        Assert.AreEqual("EFBFBD", Convert.ToHexString(Payload(message!, 36)));
    }

    [TestMethod]
    public void TryEncode_NulInsideUser_KeepsItAndTheBytesAfterIt()
    {
        NtlmAuthenticateMessage answer = Message(NtlmNegotiateFlags.None, user: "a\0b");

        answer.TryEncode(out byte[]? message);

        Assert.AreEqual("610062", Convert.ToHexString(Payload(message!, 36)));
    }

    [TestMethod]
    public void TryEncode_MessageOneByteUnderCurlsBuffer_IsWritten()
    {
        NtlmAuthenticateMessage answer = MessageOfLength(NtlmAuthenticateMessage.CurlBufferSize - 1);

        Assert.IsTrue(answer.TryEncode(out byte[]? message, out NtlmMessageFailure failure));
        Assert.HasCount(NtlmAuthenticateMessage.CurlBufferSize - 1, message!);
        Assert.AreEqual(NtlmMessageFailure.None, failure);
    }

    [TestMethod]
    public void TryEncode_MessageExactlyCurlsBuffer_IsRefusedAsNamesTooLarge()
    {
        NtlmAuthenticateMessage answer = MessageOfLength(NtlmAuthenticateMessage.CurlBufferSize);

        Assert.IsFalse(answer.TryEncode(out _, out NtlmMessageFailure failure));
        Assert.AreEqual(NtlmMessageFailure.NamesTooLarge, failure);
    }

    [TestMethod]
    public void TryEncode_ResponsesEndingExactlyAtCurlsBuffer_IsRefusedAsNamesTooLarge()
    {
        NtlmAuthenticateMessage answer = new(
            NtlmNegotiateFlags.None,
            new byte[24],
            new byte[NtlmAuthenticateMessage.CurlBufferSize - NtlmAuthenticateMessage.HeaderLength - 24],
            string.Empty,
            string.Empty,
            string.Empty);

        Assert.IsFalse(answer.TryEncode(out _, out NtlmMessageFailure failure));
        Assert.AreEqual(NtlmMessageFailure.NamesTooLarge, failure);
    }

    [TestMethod]
    public void TryEncode_ResponsesEndingOneBytePastCurlsBuffer_IsRefusedAsResponsesTooLarge()
    {
        NtlmAuthenticateMessage answer = new(
            NtlmNegotiateFlags.None,
            new byte[24],
            new byte[NtlmAuthenticateMessage.CurlBufferSize - NtlmAuthenticateMessage.HeaderLength - 23],
            string.Empty,
            string.Empty,
            string.Empty);

        Assert.IsFalse(answer.TryEncode(out _, out NtlmMessageFailure failure));
        Assert.AreEqual(NtlmMessageFailure.ResponsesTooLarge, failure);
    }

    [TestMethod]
    public void TryEncode_EmptyResponsesAndNames_WritesAHeaderOnlyMessage()
    {
        NtlmAuthenticateMessage answer = new(NtlmNegotiateFlags.None, [], [], string.Empty, string.Empty, string.Empty);

        Assert.IsTrue(answer.TryEncode(out byte[]? message));
        Assert.HasCount(NtlmAuthenticateMessage.HeaderLength, message!);
    }

    [TestMethod]
    public void TryEncode_CalledTwice_ReturnsEqualBytesInSeparateArrays()
    {
        NtlmAuthenticateMessage answer = Message(NtlmNegotiateFlags.NegotiateUnicode, user: "user");
        answer.TryEncode(out byte[]? first);
        first!.AsSpan().Fill(0xEE);

        answer.TryEncode(out byte[]? second);

        Assert.AreNotSame(first, second);
        Assert.AreEqual((byte)'N', second![0]);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(7)]
    [DataRow(9)]
    public void ComputeV1_ServerChallengeOfWrongLength_ThrowsArgumentException(int length)
    {
        Assert.ThrowsExactly<ArgumentException>(() => NtlmResponseComputation.ComputeV1("pw", new byte[length], NtlmNegotiateFlags.None));
    }

    [TestMethod]
    [DataRow(8, 7)]
    [DataRow(8, 9)]
    [DataRow(0, 8)]
    public void ComputeV2_ChallengeOfWrongLength_ThrowsArgumentException(int serverLength, int clientLength)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            NtlmResponseComputation.ComputeV2("u", "d", "pw", new byte[serverLength], new byte[clientLength], Now, []));
    }

    [TestMethod]
    [DataRow(15, 16)]
    [DataRow(16, 17)]
    public void EncryptSessionKey_KeyOfWrongLength_ThrowsArgumentException(int keyExchangeKeyLength, int sessionKeyLength)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            NtlmResponseComputation.EncryptSessionKey(new byte[keyExchangeKeyLength], new byte[sessionKeyLength]));
    }

    [TestMethod]
    public void ComputeLmOwfV1_PasswordsEqualInTheirFirst14Bytes_HashTheSame()
    {
        byte[] fourteen = NtlmOneWayFunctions.ComputeLmOwfV1("abcdefghijklmn");

        byte[] fifteen = NtlmOneWayFunctions.ComputeLmOwfV1("ABCDEFGHIJKLMNo");

        CollectionAssert.AreEqual(fourteen, fifteen);
    }

    [TestMethod]
    public void ComputeLmOwfV1_PasswordsDifferingInTheirFourteenthByte_HashDifferently()
    {
        byte[] first = NtlmOneWayFunctions.ComputeLmOwfV1("abcdefghijklmn");

        byte[] second = NtlmOneWayFunctions.ComputeLmOwfV1("abcdefghijklmX");

        CollectionAssert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void ComputeNtOwfV2_NonAsciiUser_UppercasesAsciiLettersOnly()
    {
        byte[] ntOwfV1 = NtlmOneWayFunctions.ComputeNtOwfV1("pw");

        byte[] lower = NtlmOneWayFunctions.ComputeNtOwfV2("éa", "dom", ntOwfV1);
        byte[] upperAscii = NtlmOneWayFunctions.ComputeNtOwfV2("éA", "dom", ntOwfV1);
        byte[] upperBoth = NtlmOneWayFunctions.ComputeNtOwfV2("ÉA", "dom", ntOwfV1);

        CollectionAssert.AreEqual(lower, upperAscii);
        CollectionAssert.AreNotEqual(lower, upperBoth);
    }

    [TestMethod]
    public void ComputeV2_CallerChallengeArrays_AreLeftUnchanged()
    {
        byte[] server = [.. ServerChallenge];
        byte[] client = Convert.FromHexString("FFFFFF0011223344");
        byte[] targetInformation = [2, 0, 0, 0, 0, 0, 0, 0];

        NtlmResponseComputation.ComputeV2("u", "d", "pw", server, client, Now, targetInformation);

        CollectionAssert.AreEqual(ServerChallenge, server);
        Assert.AreEqual("FFFFFF0011223344", Convert.ToHexString(client));
        Assert.AreEqual("0200000000000000", Convert.ToHexString(targetInformation));
    }

    [TestMethod]
    public void Answer_TwoClientChallenges_GiveDifferentNtlmV2Responses()
    {
        NtlmChallengeMessage challenge = Challenge(NtlmNegotiateFlags.NegotiateExtendedSessionSecurity);

        NtlmAuthenticateMessage first = new NtlmChallengeAnswerer(new FixedTimeProvider(Now), new FixedRandomSource(0x11)).Answer(challenge, "u", "pw");
        NtlmAuthenticateMessage second = new NtlmChallengeAnswerer(new FixedTimeProvider(Now), new FixedRandomSource(0x22)).Answer(challenge, "u", "pw");

        CollectionAssert.AreNotEqual(first.NtChallengeResponse, second.NtChallengeResponse);
        CollectionAssert.AreNotEqual(first.LmChallengeResponse, second.LmChallengeResponse);
    }

    [TestMethod]
    public void Answer_SubSecondClockDifferences_GiveTheSameNtlmV2Response()
    {
        NtlmChallengeMessage challenge = Challenge(NtlmNegotiateFlags.NegotiateExtendedSessionSecurity);

        NtlmAuthenticateMessage first = new NtlmChallengeAnswerer(new FixedTimeProvider(Now), new FixedRandomSource(0x11)).Answer(challenge, "u", "pw");
        NtlmAuthenticateMessage second = new NtlmChallengeAnswerer(new FixedTimeProvider(Now.AddMilliseconds(999)), new FixedRandomSource(0x11)).Answer(challenge, "u", "pw");

        CollectionAssert.AreEqual(first.NtChallengeResponse, second.NtChallengeResponse);
    }

    [TestMethod]
    [DataRow(NtlmNegotiateFlags.NegotiateUnicode)]
    [DataRow(NtlmNegotiateFlags.NegotiateUnicode | NtlmNegotiateFlags.NegotiateExtendedSessionSecurity)]
    public void Answer_ManyConcurrentCallersOnOneAnswerer_AnswerAsOneAfterAnother(NtlmNegotiateFlags flags)
    {
        NtlmChallengeAnswerer answerer = CreateAnswerer();
        NtlmChallengeMessage challenge = Challenge(flags);
        string[] sequential = new string[64];
        for (int index = 0; index < sequential.Length; index++)
        {
            sequential[index] = Encoded(answerer.Answer(challenge, $"dom\\user{index}", $"pw{index}"));
        }

        string[] concurrent = new string[sequential.Length];
        Parallel.For(0, concurrent.Length, index => concurrent[index] = Encoded(answerer.Answer(challenge, $"dom\\user{index}", $"pw{index}")));

        CollectionAssert.AreEqual(sequential, concurrent);
    }

    private static NtlmChallengeAnswerer CreateAnswerer() => new(new FixedTimeProvider(Now), new FixedRandomSource(0x5A));

    private static NtlmChallengeMessage Challenge(NtlmNegotiateFlags flags) => new(flags, [.. ServerChallenge], [], [], []);

    private static NtlmAuthenticateMessage Message(NtlmNegotiateFlags flags, string user) =>
        new(flags, new byte[24], new byte[24], string.Empty, user, NtlmAuthenticateMessage.CurlWorkstation);

    // Header 64, responses 48, workstation 11: the user fills the rest to totalLength.
    private static NtlmAuthenticateMessage MessageOfLength(int totalLength) =>
        new(
            NtlmNegotiateFlags.None,
            new byte[24],
            new byte[24],
            string.Empty,
            new string('u', totalLength - NtlmAuthenticateMessage.HeaderLength - 48 - NtlmAuthenticateMessage.CurlWorkstation.Length),
            NtlmAuthenticateMessage.CurlWorkstation);

    private static byte[] Payload(byte[] message, int field)
    {
        int length = BinaryPrimitives.ReadUInt16LittleEndian(message.AsSpan(field));
        int offset = (int)BinaryPrimitives.ReadUInt32LittleEndian(message.AsSpan(field + 4));
        return message.AsSpan(offset, length).ToArray();
    }

    private static string Encoded(NtlmAuthenticateMessage answer) =>
        answer.TryEncode(out byte[]? message) ? Convert.ToHexString(message) : "refused";

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FixedRandomSource(byte fill) : INtlmRandomSource
    {
        public void Fill(Span<byte> destination) => destination.Fill(fill);
    }
}
