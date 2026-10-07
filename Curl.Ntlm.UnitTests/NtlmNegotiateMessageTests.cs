using System.Buffers.Binary;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Encode_CurlDefaults_WritesTheBytesCurlSends()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("expected Authorization value", $"NTLM {MeasuredCurlNegotiate}");

        byte[] message = NtlmNegotiateMessage.Encode();
        ActMessage(diagnostics, message);

        diagnostics.Diff("type 1 message", Convert.FromBase64String(MeasuredCurlNegotiate), message);
        Assert.AreEqual(MeasuredCurlNegotiate, Convert.ToBase64String(message));
    }

    [TestMethod]
    public void Encode_CurlDefaults_CarriesCurlFlagsAtOffsetTwelve()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("curl's flags", $"0x{(uint)NtlmNegotiateMessage.CurlFlags:X8} ({NtlmNegotiateMessage.CurlFlags})");

        byte[] message = NtlmNegotiateMessage.Encode();
        ActMessage(diagnostics, message);

        uint flagsAtTwelve = BinaryPrimitives.ReadUInt32LittleEndian(message.AsSpan(12));
        diagnostics.Assert("flags at offset 12", $"0x{(uint)NtlmNegotiateMessage.CurlFlags:X8}", $"0x{flagsAtTwelve:X8}");
        Assert.AreEqual((uint)NtlmNegotiateMessage.CurlFlags, BinaryPrimitives.ReadUInt32LittleEndian(message.AsSpan(12)));
    }

    [TestMethod]
    public void Encode_CurlDefaults_IsThirtyTwoBytesWithEmptyDomainAndWorkstationBuffers()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("expected length", NtlmNegotiateMessage.Length);

        byte[] message = NtlmNegotiateMessage.Encode();
        ActMessage(diagnostics, message);

        diagnostics.Assert("length", NtlmNegotiateMessage.Length, message.Length);
        diagnostics.Assert("first non-zero byte from offset 16", -1, message.AsSpan(16).IndexOfAnyExcept((byte)0));
        Assert.AreEqual(NtlmNegotiateMessage.Length, message.Length);
        Assert.IsTrue(message.AsSpan(16).IndexOfAnyExcept((byte)0) < 0);
    }

    private static void ActMessage(TestDiagnostics diagnostics, byte[] message)
    {
        diagnostics.Bytes("type 1 message", message);
        diagnostics.Act("base64", Convert.ToBase64String(message));
    }
}
