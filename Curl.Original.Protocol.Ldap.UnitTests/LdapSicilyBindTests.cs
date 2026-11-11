using System.Text;

namespace Curl.Original.Protocol.Ldap;

/// <summary>Pins the bytes of the Sicily NTLM negotiate BindRequest.</summary>
[TestClass]
public sealed class LdapSicilyBindTests
{
    private static readonly byte[] Negotiate = [.. "NTLMSSP\0"u8, 1, 0, 0, 0, 7, 0xb2, 8, 0xa2];

    [TestMethod]
    public void NegotiateRequest_NtlmssPayload_EncodesNameNtlmAndTag0x8a()
    {
        byte[] request = LdapSicilyBind.NegotiateRequest(new LdapBerWriter(LdapDialect.OpenLdap), 1, Negotiate);

        byte[] expected =
        [
            0x30, 0x20, 0x02, 0x01, 0x01,
            0x60, 0x1b, 0x02, 0x01, 0x03,
            0x04, 0x04, (byte)'N', (byte)'T', (byte)'L', (byte)'M',
            0x8a, 0x10, .. Negotiate,
        ];
        CollectionAssert.AreEqual(expected, request);
    }

    [TestMethod]
    public void NegotiateRequest_AnyPayload_NeverContainsAPassword()
    {
        byte[] request = LdapSicilyBind.NegotiateRequest(new LdapBerWriter(LdapDialect.WinLdap), 2, Negotiate);

        Assert.IsFalse(Encoding.Latin1.GetString(request).Contains("secret", StringComparison.Ordinal));
        Assert.IsTrue(request.AsSpan().IndexOf((byte)0x8a) > 0);
    }

    [TestMethod]
    public void NegotiateRequest_NotANegotiateMessage_Throws()
    {
        byte[] challenge = [.. "NTLMSSP\0"u8, 2, 0, 0, 0];
        Assert.ThrowsExactly<ArgumentException>(() => LdapSicilyBind.NegotiateRequest(new LdapBerWriter(LdapDialect.OpenLdap), 1, challenge));
        Assert.ThrowsExactly<ArgumentException>(() => LdapSicilyBind.NegotiateRequest(new LdapBerWriter(LdapDialect.OpenLdap), 1, []));
    }
}
