using Curl.Testing;

namespace Curl.Ntlm;

/// <summary>
/// Pins <see cref="NtlmAuthenticateMessage.TryEncode" /> to the layout curl 8.21.0's
/// <c>Curl_auth_create_ntlm_type3_message</c> writes for fixed inputs, and to its
/// <c>NTLM_BUFSIZE</c> limits.
/// </summary>
[TestClass]
public sealed class NtlmAuthenticateMessageTests
{
    private static readonly byte[] LmResponse = Enumerable.Repeat((byte)0x11, 24).ToArray();

    private static readonly byte[] NtResponse = Enumerable.Repeat((byte)0x22, 24).ToArray();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TryEncode_UnicodeFixedInputs_WritesCurlsLayout()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        NtlmAuthenticateMessage message = new(
            NtlmNegotiateFlags.NegotiateUnicode | NtlmNegotiateFlags.NegotiateNtlm,
            LmResponse,
            NtResponse,
            "DOM",
            "user",
            NtlmAuthenticateMessage.CurlWorkstation);
        ArrangeMessage(diagnostics, message);

        bool encodedOk = message.TryEncode(out byte[]? encoded);
        ActEncoding(diagnostics, encodedOk, encoded);

        string expected =
            "4E544C4D53535000" + "03000000" +
            "1800180040000000" + // LM response: 24 bytes at 64
            "1800180058000000" + // NT response: 24 bytes at 88
            "0600060070000000" + // domain: 6 bytes at 112
            "0800080076000000" + // user: 8 bytes at 118
            "160016007E000000" + // workstation: 22 bytes at 126
            "0000000000000000" + // session key: always zero
            "01020000" + // flags
            new string('1', 48) +
            new string('2', 48) +
            "44004F004D00" +
            "7500730065007200" +
            "57004F0052004B00530054004100540049004F004E00";
        diagnostics.Diff("type 3 message", Convert.FromHexString(expected), encoded ?? []);
        Assert.IsTrue(encodedOk);

