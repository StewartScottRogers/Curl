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

    [TestMethod]
    public void TryEncode_UnicodeFixedInputs_WritesCurlsLayout()
    {
        NtlmAuthenticateMessage message = new(
            NtlmNegotiateFlags.NegotiateUnicode | NtlmNegotiateFlags.NegotiateNtlm,
            LmResponse,
            NtResponse,
            "DOM",
            "user",
            NtlmAuthenticateMessage.CurlWorkstation);

        Assert.IsTrue(message.TryEncode(out byte[]? encoded));

        Assert.AreEqual(
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
            "57004F0052004B00530054004100540049004F004E00",
            Convert.ToHexString(encoded));
    }

    [TestMethod]
    public void TryEncode_OemFlags_WritesStringsUnwidened()
    {
        NtlmAuthenticateMessage message = new(NtlmNegotiateFlags.NegotiateOem, [0xAA], [0xBB], "D", "u", "W");

        Assert.IsTrue(message.TryEncode(out byte[]? encoded));

        Assert.AreEqual(
            "4E544C4D53535000" + "03000000" +
            "0100010040000000" +
            "0100010041000000" +
            "0100010042000000" +
            "0100010043000000" +
            "0100010044000000" +
            "0000000000000000" +
            "02000000" +
            "AABB" + "44" + "75" + "57",
            Convert.ToHexString(encoded));
    }

    [TestMethod]
    public void TryEncode_UnicodeNonAscii_WidensEachUtf8ByteAsCurlDoes()
    {
        NtlmAuthenticateMessage message = new(NtlmNegotiateFlags.NegotiateUnicode, [], [], string.Empty, "é", string.Empty);

        Assert.IsTrue(message.TryEncode(out byte[]? encoded));

        Assert.AreEqual("C300A900", Convert.ToHexString(encoded.AsSpan(NtlmAuthenticateMessage.HeaderLength)));
    }

    [TestMethod]
    public void TryEncode_ResponsesPastTheBuffer_ReturnsFalse()
    {
        byte[] ntResponse = new byte[NtlmAuthenticateMessage.CurlBufferSize - NtlmAuthenticateMessage.HeaderLength - 24 + 1];
        NtlmAuthenticateMessage message = new(NtlmNegotiateFlags.None, LmResponse, ntResponse, string.Empty, string.Empty, string.Empty);

        Assert.IsFalse(message.TryEncode(out byte[]? encoded));
        Assert.IsNull(encoded);
    }

    [TestMethod]
    public void TryEncode_MessageFillingTheBuffer_ReturnsFalse()
    {
        byte[] ntResponse = new byte[NtlmAuthenticateMessage.CurlBufferSize - NtlmAuthenticateMessage.HeaderLength - 24 - 1];
        NtlmAuthenticateMessage message = new(NtlmNegotiateFlags.None, LmResponse, ntResponse, string.Empty, "u", string.Empty);

        Assert.IsFalse(message.TryEncode(out _));
    }

    [TestMethod]
    public void TryEncode_MessageOneByteShortOfTheBuffer_Succeeds()
    {
        byte[] ntResponse = new byte[NtlmAuthenticateMessage.CurlBufferSize - NtlmAuthenticateMessage.HeaderLength - 24 - 2];
        NtlmAuthenticateMessage message = new(NtlmNegotiateFlags.None, LmResponse, ntResponse, string.Empty, "u", string.Empty);

        Assert.IsTrue(message.TryEncode(out byte[]? encoded));
        Assert.HasCount(NtlmAuthenticateMessage.CurlBufferSize - 1, encoded);
    }
}
