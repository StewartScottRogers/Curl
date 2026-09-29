namespace Curl.Kerberos;

/// <summary>
/// Checks <see cref="KerberosKdcProxyMessage" /> against MS-KKDCP section 2.2.2 and MIT's
/// encoding of it (<c>src/lib/krb5/asn.1/asn1_k5.c</c>, <c>kkdcp_message</c>, as
/// <c>src/lib/krb5/os/sendto_kdc.c</c> fills it): explicit context tags, the Kerberos message
/// framed with its four-byte big-endian length inside <c>kerb-message</c>, the realm as a
/// GeneralString <c>target-domain</c>, and no <c>dclocator-hint</c>.
/// </summary>
[TestClass]
public sealed class KerberosKdcProxyMessageTests
{
    private static readonly byte[] Message = [0x6A, 0x03, 0x02, 0x01, 0x05];

    // SEQUENCE { [0] OCTET STRING 00000005 6A03020105, [1] GeneralString "EXAMPLE.TEST" }.
    private static readonly byte[] MitEncoding = Hex.Bytes(
        "30 1D A0 0B 04 09 00 00 00 05 6A 03 02 01 05 A1 0E 1B 0C 45 58 41 4D 50 4C 45 2E 54 45 53 54");

    [TestMethod]
    public void Encode_MessageAndRealm_MatchesMitsEncoding()
    {
        byte[] encoded = new KerberosKdcProxyMessage(Message, "EXAMPLE.TEST").Encode();

        CollectionAssert.AreEqual(MitEncoding, encoded);
    }

    [TestMethod]
    public void Encode_NoTargetDomain_LeavesTheFieldOut()
    {
        byte[] encoded = new KerberosKdcProxyMessage(Message, null).Encode();

        CollectionAssert.AreEqual(Hex.Bytes("30 0D A0 0B 04 09 00 00 00 05 6A 03 02 01 05"), encoded);
    }

    [TestMethod]
    public void Decode_MitsEncoding_GivesTheMessageWithoutItsPrefixAndTheRealm()
    {
        KerberosKdcProxyMessage decoded = KerberosKdcProxyMessage.Decode(MitEncoding);

        CollectionAssert.AreEqual(Message, decoded.KerberosMessage);
        Assert.AreEqual("EXAMPLE.TEST", decoded.TargetDomain);
    }

    [TestMethod]
    public void Decode_OnlyAMessageAndADcLocatorHint_GivesNoTargetDomain()
    {
        byte[] encoded = Hex.Bytes("30 12 A0 0B 04 09 00 00 00 05 6A 03 02 01 05 A2 03 02 01 01");

        KerberosKdcProxyMessage decoded = KerberosKdcProxyMessage.Decode(encoded);

        CollectionAssert.AreEqual(Message, decoded.KerberosMessage);
        Assert.IsNull(decoded.TargetDomain);
    }

    [TestMethod]
    [DataRow("30 0D A0 0B 04 09 00 00 00 06 6A 03 02 01 05", DisplayName = "Prefix longer than the message")]
    [DataRow("30 07 A0 05 04 03 00 00 00", DisplayName = "Shorter than the prefix")]
    [DataRow("04 03 00 00 00", DisplayName = "Not a SEQUENCE")]
    public void Decode_NotAKdcProxyMessage_ThrowsMalformed(string hex)
    {
        KerberosMessageException failure = Assert.ThrowsExactly<KerberosMessageException>(() => KerberosKdcProxyMessage.Decode(Hex.Bytes(hex)));

        Assert.AreEqual(KerberosMessageError.Malformed, failure.Error);
    }
}