        Assert.AreEqual(expected, Convert.ToHexString(encoded!));
    }

    [TestMethod]
    public void TryEncode_OemFlags_WritesStringsUnwidened()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        NtlmAuthenticateMessage message = new(NtlmNegotiateFlags.NegotiateOem, [0xAA], [0xBB], "D", "u", "W");
        ArrangeMessage(diagnostics, message);

        bool encodedOk = message.TryEncode(out byte[]? encoded);
        ActEncoding(diagnostics, encodedOk, encoded);

        string expected =
            "4E544C4D53535000" + "03000000" +
            "0100010040000000" +
            "0100010041000000" +
            "0100010042000000" +
            "0100010043000000" +
            "0100010044000000" +
            "0000000000000000" +
            "02000000" +
            "AABB" + "44" + "75" + "57";
        diagnostics.Diff("type 3 message", Convert.FromHexString(expected), encoded ?? []);
        Assert.IsTrue(encodedOk);

        Assert.AreEqual(expected, Convert.ToHexString(encoded!));
    }

    [TestMethod]
    public void TryEncode_UnicodeNonAscii_WidensEachUtf8ByteAsCurlDoes()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        NtlmAuthenticateMessage message = new(NtlmNegotiateFlags.NegotiateUnicode, [], [], string.Empty, "é", string.Empty);
        ArrangeMessage(diagnostics, message);

        bool encodedOk = message.TryEncode(out byte[]? encoded);
        ActEncoding(diagnostics, encodedOk, encoded);

        diagnostics.Diff("payload after the header", Convert.FromHexString("C300A900"), encoded.AsSpan(Math.Min(encoded?.Length ?? 0, NtlmAuthenticateMessage.HeaderLength)));
        Assert.IsTrue(encodedOk);

        Assert.AreEqual("C300A900", Convert.ToHexString(encoded.AsSpan(NtlmAuthenticateMessage.HeaderLength)));
    }

    [TestMethod]
    public void TryEncode_ResponsesPastTheBuffer_ReturnsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] ntResponse = new byte[NtlmAuthenticateMessage.CurlBufferSize - NtlmAuthenticateMessage.HeaderLength - 24 + 1];
        NtlmAuthenticateMessage message = new(NtlmNegotiateFlags.None, LmResponse, ntResponse, string.Empty, string.Empty, string.Empty);
        ArrangeMessage(diagnostics, message);

        bool encodedOk = message.TryEncode(out byte[]? encoded);
        ActEncoding(diagnostics, encodedOk, encoded);

        diagnostics.Assert("encoded", false, encodedOk);
        Assert.IsFalse(encodedOk);
        Assert.IsNull(encoded);
    }

    [TestMethod]
    public void TryEncode_MessageFillingTheBuffer_ReturnsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] ntResponse = new byte[NtlmAuthenticateMessage.CurlBufferSize - NtlmAuthenticateMessage.HeaderLength - 24 - 1];
        NtlmAuthenticateMessage message = new(NtlmNegotiateFlags.None, LmResponse, ntResponse, string.Empty, "u", string.Empty);
        ArrangeMessage(diagnostics, message);

        bool encodedOk = message.TryEncode(out byte[]? encoded);
        ActEncoding(diagnostics, encodedOk, encoded);

        diagnostics.Assert("encoded", false, encodedOk);
        Assert.IsFalse(encodedOk);
    }

    [TestMethod]
    public void TryEncode_MessageOneByteShortOfTheBuffer_Succeeds()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] ntResponse = new byte[NtlmAuthenticateMessage.CurlBufferSize - NtlmAuthenticateMessage.HeaderLength - 24 - 2];
        NtlmAuthenticateMessage message = new(NtlmNegotiateFlags.None, LmResponse, ntResponse, string.Empty, "u", string.Empty);
        ArrangeMessage(diagnostics, message);

        bool encodedOk = message.TryEncode(out byte[]? encoded);
        ActEncoding(diagnostics, encodedOk, encoded);

        diagnostics.Assert("encoded length", NtlmAuthenticateMessage.CurlBufferSize - 1, encoded?.Length);
        Assert.IsTrue(encodedOk);
        Assert.HasCount(NtlmAuthenticateMessage.CurlBufferSize - 1, encoded!);
    }

    [TestMethod]
    public void TryEncodeWithFailure_ResponsesPastTheBuffer_ReportsResponsesTooLarge()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] ntResponse = new byte[NtlmAuthenticateMessage.CurlBufferSize - NtlmAuthenticateMessage.HeaderLength - 24 + 1];
        NtlmAuthenticateMessage message = new(NtlmNegotiateFlags.None, LmResponse, ntResponse, string.Empty, string.Empty, string.Empty);
        ArrangeMessage(diagnostics, message);

        bool encodedOk = message.TryEncode(out byte[]? encoded, out NtlmMessageFailure failure);
        ActEncoding(diagnostics, encodedOk, encoded);
        diagnostics.Act("failure", failure);

        diagnostics.Assert("failure", NtlmMessageFailure.ResponsesTooLarge, failure);
        Assert.IsFalse(encodedOk);
        Assert.IsNull(encoded);
        Assert.AreEqual(NtlmMessageFailure.ResponsesTooLarge, failure);
    }

    [TestMethod]
    public void TryEncodeWithFailure_ResponsesFitButNamesReachTheBuffer_ReportsNamesTooLarge()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        NtlmAuthenticateMessage message = new(NtlmNegotiateFlags.None, LmResponse, NtResponse, "DOM", new string('u', 1000), string.Empty);
        ArrangeMessage(diagnostics, message);

        bool encodedOk = message.TryEncode(out byte[]? encoded, out NtlmMessageFailure failure);
        ActEncoding(diagnostics, encodedOk, encoded);
        diagnostics.Act("failure", failure);

        diagnostics.Assert("failure", NtlmMessageFailure.NamesTooLarge, failure);
        Assert.IsFalse(encodedOk);
        Assert.IsNull(encoded);
        Assert.AreEqual(NtlmMessageFailure.NamesTooLarge, failure);
    }

    [TestMethod]
    public void TryEncodeWithFailure_MessageOf1023Bytes_EncodesWithNoFailure()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] ntResponse = new byte[NtlmAuthenticateMessage.CurlBufferSize - NtlmAuthenticateMessage.HeaderLength - 24 - 2];
        NtlmAuthenticateMessage message = new(NtlmNegotiateFlags.None, LmResponse, ntResponse, string.Empty, "u", string.Empty);
        ArrangeMessage(diagnostics, message);

        bool encodedOk = message.TryEncode(out byte[]? encoded, out NtlmMessageFailure failure);
        ActEncoding(diagnostics, encodedOk, encoded);
        diagnostics.Act("failure", failure);

        diagnostics.Assert("encoded length", 1023, encoded?.Length);
        diagnostics.Assert("failure", NtlmMessageFailure.None, failure);
        Assert.IsTrue(encodedOk);
        Assert.HasCount(1023, encoded!);
        Assert.AreEqual(NtlmMessageFailure.None, failure);
    }

    [TestMethod]
    public void TryEncodeWithFailure_MessageOf1024Bytes_ReportsNamesTooLarge()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] ntResponse = new byte[NtlmAuthenticateMessage.CurlBufferSize - NtlmAuthenticateMessage.HeaderLength - 24 - 1];
        NtlmAuthenticateMessage message = new(NtlmNegotiateFlags.None, LmResponse, ntResponse, string.Empty, "u", string.Empty);
        ArrangeMessage(diagnostics, message);

        bool encodedOk = message.TryEncode(out byte[]? encoded, out NtlmMessageFailure failure);
        ActEncoding(diagnostics, encodedOk, encoded);
        diagnostics.Act("failure", failure);

        diagnostics.Assert("failure", NtlmMessageFailure.NamesTooLarge, failure);
        Assert.IsFalse(encodedOk);
        Assert.AreEqual(NtlmMessageFailure.NamesTooLarge, failure);
    }

    [TestMethod]
    public void TryEncodeWithFailure_ResponsesEndingAtExactly1024Bytes_PassTheResponsesCheck()
    {
        // curl's first check is ntresplen + size > NTLM_BUFSIZE, so responses ending at 1024 pass
        // it; the empty names still make a 1024-byte message, which the second (>=) check refuses.
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] ntResponse = new byte[NtlmAuthenticateMessage.CurlBufferSize - NtlmAuthenticateMessage.HeaderLength - 24];
        NtlmAuthenticateMessage message = new(NtlmNegotiateFlags.None, LmResponse, ntResponse, string.Empty, string.Empty, string.Empty);
        ArrangeMessage(diagnostics, message);

        bool encodedOk = message.TryEncode(out byte[]? encoded, out NtlmMessageFailure failure);
        ActEncoding(diagnostics, encodedOk, encoded);
        diagnostics.Act("failure", failure);

        diagnostics.Assert("failure", NtlmMessageFailure.NamesTooLarge, failure);
        Assert.IsFalse(encodedOk);
        Assert.AreEqual(NtlmMessageFailure.NamesTooLarge, failure);
    }

    private static void ArrangeMessage(TestDiagnostics diagnostics, NtlmAuthenticateMessage message)
    {
        diagnostics.Arrange("flags", $"0x{(uint)message.Flags:X8} ({message.Flags})");
        diagnostics.Arrange("domain", message.Domain.Length > 64 ? $"{message.Domain.Length} characters" : message.Domain);
        diagnostics.Arrange("user", message.User.Length > 64 ? $"{message.User.Length} characters of '{message.User[0]}'" : message.User);
        diagnostics.Arrange("workstation", message.Workstation);
        diagnostics.Arrange("response lengths", $"LM {message.LmChallengeResponse.Length}, NT {message.NtChallengeResponse.Length} (buffer {NtlmAuthenticateMessage.CurlBufferSize}, header {NtlmAuthenticateMessage.HeaderLength})");
        diagnostics.Bytes("LM response", message.LmChallengeResponse);
        diagnostics.Bytes("NT response", message.NtChallengeResponse);
    }

    private static void ActEncoding(TestDiagnostics diagnostics, bool encodedOk, byte[]? encoded)
    {
        diagnostics.Act("encoded", encodedOk);
        diagnostics.Act("encoded length", encoded?.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null");
        if (encoded is not null)
        {
            diagnostics.Bytes("type 3 message", encoded);
        }
    }
}
