using System.Text;
using Curl.Testing;

namespace Curl.Ntlm;

/// <summary>
/// Pins <see cref="NtlmTargetInformation.Decode" /> to the AV_PAIR list of MS-NLMP
/// section 4.2.4.3's CHALLENGE message, and checks a truncated or unterminated list is the
/// typed failure.
/// </summary>
[TestClass]
public sealed class NtlmTargetInformationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Decode_MsNlmpNtlmV2TargetInformation_ReadsDomainThenServer()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] targetInformation = Convert.FromHexString(NtlmChallengeMessageTests.NtlmV2TargetInformation);
        diagnostics.Bytes("target information", targetInformation);
        diagnostics.Arrange("target information", "MS-NLMP 4.2.4.3's AV_PAIR list");

        NtlmTargetInformationDecoding decoding = NtlmTargetInformation.Decode(targetInformation);
        ActDecoding(diagnostics, decoding);

        diagnostics.Assert("failure", NtlmMessageFailure.None, decoding.Failure);
        diagnostics.Assert("pair count", 2, decoding.Pairs.Count);
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
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] targetInformation = Convert.FromHexString("2A000100FF00000000FFFF");
        diagnostics.Bytes("target information", targetInformation);
        diagnostics.Arrange("target information", "AV_PAIR 0x2A with value FF, MsvAvEOL, then FFFF");

        NtlmTargetInformationDecoding decoding = NtlmTargetInformation.Decode(targetInformation);
        ActDecoding(diagnostics, decoding);

        diagnostics.Assert("failure", NtlmMessageFailure.None, decoding.Failure);
        diagnostics.Assert("pair count", 1, decoding.Pairs.Count);
        Assert.AreEqual(NtlmMessageFailure.None, decoding.Failure);
        Assert.HasCount(1, decoding.Pairs);
        Assert.AreEqual((NtlmAvId)0x2A, decoding.Pairs[0].Id);
        Assert.AreEqual("FF", Convert.ToHexString(decoding.Pairs[0].Value));
    }

    [TestMethod]
    public void Decode_Empty_FailsAvPairListUnterminated()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        AssertFails(diagnostics, NtlmMessageFailure.AvPairListUnterminated, string.Empty);
    }

    [TestMethod]
    public void Decode_PairsWithoutEndOfList_FailsAvPairListUnterminated()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        AssertFails(diagnostics, NtlmMessageFailure.AvPairListUnterminated, "02000200AAAA");
    }

    [TestMethod]
    public void Decode_ValueRunningPastTheEnd_FailsAvPairTruncated()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        AssertFails(diagnostics, NtlmMessageFailure.AvPairTruncated, "02000300AAAA");
    }

    [TestMethod]
    public void Decode_PartialPairHeader_FailsAvPairTruncated()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        AssertFails(diagnostics, NtlmMessageFailure.AvPairTruncated, "02000200AAAA000000");
    }

    private static void ActDecoding(TestDiagnostics diagnostics, NtlmTargetInformationDecoding decoding)
    {
        diagnostics.Act("failure", decoding.Failure);
        diagnostics.Act("pair count", decoding.Pairs.Count);
        for (int index = 0; index < decoding.Pairs.Count; index++)
        {
            diagnostics.Bytes($"pair {index} ({decoding.Pairs[index].Id}) value", decoding.Pairs[index].Value);
        }
    }

    private static void AssertFails(TestDiagnostics diagnostics, NtlmMessageFailure expected, string hex)
    {
        byte[] targetInformation = Convert.FromHexString(hex);
        diagnostics.Bytes("target information", targetInformation);
        diagnostics.Arrange("target information hex", hex.Length == 0 ? "(empty)" : hex);

        NtlmTargetInformationDecoding decoding = NtlmTargetInformation.Decode(targetInformation);
        ActDecoding(diagnostics, decoding);

        diagnostics.Assert("failure", expected, decoding.Failure);
        diagnostics.Assert("pair count", 0, decoding.Pairs.Count);
        Assert.AreEqual(expected, decoding.Failure);
        Assert.IsEmpty(decoding.Pairs);
    }
}
