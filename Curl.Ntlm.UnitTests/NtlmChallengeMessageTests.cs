using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Decode_MsNlmpNtlmV1Challenge_ReadsEveryField()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        NtlmChallengeMessage message = Decoded(diagnostics, NtlmV1Challenge);

        diagnostics.Assert("flags", "0xE2028233", Flags(message.Flags));
        diagnostics.Diff("server challenge", "0123456789ABCDEF", Convert.ToHexString(message.ServerChallenge));
        diagnostics.Diff("target name", "Server", Encoding.Unicode.GetString(message.TargetName));
        diagnostics.Diff("version", "060070170000000F", Convert.ToHexString(message.Version));
        Assert.AreEqual(0xE2028233u, (uint)message.Flags);
        Assert.AreEqual("0123456789ABCDEF", Convert.ToHexString(message.ServerChallenge));
        Assert.AreEqual("Server", Encoding.Unicode.GetString(message.TargetName));
        Assert.IsEmpty(message.TargetInformation);
        Assert.AreEqual("060070170000000F", Convert.ToHexString(message.Version));
    }

    [TestMethod]
    public void Decode_MsNlmpNtlmV2Challenge_ReadsTheTargetInformation()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        NtlmChallengeMessage message = Decoded(diagnostics, NtlmV2Challenge);

        diagnostics.Assert("flags", "0xE28A8233", Flags(message.Flags));
        diagnostics.Diff("server challenge", "0123456789ABCDEF", Convert.ToHexString(message.ServerChallenge));
        diagnostics.Diff("target name", "Server", Encoding.Unicode.GetString(message.TargetName));
        diagnostics.Diff("target information", Convert.FromHexString(NtlmV2TargetInformation), message.TargetInformation);
        diagnostics.Diff("version", "060070170000000F", Convert.ToHexString(message.Version));
        Assert.AreEqual(0xE28A8233u, (uint)message.Flags);
        Assert.AreEqual("0123456789ABCDEF", Convert.ToHexString(message.ServerChallenge));
        Assert.AreEqual("Server", Encoding.Unicode.GetString(message.TargetName));
        Assert.AreEqual(NtlmV2TargetInformation, Convert.ToHexString(message.TargetInformation));
        Assert.AreEqual("060070170000000F", Convert.ToHexString(message.Version));
    }

    [TestMethod]
    public void Decode_ShorterThanThirtyTwoBytes_FailsTooShort()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] message = Convert.FromHexString(NtlmV1Challenge)[..31];
        diagnostics.Arrange("change", "the MS-NLMP 4.2.2.3 message cut to 31 bytes");

        AssertFails(diagnostics, NtlmMessageFailure.TooShort, message);
    }

    [TestMethod]
    public void Decode_WrongSignature_FailsWrongSignatureOrType()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] message = Convert.FromHexString(NtlmV1Challenge);
        message[0] = (byte)'X';
        diagnostics.Arrange("change", "the MS-NLMP 4.2.2.3 message with byte 0 set to 'X'");

        AssertFails(diagnostics, NtlmMessageFailure.WrongSignatureOrType, message);
    }

    [TestMethod]
    public void Decode_NegotiateMessage_FailsWrongSignatureOrType()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("change", "curl's type 1 (NEGOTIATE) message in place of a type 2");

        AssertFails(diagnostics, NtlmMessageFailure.WrongSignatureOrType, NtlmNegotiateMessage.Encode());
    }

    [TestMethod]
    public void Decode_TargetInformationRunningPastTheEnd_FailsTargetInfoOutOfRange()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] message = Convert.FromHexString(NtlmV2Challenge);
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(40), 0x25);
        diagnostics.Arrange("change", "the MS-NLMP 4.2.4.3 message with target information length 0x25 (one past the end)");

        AssertFails(diagnostics, NtlmMessageFailure.TargetInfoOutOfRange, message);
    }

    [TestMethod]
    public void Decode_TargetInformationStartingInsideTheHeader_FailsTargetInfoOutOfRange()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] message = Convert.FromHexString(NtlmV2Challenge);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(44), 47);
        diagnostics.Arrange("change", "the MS-NLMP 4.2.4.3 message with target information offset 47 (inside the header)");

        AssertFails(diagnostics, NtlmMessageFailure.TargetInfoOutOfRange, message);
    }

    [TestMethod]
    public void Decode_TargetInformationOffsetThatWouldOverflow_FailsTargetInfoOutOfRange()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] message = Convert.FromHexString(NtlmV2Challenge);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(44), uint.MaxValue);
        diagnostics.Arrange("change", "the MS-NLMP 4.2.4.3 message with target information offset 0xFFFFFFFF");

        AssertFails(diagnostics, NtlmMessageFailure.TargetInfoOutOfRange, message);
    }

    [TestMethod]
    public void Decode_TargetInformationFlagClear_IgnoresTheTargetInformationBuffer()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] message = Convert.FromHexString(NtlmV2Challenge);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(20), 0xE20A8233);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(44), uint.MaxValue);
        diagnostics.Arrange("change", "the MS-NLMP 4.2.4.3 message with flags 0xE20A8233 (no TARGET_INFO) and target information offset 0xFFFFFFFF");
        diagnostics.Bytes("type 2 message", message);

        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(message);
        ActDecoding(diagnostics, decoding);

        diagnostics.Assert("target information length", 0, decoding.Message?.TargetInformation.Length);
        Assert.IsEmpty(decoding.Message!.TargetInformation);
    }

    [TestMethod]
    public void Decode_TargetInformationOfZeroLength_IgnoresItsOffset()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] message = Convert.FromHexString(NtlmV2Challenge);
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(40), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(44), uint.MaxValue);
        diagnostics.Arrange("change", "the MS-NLMP 4.2.4.3 message with target information length 0 and offset 0xFFFFFFFF");
        diagnostics.Bytes("type 2 message", message);

        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(message);
        ActDecoding(diagnostics, decoding);

        diagnostics.Assert("target information length", 0, decoding.Message?.TargetInformation.Length);
        Assert.IsEmpty(decoding.Message!.TargetInformation);
    }

    [TestMethod]
    public void Decode_ThirtyTwoBytesWithTheTargetInformationFlag_HasNoTargetInformationOrVersion()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] message = Convert.FromHexString(NtlmV2Challenge)[..32];
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(12), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(16), 0);
        diagnostics.Arrange("change", "the MS-NLMP 4.2.4.3 message cut to 32 bytes, target name length and offset 0");
        diagnostics.Bytes("type 2 message", message);

        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(message);
        ActDecoding(diagnostics, decoding);
        NtlmChallengeMessage decoded = decoding.Message!;

        diagnostics.Assert(
            "target information, target name and version lengths",
            "0, 0, 0",
            string.Create(CultureInfo.InvariantCulture, $"{decoded.TargetInformation.Length}, {decoded.TargetName.Length}, {decoded.Version.Length}"));
        Assert.IsEmpty(decoded.TargetInformation);
        Assert.IsEmpty(decoded.TargetName);
        Assert.IsEmpty(decoded.Version);
    }

    [TestMethod]
    public void Decode_TargetNameOutsideTheMessage_SucceedsWithAnEmptyTargetName()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] message = Convert.FromHexString(NtlmV1Challenge);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(16), uint.MaxValue);
        diagnostics.Arrange("change", "the MS-NLMP 4.2.2.3 message with target name offset 0xFFFFFFFF");
        diagnostics.Bytes("type 2 message", message);

        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(message);
        ActDecoding(diagnostics, decoding);

        diagnostics.Assert("failure", NtlmMessageFailure.None, decoding.Failure);
        diagnostics.Assert("target name length", 0, decoding.Message?.TargetName.Length);
        Assert.AreEqual(NtlmMessageFailure.None, decoding.Failure);
        Assert.IsEmpty(decoding.Message!.TargetName);
    }

    [TestMethod]
    public void Decode_VersionFlagClear_LeavesTheVersionEmpty()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] message = Convert.FromHexString(NtlmV1Challenge);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(20), 0xE0028233);
        diagnostics.Arrange("change", "the MS-NLMP 4.2.2.3 message with flags 0xE0028233 (no VERSION)");
        diagnostics.Bytes("type 2 message", message);

        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(message);
        ActDecoding(diagnostics, decoding);

        diagnostics.Assert("version length", 0, decoding.Message?.Version.Length);
        Assert.IsEmpty(decoding.Message!.Version);
    }

    private static string Flags(NtlmNegotiateFlags flags) =>
        string.Create(CultureInfo.InvariantCulture, $"0x{(uint)flags:X8}");

    private static void ActDecoding(TestDiagnostics diagnostics, NtlmChallengeDecoding decoding)
    {
        diagnostics.Act("failure", decoding.Failure);
        if (decoding.Message is not { } message)
        {
            diagnostics.Act("message", "null");
            return;
        }

        diagnostics.Act("flags", $"{Flags(message.Flags)} ({message.Flags})");
        diagnostics.Bytes("server challenge", message.ServerChallenge);
        diagnostics.Bytes("target name", message.TargetName);
        diagnostics.Bytes("target information", message.TargetInformation);
        diagnostics.Bytes("version", message.Version);
    }

    private static NtlmChallengeMessage Decoded(TestDiagnostics diagnostics, string hex)
    {
        byte[] bytes = Convert.FromHexString(hex);
        diagnostics.Bytes("type 2 message", bytes);
        diagnostics.Arrange("type 2 message length", bytes.Length);

        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(bytes);
        ActDecoding(diagnostics, decoding);

        diagnostics.Assert("failure", NtlmMessageFailure.None, decoding.Failure);
        Assert.AreEqual(NtlmMessageFailure.None, decoding.Failure);
        return decoding.Message!;
    }

    private static void AssertFails(TestDiagnostics diagnostics, NtlmMessageFailure expected, byte[] message)
    {
        diagnostics.Bytes("type 2 message", message);
        diagnostics.Arrange("type 2 message length", message.Length);

        NtlmChallengeDecoding decoding = NtlmChallengeMessage.Decode(message);
        ActDecoding(diagnostics, decoding);

        diagnostics.Assert("failure", expected, decoding.Failure);
        diagnostics.Assert("message", "null", decoding.Message is null ? "null" : "decoded");
        Assert.AreEqual(expected, decoding.Failure);
        Assert.IsNull(decoding.Message);
    }
}
