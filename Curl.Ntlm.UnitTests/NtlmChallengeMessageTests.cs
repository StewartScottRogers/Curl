using System.Buffers.Binary;
using System.Text;

namespace Curl.Ntlm;

/// <summary>
/// Pins <see cref="NtlmChallengeMessage.Decode" /> to MS-NLMP section 4.2's example
/// CHALLENGE messages, and checks it rejects what curl 8.21.0's
/// <c>Curl_auth_decode_ntlm_type2_message</c> rejects with the typed failure rather than an
/// out-of-range read.
/// </summary>
[TestClass]
public sealed class NtlmChallengeMessageTests
{
    // MS-NLMP 4.2.4.1.3's target information: MsvAvNbDomainName "Domain",
    // MsvAvNbComputerName "Server", MsvAvEOL.
    internal const string NtlmV2TargetInformation =
        "02000C0044006F006D00610069006E00" +
        "01000C00530065007200760065007200" +
        "00000000";

    // MS-NLMP 4.2.2.3, the NTLMv1 CHALLENGE message: target name "Server", flags
    // 0xE2028233, challenge 0123456789ABCDEF, no target information, a VERSION block.
    private const string NtlmV1Challenge =
        "4E544C4D53535000020000000C000C0038000000338202E20123456789ABCDEF" +
        "00000000000000000000000000000000" +
        "060070170000000F" +
        "530065007200760065007200";

    // MS-NLMP 4.2.4.3, the NTLMv2 CHALLENGE message: as above with flags 0xE28A8233 and
    // 36 bytes of target information at offset 0x44.
    private const string NtlmV2Challenge =
        "4E544C4D53535000020000000C000C003800000033828AE20123456789ABCDEF" +
        "00000000000000002400240044000000" +
        "060070170000000F" +
        "530065007200760065007200" +
        NtlmV2TargetInformation;

    [TestMethod]
    public void Decode_MsNlmpNtlmV1Challenge_ReadsEveryField()
    {
        NtlmChallengeMessage message = Decoded(NtlmV1Challenge);

        Assert.AreEqual(0xE2028233u, (uint)message.Flags);
        Assert.AreEqual("0123456789ABCDEF", Convert.ToHexString(message.ServerChallenge));
        Assert.AreEqual("Server", Encoding.Unicode.GetString(message.TargetName));
        Assert.IsEmpty(message.TargetInformation);
        Assert.AreEqual("060070170000000F", Convert.ToHexString(message.Version));
    }

    [TestMethod]
    public void Decode_MsNlmpNtlmV2Challenge_ReadsTheTargetInformation()
    {
        NtlmChallengeMessage message = Decoded(NtlmV2Challenge);

        Assert.AreEqual(0xE28A8233u, (uint)message.Flags);
        Assert.AreEqual("0123456789ABCDEF", Convert.ToHexString(message.ServerChallenge));
        Assert.AreEqual("Server", Encoding.Unicode.GetString(message.TargetName));
        Assert.AreEqual(NtlmV2TargetInformation, Convert.ToHexString(message.TargetInformation));
        Assert.AreEqual("060070170000000F", Convert.ToHexString(message.Version));
    }

    [TestMethod]
    public void Decode_ShorterThanThirtyTwoBytes_FailsTooShort()
    {
        byte[] message = Convert.FromHexString(NtlmV1Challenge)[..31];

        AssertFails(NtlmMessageFailure.TooShort, message);
    }

    [TestMethod]
    public void Decode_WrongSignature_FailsWrongSignatureOrType()
    {
        byte[] message = Convert.FromHexString(NtlmV1Challenge);
        message[0] = (byte)'X';

        AssertFails(NtlmMessageFailure.WrongSignatureOrType, message);
    }

    [TestMethod]
    public void Decode_NegotiateMessage_FailsWrongSignatureOrType()
    {
        AssertFails(NtlmMessageFailure.WrongSignatureOrType, NtlmNegotiateMessage.Encode());
    }

    [TestMethod]
    public void Decode_TargetInformationRunningPastTheEnd_FailsTargetInfoOutOfRange()
    {
        byte[] message = Convert.FromHexString(NtlmV2Challenge);
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(40), 0x25);

        AssertFails(NtlmMessageFailure.TargetInfoOutOfRange, message);
    }

    [TestMethod]
    public void Decode_TargetInformationStartingInsideTheHeader_FailsTargetInfoOutOfRange()
    {
        byte[] message = Convert.FromHexString(NtlmV2Challenge);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(44), 47);

        AssertFails(NtlmMessageFailure.TargetInfoOutOfRange, message);
    }

    [TestMethod]
    public void Decode_TargetInformationOffsetThatWouldOverflow_FailsTargetInfoOutOfRange()
    {
        byte[] message = Convert.FromHexString(NtlmV2Challenge);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(44), uint.MaxValue);

        AssertFails(NtlmMessageFailure.TargetInfoOutOfRange, message);
    }

    [TestMethod]
    public void Decode_TargetInformationFlagClear_IgnoresTheTargetInformationBuffer()
    {
        byte[] message = Convert.FromHexString(NtlmV2Challenge);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(20), 0xE20A8233);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(44), uint.MaxValue);

        Assert.IsEmpty(NtlmChallengeMessage.Decode(message).Message!.TargetInformation);
    }

    [TestMethod]
    public void Decode_TargetInformationOfZeroLength_IgnoresItsOffset()
    {
        byte[] message = Convert.FromHexString(NtlmV2Challenge);
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(40), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(44), uint.MaxValue);

        Assert.IsEmpty(NtlmChallengeMessage.Decode(message).Message!.TargetInformation);
    }

    [TestMethod]
    public void Decode_ThirtyTwoBytesWithTheTargetInformationFlag_HasNoTargetInformationOrVersion()
    {
        byte[] message = Convert.FromHexString(NtlmV2Challenge)[..32];
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(12), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(16), 0);

        NtlmChallengeMessage decoded = NtlmChallengeMessage.Decode(message).Message!;

        Assert.IsEmpty(decoded.TargetInformation);
        Assert.IsEmpty(decoded.TargetName);
        Assert.IsEmpty(decoded.Version);
    }

    [TestMethod]
    public void Decode_TargetNameOutsideTheMessage_SucceedsWithAnEmptyTargetName()
    {
        byte[] message = Convert.FromHexString(NtlmV1Challenge);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(16), uint.MaxValue);

        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(message);

        Assert.AreEqual(NtlmMessageFailure.None, decoding.Failure);
        Assert.IsEmpty(decoding.Message!.TargetName);
    }

    [TestMethod]
    public void Decode_VersionFlagClear_LeavesTheVersionEmpty()
    {
        byte[] message = Convert.FromHexString(NtlmV1Challenge);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(20), 0xE0028233);

        Assert.IsEmpty(NtlmChallengeMessage.Decode(message).Message!.Version);
    }

    private static NtlmChallengeMessage Decoded(string hex)
    {
        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(Convert.FromHexString(hex));
        Assert.AreEqual(NtlmMessageFailure.None, decoding.Failure);
        return decoding.Message!;
    }

    private static void AssertFails(NtlmMessageFailure expected, byte[] message)
    {
        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(message);

        Assert.AreEqual(expected, decoding.Failure);
        Assert.IsNull(decoding.Message);
    }
}
