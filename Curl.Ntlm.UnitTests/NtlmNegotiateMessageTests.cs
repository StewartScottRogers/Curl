using System.Buffers.Binary;

namespace Curl.Ntlm;

/// <summary>
/// Pins <see cref="NtlmNegotiateMessage" /> to the NEGOTIATE message curl 8.21.0's own NTLM
/// sends: measured with <c>Record-CurlExchange.ps1</c> (<c>--ntlm -u u:p</c> against a
/// <c>401 NTLM</c>, the OpenSSL build on Ubuntu, ADR-0142's table) and written by
/// <c>Curl_auth_create_ntlm_type1_message</c> in <c>lib/vauth/ntlm.c</c> at
/// <c>curl-8_21_0</c>.
/// </summary>
[TestClass]
public sealed class NtlmNegotiateMessageTests
{
    // ADR-0142, "Measured on 2026-09-28": Authorization: NTLM <this>, flags 0x00088206.
    private const string MeasuredCurlNegotiate = "TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=";

    [TestMethod]
    public void Encode_CurlDefaults_WritesTheBytesCurlSends()
    {
        Assert.AreEqual(MeasuredCurlNegotiate, Convert.ToBase64String(NtlmNegotiateMessage.Encode()));
    }

    [TestMethod]
    public void Encode_CurlDefaults_CarriesCurlFlagsAtOffsetTwelve()
    {
        byte[] message = NtlmNegotiateMessage.Encode();

        Assert.AreEqual((uint)NtlmNegotiateMessage.CurlFlags, BinaryPrimitives.ReadUInt32LittleEndian(message.AsSpan(12)));
    }

    [TestMethod]
    public void Encode_CurlDefaults_IsThirtyTwoBytesWithEmptyDomainAndWorkstationBuffers()
    {
        byte[] message = NtlmNegotiateMessage.Encode();

        Assert.AreEqual(NtlmNegotiateMessage.Length, message.Length);
        Assert.IsTrue(message.AsSpan(16).IndexOfAnyExcept((byte)0) < 0);
    }
}
