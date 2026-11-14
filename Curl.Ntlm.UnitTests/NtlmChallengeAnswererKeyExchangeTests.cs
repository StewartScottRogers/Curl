namespace Curl.Ntlm;

/// <summary>Checks <see cref="NtlmChallengeAnswerer.AnswerWithKeyExchange" />.</summary>
[TestClass]
public sealed class NtlmChallengeAnswererKeyExchangeTests
{
    private const NtlmNegotiateFlags V2 = NtlmNegotiateFlags.NegotiateUnicode | NtlmNegotiateFlags.NegotiateNtlm | NtlmNegotiateFlags.NegotiateExtendedSessionSecurity;

    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 34, 56, TimeSpan.Zero);

    private static readonly byte[] ClientChallenge = [0, 1, 2, 3, 4, 5, 6, 7];

    [TestMethod]
    public void AnswerWithKeyExchange_KeyExchangeNegotiated_WritesTheEncryptedSessionKey()
    {
        NtlmChallengeMessage challenge = new(V2 | NtlmNegotiateFlags.NegotiateKeyExchange, new byte[8], [], [], []);
        NtlmChallengeAnswerer answerer = new(new Clock(), new Counting());

        NtlmAuthenticateMessage message = answerer.AnswerWithKeyExchange(challenge, "u", "pw", out byte[] exported);

        NtlmResponses responses = NtlmResponseComputation.ComputeV2("u", string.Empty, "pw", new byte[8], ClientChallenge, Now, []);
        CollectionAssert.AreEqual(Enumerable.Range(8, 16).Select(value => (byte)value).ToArray(), exported);
        CollectionAssert.AreEqual(NtlmResponseComputation.EncryptSessionKey(responses.KeyExchangeKey, exported), message.EncryptedRandomSessionKey);
        Assert.IsTrue(message.TryEncode(out byte[]? bytes));
        CollectionAssert.AreEqual(message.EncryptedRandomSessionKey, bytes.AsSpan(bytes.Length - 16).ToArray());
        Assert.AreEqual(16, BitConverter.ToUInt16(bytes, 52));
    }

    [TestMethod]
    public void AnswerWithKeyExchange_NotNegotiated_ExportsTheKeyExchangeKeyAndWritesNoKey()
    {
        NtlmChallengeMessage challenge = new(V2, new byte[8], [], [], []);
        NtlmChallengeAnswerer answerer = new(new Clock(), new Counting());

        NtlmAuthenticateMessage message = answerer.AnswerWithKeyExchange(challenge, "u", "pw", out byte[] exported);

        NtlmResponses responses = NtlmResponseComputation.ComputeV2("u", string.Empty, "pw", new byte[8], ClientChallenge, Now, []);
        CollectionAssert.AreEqual(responses.KeyExchangeKey, exported);
        Assert.IsNull(message.EncryptedRandomSessionKey);
    }

    [TestMethod]
    public void Answer_KeyExchangeNegotiated_StillLeavesTheSessionKeyEmpty()
    {
        NtlmChallengeMessage challenge = new(V2 | NtlmNegotiateFlags.NegotiateKeyExchange, new byte[8], [], [], []);
        NtlmChallengeAnswerer answerer = new(new Clock(), new Counting());

        Assert.IsNull(answerer.Answer(challenge, "u", "pw").EncryptedRandomSessionKey);
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    // Hands out 0, 1, 2 and so on: the client challenge is 0 to 7, the session key 8 to 23.
    private sealed class Counting : INtlmRandomSource
    {
        private byte next;

        public void Fill(Span<byte> destination)
        {
            for (int index = 0; index < destination.Length; index++)
            {
                destination[index] = next++;
            }
        }
    }
}
