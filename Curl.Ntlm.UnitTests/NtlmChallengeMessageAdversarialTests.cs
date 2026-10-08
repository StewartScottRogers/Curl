using System.Buffers.Binary;

namespace Curl.Ntlm;

/// <summary>
/// Adversarial black-box tests (BL-1503, by the method in
/// <c>Documentation/Wiki/Adversarial-Testing.md</c>) that attack
/// <see cref="NtlmChallengeMessage.Decode" /> and <see cref="NtlmTargetInformation.Decode" />
/// with CHALLENGE messages and AV_PAIR lists built to break them: every limit on both sides,
/// offsets and lengths that point outside the message or into its header, malformed and
/// duplicated pairs, and seeded random mutations. The oracle is MS-NLMP section 2.2 and the
/// libraries' documented contract: a typed <see cref="NtlmMessageFailure" />, never an exception.
/// </summary>
[TestClass]
public sealed class NtlmChallengeMessageAdversarialTests
{
    private const NtlmNegotiateFlags TargetInfoFlags = NtlmNegotiateFlags.NegotiateUnicode | NtlmNegotiateFlags.NegotiateTargetInfo;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Decode_EmptyMessage_RefusesAsTooShort()
    {
        Assert.AreEqual(NtlmMessageFailure.TooShort, NtlmChallengeMessage.Decode([]).Failure);
    }

    [TestMethod]
    public void Decode_OneByteShorterThanMinimum_RefusesAsTooShort()
    {
        byte[] message = Challenge(NtlmChallengeMessage.MinimumLength, NtlmNegotiateFlags.None);

        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(message.AsSpan(0, NtlmChallengeMessage.MinimumLength - 1));

        Assert.AreEqual(NtlmMessageFailure.TooShort, decoding.Failure);
        Assert.IsNull(decoding.Message);
    }

    [TestMethod]
    public void Decode_ExactlyMinimumLengthWithTargetInfoFlag_DecodesWithoutTargetInformation()
    {
        byte[] message = Challenge(NtlmChallengeMessage.MinimumLength, TargetInfoFlags);

        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(message);

        Assert.AreEqual(NtlmMessageFailure.None, decoding.Failure);
        Assert.IsEmpty(decoding.Message!.TargetInformation);
        Assert.HasCount(NtlmResponseComputation.ChallengeLength, decoding.Message.ServerChallenge);
    }

    [TestMethod]
    public void Decode_OneByteShorterThanTargetInfoHeaderWithBogusTargetInfo_IgnoresTheTargetInfoField()
    {
        byte[] message = Challenge(NtlmChallengeMessage.TargetInformationHeaderLength - 1, TargetInfoFlags);

        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(message);

        Assert.AreEqual(NtlmMessageFailure.None, decoding.Failure);
        Assert.IsEmpty(decoding.Message!.TargetInformation);
    }

    [TestMethod]
    public void Decode_SignatureOneByteWrong_RefusesAsWrongSignatureOrType()
    {
        byte[] message = Challenge(NtlmChallengeMessage.MinimumLength, NtlmNegotiateFlags.None);
        message[6] = (byte)'s';

        Assert.AreEqual(NtlmMessageFailure.WrongSignatureOrType, NtlmChallengeMessage.Decode(message).Failure);
    }

    [TestMethod]
    [DataRow(0u)]
    [DataRow(1u)]
    [DataRow(3u)]
    [DataRow(0x0100_0002u)]
    [DataRow(uint.MaxValue)]
    public void Decode_MessageTypeOtherThanChallenge_RefusesAsWrongSignatureOrType(uint messageType)
    {
        byte[] message = Challenge(NtlmChallengeMessage.MinimumLength, NtlmNegotiateFlags.None);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(8), messageType);

