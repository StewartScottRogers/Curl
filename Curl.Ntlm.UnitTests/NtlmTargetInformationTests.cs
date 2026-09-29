using System.Text;

namespace Curl.Ntlm;

/// <summary>
/// Pins <see cref="NtlmTargetInformation.Decode" /> to the AV_PAIR list of MS-NLMP
/// section 4.2.4.3's CHALLENGE message, and checks a truncated or unterminated list is the
/// typed failure.
/// </summary>
[TestClass]
public sealed class NtlmTargetInformationTests
{
    [TestMethod]
    public void Decode_MsNlmpNtlmV2TargetInformation_ReadsDomainThenServer()
    {
        NtlmTargetInformationDecoding decoding = NtlmTargetInformation.Decode(
            Convert.FromHexString(NtlmChallengeMessageTests.NtlmV2TargetInformation));

        Assert.AreEqual(NtlmMessageFailure.None, decoding.Failure);
        Assert.HasCount(2, decoding.Pairs);
        Assert.AreEqual(NtlmAvId.NetBiosDomainName, decoding.Pairs[0].Id);
        Assert.AreEqual("Domain", Encoding.Unicode.GetString(decoding.Pairs[0].Value));
        Assert.AreEqual(NtlmAvId.NetBiosComputerName, decoding.Pairs[1].Id);
        Assert.AreEqual("Server", Encoding.Unicode.GetString(decoding.Pairs[1].Value));
    }

    [TestMethod]
    public void Decode_UnnamedIdAndBytesAfterEndOfList_KeepsTheIdAndIgnoresTheRest()
    {
        NtlmTargetInformationDecoding decoding = NtlmTargetInformation.Decode(Convert.FromHexString("2A000100FF00000000FFFF"));

        Assert.AreEqual(NtlmMessageFailure.None, decoding.Failure);
        Assert.HasCount(1, decoding.Pairs);
        Assert.AreEqual((NtlmAvId)0x2A, decoding.Pairs[0].Id);
        Assert.AreEqual("FF", Convert.ToHexString(decoding.Pairs[0].Value));
    }

    [TestMethod]
    public void Decode_Empty_FailsAvPairListUnterminated()
    {
        AssertFails(NtlmMessageFailure.AvPairListUnterminated, string.Empty);
    }

    [TestMethod]
    public void Decode_PairsWithoutEndOfList_FailsAvPairListUnterminated()
    {
        AssertFails(NtlmMessageFailure.AvPairListUnterminated, "02000200AAAA");
    }

    [TestMethod]
    public void Decode_ValueRunningPastTheEnd_FailsAvPairTruncated()
    {
        AssertFails(NtlmMessageFailure.AvPairTruncated, "02000300AAAA");
    }

    [TestMethod]
    public void Decode_PartialPairHeader_FailsAvPairTruncated()
    {
        AssertFails(NtlmMessageFailure.AvPairTruncated, "02000200AAAA000000");
    }

    private static void AssertFails(NtlmMessageFailure expected, string hex)
    {
        NtlmTargetInformationDecoding decoding = NtlmTargetInformation.Decode(Convert.FromHexString(hex));

        Assert.AreEqual(expected, decoding.Failure);
        Assert.IsEmpty(decoding.Pairs);
    }
}