        Assert.AreEqual(NtlmMessageFailure.WrongSignatureOrType, NtlmChallengeMessage.Decode(message).Failure);
    }

    [TestMethod]
    public void Decode_TargetInfoEndingExactlyAtMessageEnd_ReadsIt()
    {
        byte[] message = Challenge(52, TargetInfoFlags);
        SetSecurityBuffer(message, 40, 4, 48);
        message[48] = 0xAA;
        message[51] = 0xBB;

        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(message);

        Assert.AreEqual(NtlmMessageFailure.None, decoding.Failure);
        Assert.AreEqual("AA0000BB", Convert.ToHexString(decoding.Message!.TargetInformation));
    }

    [TestMethod]
    public void Decode_TargetInfoEndingOneBytePastMessageEnd_RefusesAsOutOfRange()
    {
        byte[] message = Challenge(52, TargetInfoFlags);
        SetSecurityBuffer(message, 40, 5, 48);

        Assert.AreEqual(NtlmMessageFailure.TargetInfoOutOfRange, NtlmChallengeMessage.Decode(message).Failure);
    }

    [TestMethod]
    public void Decode_TargetInfoStartingOneByteInsideHeader_RefusesAsOutOfRange()
    {
        byte[] message = Challenge(52, TargetInfoFlags);
        SetSecurityBuffer(message, 40, 4, NtlmChallengeMessage.TargetInformationHeaderLength - 1);

        Assert.AreEqual(NtlmMessageFailure.TargetInfoOutOfRange, NtlmChallengeMessage.Decode(message).Failure);
    }

    [TestMethod]
    public void Decode_TargetInfoOverlappingItsOwnSecurityBuffer_RefusesAsOutOfRange()
    {
        byte[] message = Challenge(64, TargetInfoFlags);
        SetSecurityBuffer(message, 40, 8, 40);

        Assert.AreEqual(NtlmMessageFailure.TargetInfoOutOfRange, NtlmChallengeMessage.Decode(message).Failure);
    }

    [TestMethod]
    [DataRow(0x7FFF_FFFFu)]
    [DataRow(0x8000_0000u)]
    [DataRow(uint.MaxValue - 3)]
    [DataRow(uint.MaxValue)]
    public void Decode_TargetInfoOffsetNearUInt32Max_RefusesWithoutOverflowing(uint offset)
    {
        byte[] message = Challenge(64, TargetInfoFlags);
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(40), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(44), offset);

        Assert.AreEqual(NtlmMessageFailure.TargetInfoOutOfRange, NtlmChallengeMessage.Decode(message).Failure);
    }

    [TestMethod]
    public void Decode_TargetInfoOfMaximumLengthFillingTheMessage_ReadsAllOfIt()
    {
        byte[] message = Challenge(NtlmChallengeMessage.TargetInformationHeaderLength + ushort.MaxValue, TargetInfoFlags);
        SetSecurityBuffer(message, 40, ushort.MaxValue, NtlmChallengeMessage.TargetInformationHeaderLength);

        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(message);

        Assert.AreEqual(NtlmMessageFailure.None, decoding.Failure);
        Assert.HasCount(ushort.MaxValue, decoding.Message!.TargetInformation);
    }

    [TestMethod]
    public void Decode_ZeroLengthTargetInfoAtAnOutOfRangeOffset_DecodesWithoutTargetInformation()
    {
        byte[] message = Challenge(64, TargetInfoFlags);
        SetSecurityBuffer(message, 40, 0, int.MaxValue);

        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(message);

        Assert.AreEqual(NtlmMessageFailure.None, decoding.Failure);
        Assert.IsEmpty(decoding.Message!.TargetInformation);
    }

    [TestMethod]
    public void Decode_OutOfRangeTargetInfoWithTargetInfoFlagClear_IgnoresIt()
    {
        byte[] message = Challenge(64, NtlmNegotiateFlags.NegotiateUnicode);
        SetSecurityBuffer(message, 40, 100, 1000);

        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(message);

        Assert.AreEqual(NtlmMessageFailure.None, decoding.Failure);
        Assert.IsEmpty(decoding.Message!.TargetInformation);
    }

    [TestMethod]
    [DataRow(0u, (ushort)12)]
    [DataRow(60u, (ushort)4)]
    [DataRow(60u, (ushort)5)]
    [DataRow(uint.MaxValue, (ushort)1)]
    [DataRow(0x8000_0000u, ushort.MaxValue)]
    public void Decode_TargetNameAtAnyOffset_ReadsItOnlyWhenWhollyInsideTheMessage(uint offset, ushort length)
    {
        byte[] message = Challenge(64, NtlmNegotiateFlags.NegotiateUnicode);
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(12), length);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(16), offset);

        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(message);

        Assert.AreEqual(NtlmMessageFailure.None, decoding.Failure);
        if (offset + (long)length <= message.Length)
        {
            Assert.HasCount(length, decoding.Message!.TargetName);
        }
        else
        {
            Assert.IsEmpty(decoding.Message!.TargetName);
        }
    }

    [TestMethod]
    public void Decode_TargetNamePointingIntoTheHeader_ReadsThoseBytesAsCurlDoes()
    {
        byte[] message = Challenge(64, NtlmNegotiateFlags.NegotiateOem);
        SetSecurityBuffer(message, 12, 8, 0);

        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(message);

        Assert.AreEqual(NtlmMessageFailure.None, decoding.Failure);
        Assert.AreEqual("NTLMSSP\0", System.Text.Encoding.ASCII.GetString(decoding.Message!.TargetName));
    }

    [TestMethod]
    public void Decode_VersionFlagWithOneByteTooFewForVersion_LeavesVersionEmpty()
    {
        byte[] message = Challenge(55, NtlmNegotiateFlags.NegotiateVersion);

        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(message);

        Assert.AreEqual(NtlmMessageFailure.None, decoding.Failure);
        Assert.IsEmpty(decoding.Message!.Version);
    }

    [TestMethod]
    public void Decode_VersionFlagWithExactlyEnoughForVersion_ReadsVersion()
    {
        byte[] message = Challenge(56, NtlmNegotiateFlags.NegotiateVersion);
        message[48] = 0x0A;
        message[55] = 0x0F;

        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(message);

        Assert.AreEqual("0A0000000000000F", Convert.ToHexString(decoding.Message!.Version));
    }

    [TestMethod]
    [DataRow(NtlmNegotiateFlags.None)]
    [DataRow(NtlmNegotiateFlags.NegotiateUnicode)]
    [DataRow(NtlmNegotiateFlags.NegotiateOem)]
    [DataRow(NtlmNegotiateFlags.NegotiateUnicode | NtlmNegotiateFlags.NegotiateOem)]
    [DataRow((NtlmNegotiateFlags)uint.MaxValue)]
    public void Decode_AnyUnicodeAndOemCombination_KeepsTheFlagsAndTargetNameBytesAsSent(NtlmNegotiateFlags flags)
    {
        byte[] message = Challenge(70, flags & ~NtlmNegotiateFlags.NegotiateTargetInfo);
        SetSecurityBuffer(message, 12, 6, 64);
        message[64] = 0xFF;
        message[69] = 0x80;

        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(message);

        Assert.AreEqual(flags & ~NtlmNegotiateFlags.NegotiateTargetInfo, decoding.Message!.Flags);
        Assert.AreEqual("FF0000000080", Convert.ToHexString(decoding.Message.TargetName));
    }

    [TestMethod]
    public void Decode_InputChangedAfterDecoding_LeavesTheDecodedMessageUnchanged()
    {
        byte[] message = Challenge(60, TargetInfoFlags);
        SetSecurityBuffer(message, 40, 4, 56);
        message[24] = 0x11;
        message[56] = 0x22;
        NtlmChallengeMessage decoded = NtlmChallengeMessage.Decode(message).Message!;

        message.AsSpan(24).Fill(0xEE);

        Assert.AreEqual("1100000000000000", Convert.ToHexString(decoded.ServerChallenge));
        Assert.AreEqual("22000000", Convert.ToHexString(decoded.TargetInformation));
    }

    [TestMethod]
    public void Decode_SameMessageTwiceAfterChangingTheFirstResult_ReturnsTheOriginalBytes()
    {
        byte[] message = Challenge(60, TargetInfoFlags);
        SetSecurityBuffer(message, 40, 4, 56);
        NtlmChallengeMessage first = NtlmChallengeMessage.Decode(message).Message!;
        first.ServerChallenge.AsSpan().Fill(0xEE);
        first.TargetInformation.AsSpan().Fill(0xEE);

        NtlmChallengeMessage second = NtlmChallengeMessage.Decode(message).Message!;

        Assert.AreEqual("0000000000000000", Convert.ToHexString(second.ServerChallenge));
        Assert.AreEqual("00000000", Convert.ToHexString(second.TargetInformation));
    }

    [TestMethod]
    public void Decode_ManyConcurrentCallers_AllReadTheSameMessage()
    {
        byte[] message = Challenge(60, TargetInfoFlags);
        SetSecurityBuffer(message, 40, 4, 56);
        message[24] = 0x5A;
        string expected = Convert.ToHexString(NtlmChallengeMessage.Decode(message).Message!.ServerChallenge);

        string[] results = new string[64];
        Parallel.For(0, results.Length, index => results[index] = Convert.ToHexString(NtlmChallengeMessage.Decode(message).Message!.ServerChallenge));

        CollectionAssert.AreEqual(Enumerable.Repeat(expected, results.Length).ToArray(), results);
    }

    [TestMethod]
    public void Decode_SeededRandomMutationsOfAValidChallenge_NeverThrow()
    {
        // Seed fixed so a failure reproduces; printed when the test fails.
        const int Seed = 1503;
        Random random = new(Seed);
        byte[] valid = Challenge(96, TargetInfoFlags | NtlmNegotiateFlags.NegotiateVersion);
        SetSecurityBuffer(valid, 12, 8, 56);
        SetSecurityBuffer(valid, 40, 32, 64);
        int iteration = 0;
        try
        {
            for (iteration = 0; iteration < 5000; iteration++)
            {
                byte[] mutated = Mutate(random, valid);
                NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(mutated);
                if (decoding.Message is { } decoded)
                {
                    NtlmTargetInformation.Decode(decoded.TargetInformation);
                    Assert.AreEqual(NtlmMessageFailure.None, decoding.Failure);
                }
                else
                {
                    Assert.AreNotEqual(NtlmMessageFailure.None, decoding.Failure);
                }
            }
        }
        catch (Exception)
        {
            TestContext.WriteLine($"seed {Seed}, iteration {iteration}");
            throw;
        }
    }

    [TestMethod]
    public void DecodeTargetInformation_Empty_RefusesAsUnterminated()
    {
        Assert.AreEqual(NtlmMessageFailure.AvPairListUnterminated, NtlmTargetInformation.Decode([]).Failure);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void DecodeTargetInformation_PairHeaderCutShort_RefusesAsTruncated(int length)
    {
        NtlmTargetInformationDecoding decoding = NtlmTargetInformation.Decode(new byte[length]);

        Assert.AreEqual(NtlmMessageFailure.AvPairTruncated, decoding.Failure);
        Assert.IsEmpty(decoding.Pairs);
    }

    [TestMethod]
    public void DecodeTargetInformation_ValueOneBytePastTheEnd_RefusesAsTruncated()
    {
        byte[] list = [.. Pair(NtlmAvId.NetBiosComputerName, 3), 0x41, 0x42];

        Assert.AreEqual(NtlmMessageFailure.AvPairTruncated, NtlmTargetInformation.Decode(list).Failure);
    }

    [TestMethod]
    public void DecodeTargetInformation_ValueEndingExactlyAtTheEndWithNoEndOfList_RefusesAsUnterminated()
    {
        byte[] list = [.. Pair(NtlmAvId.NetBiosComputerName, 2), 0x41, 0x42];

        Assert.AreEqual(NtlmMessageFailure.AvPairListUnterminated, NtlmTargetInformation.Decode(list).Failure);
    }

    [TestMethod]
    public void DecodeTargetInformation_ValueLengthOfUInt16Max_RefusesAsTruncatedWithoutReadingPastTheEnd()
    {
        byte[] list = [.. Pair(NtlmAvId.DnsTreeName, ushort.MaxValue), .. Pair(NtlmAvId.EndOfList, 0)];

        Assert.AreEqual(NtlmMessageFailure.AvPairTruncated, NtlmTargetInformation.Decode(list).Failure);
    }

    [TestMethod]
    public void DecodeTargetInformation_EndOfListOnly_DecodesNoPairs()
    {
        NtlmTargetInformationDecoding decoding = NtlmTargetInformation.Decode(Pair(NtlmAvId.EndOfList, 0));

        Assert.AreEqual(NtlmMessageFailure.None, decoding.Failure);
        Assert.IsEmpty(decoding.Pairs);
    }

    [TestMethod]
    public void DecodeTargetInformation_EndOfListWithANonZeroLengthAndNoValue_StopsThere()
    {
        NtlmTargetInformationDecoding decoding = NtlmTargetInformation.Decode(Pair(NtlmAvId.EndOfList, 500));

        Assert.AreEqual(NtlmMessageFailure.None, decoding.Failure);
        Assert.IsEmpty(decoding.Pairs);
    }

    [TestMethod]
    public void DecodeTargetInformation_GarbageAfterEndOfList_IsIgnored()
    {
        byte[] list = [.. Pair(NtlmAvId.Flags, 4), 1, 0, 0, 0, .. Pair(NtlmAvId.EndOfList, 0), 0x07, 0x00, 0xFF, 0xFF, 0x01];

        NtlmTargetInformationDecoding decoding = NtlmTargetInformation.Decode(list);

        Assert.AreEqual(NtlmMessageFailure.None, decoding.Failure);
        Assert.HasCount(1, decoding.Pairs);
        Assert.AreEqual(NtlmAvId.Flags, decoding.Pairs[0].Id);
    }

    [TestMethod]
    public void DecodeTargetInformation_DuplicatedPairs_KeepsEveryOneInMessageOrder()
    {
        byte[] list =
        [
            .. Pair(NtlmAvId.Timestamp, 1), 0x01,
            .. Pair(NtlmAvId.Timestamp, 1), 0x02,
            .. Pair(NtlmAvId.Timestamp, 0),
            .. Pair(NtlmAvId.EndOfList, 0),
        ];

        NtlmTargetInformationDecoding decoding = NtlmTargetInformation.Decode(list);

        Assert.HasCount(3, decoding.Pairs);
        Assert.AreEqual("01", Convert.ToHexString(decoding.Pairs[0].Value));
        Assert.AreEqual("02", Convert.ToHexString(decoding.Pairs[1].Value));
        Assert.IsEmpty(decoding.Pairs[2].Value);
    }

    [TestMethod]
    [DataRow((ushort)11)]
    [DataRow((ushort)0x8000)]
    [DataRow(ushort.MaxValue)]
    public void DecodeTargetInformation_UnknownAvId_KeepsItAsItsNumber(ushort id)
    {
        byte[] list = [.. Pair((NtlmAvId)id, 1), 0x7F, .. Pair(NtlmAvId.EndOfList, 0)];

        NtlmTargetInformationDecoding decoding = NtlmTargetInformation.Decode(list);

        Assert.AreEqual(NtlmMessageFailure.None, decoding.Failure);
        Assert.AreEqual(id, (ushort)decoding.Pairs[0].Id);
    }

    [TestMethod]
    public void DecodeTargetInformation_PairAfterAFailure_DoesNotLeakPartialPairs()
    {
        byte[] list = [.. Pair(NtlmAvId.NetBiosDomainName, 1), 0x41, .. Pair(NtlmAvId.DnsDomainName, 9), 0x42];

        NtlmTargetInformationDecoding decoding = NtlmTargetInformation.Decode(list);

        Assert.AreEqual(NtlmMessageFailure.AvPairTruncated, decoding.Failure);
        Assert.IsEmpty(decoding.Pairs);
    }

    [TestMethod]
    public void DecodeTargetInformation_SeededRandomBytes_NeverThrowAndFailOnlyWithAListFailure()
    {
        // Seed fixed so a failure reproduces; printed when the test fails.
        const int Seed = 15031;
        Random random = new(Seed);
        int iteration = 0;
        try
        {
            for (iteration = 0; iteration < 5000; iteration++)
            {
                byte[] list = new byte[random.Next(0, 64)];
                random.NextBytes(list);
                for (int index = 0; index + 3 < list.Length; index += 4)
                {
                    // Keep lengths small often enough that whole pairs and EOLs appear.
                    list[index + 3] = 0;
                    list[index + 2] = (byte)(list[index + 2] % 8);
                }

                NtlmMessageFailure failure = NtlmTargetInformation.Decode(list).Failure;
                Assert.Contains(failure, [NtlmMessageFailure.None, NtlmMessageFailure.AvPairTruncated, NtlmMessageFailure.AvPairListUnterminated]);
            }
        }
        catch (Exception)
        {
            TestContext.WriteLine($"seed {Seed}, iteration {iteration}");
            throw;
        }
    }

    private static byte[] Challenge(int length, NtlmNegotiateFlags flags)
    {
        byte[] message = new byte[length];
        "NTLMSSP\0"u8.CopyTo(message);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(8), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(20), (uint)flags);
        return message;
    }

    private static void SetSecurityBuffer(byte[] message, int field, int length, int offset)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(field), (ushort)length);
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(field + 2), (ushort)length);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(field + 4), (uint)offset);
    }

    private static byte[] Pair(NtlmAvId id, int length)
    {
        byte[] header = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(header, (ushort)id);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(2), (ushort)length);
        return header;
    }

    // One of: a byte flipped, the message cut short, or a security buffer field overwritten.
    private static byte[] Mutate(Random random, byte[] valid)
    {
        byte[] mutated = [.. valid];
        switch (random.Next(3))
        {
            case 0:
                mutated[random.Next(mutated.Length)] ^= (byte)random.Next(1, 256);
                return mutated;
            case 1:
                return mutated[..random.Next(mutated.Length)];
            default:
                int field = random.Next(5) switch { 0 => 12, 1 => 16, 2 => 40, 3 => 44, _ => 20 };
                BinaryPrimitives.WriteUInt32LittleEndian(mutated.AsSpan(field), (uint)random.NextInt64(0, uint.MaxValue + 1L));
                return mutated;
        }
    }
}
