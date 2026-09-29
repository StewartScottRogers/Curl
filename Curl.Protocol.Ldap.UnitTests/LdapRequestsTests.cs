using System.Formats.Asn1;
using System.Numerics;
using Curl.Protocol.Ldap.Fakes;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Pins the BindRequest and UnbindRequest bytes curl sends, recorded with
/// <c>Record-CurlExchange.ps1 -Script</c> on 2026-09-28 (BL-586): curl 8.21.0 with WinLDAP on
/// Windows, curl 8.18.0 with OpenLDAP 2.6.10 on Linux.
/// </summary>
[TestClass]
public sealed class LdapRequestsTests
{
    [TestMethod]
    public void Bind_WinLdapWithUser_MatchesTheWindowsBuild()
    {
        byte[] request = LdapRequests.Bind(new LdapBerWriter(LdapDialect.WinLdap), 1, 3, "cn=u,dc=x", "secret");

        CollectionAssert.AreEqual(
            Hex.Bytes("30 84 00 00 00 1f 02 01 01 60 84 00 00 00 16 02 01 03 04 09 63 6e 3d 75 2c 64 63 3d 78 80 06 73 65 63 72 65 74"),
            request);
    }

    [TestMethod]
    public void Bind_WinLdapVersion2Retry_MatchesTheWindowsBuild()
    {
        byte[] request = LdapRequests.Bind(new LdapBerWriter(LdapDialect.WinLdap), 2, 2, "cn=u,dc=x", "secret");

        CollectionAssert.AreEqual(
            Hex.Bytes("30 84 00 00 00 1f 02 01 02 60 84 00 00 00 16 02 01 02 04 09 63 6e 3d 75 2c 64 63 3d 78 80 06 73 65 63 72 65 74"),
            request);
    }

    [TestMethod]
    public void Bind_OpenLdapWithUser_MatchesTheLinuxBuild()
    {
        byte[] request = LdapRequests.Bind(new LdapBerWriter(LdapDialect.OpenLdap), 1, 3, "cn=u,dc=x", "secret");

        CollectionAssert.AreEqual(
            Hex.Bytes("30 1b 02 01 01 60 16 02 01 03 04 09 63 6e 3d 75 2c 64 63 3d 78 80 06 73 65 63 72 65 74"),
            request);
    }

    [TestMethod]
    public void Bind_OpenLdapAnonymous_MatchesTheLinuxBuild()
    {
        byte[] request = LdapRequests.Bind(new LdapBerWriter(LdapDialect.OpenLdap), 1, 3, string.Empty, string.Empty);

        CollectionAssert.AreEqual(Hex.Bytes("30 0c 02 01 01 60 07 02 01 03 04 00 80 00"), request);
    }

    [TestMethod]
    public void Bind_NonAsciiName_IsSentAsUtf8()
    {
        byte[] request = LdapRequests.Bind(new LdapBerWriter(LdapDialect.OpenLdap), 1, 3, "cn=é", string.Empty);

        CollectionAssert.AreEqual(Hex.Bytes("30 11 02 01 01 60 0c 02 01 03 04 05 63 6e 3d c3 a9 80 00"), request);
    }

    [TestMethod]
    [DataRow(LdapDialect.WinLdap)]
    [DataRow(LdapDialect.OpenLdap)]
    public void Bind_RoundTripsThroughAsnReader(LdapDialect dialect)
    {
        byte[] request = LdapRequests.Bind(new LdapBerWriter(dialect), 5, 3, "cn=u", "p");

        AsnReader message = new AsnReader(request, AsnEncodingRules.BER).ReadSequence();
        Assert.AreEqual(new BigInteger(5), message.ReadInteger());
        AsnReader bind = message.ReadSequence(new Asn1Tag(TagClass.Application, 0));
        Assert.AreEqual(new BigInteger(3), bind.ReadInteger());
        CollectionAssert.AreEqual("cn=u"u8.ToArray(), bind.ReadOctetString());
        CollectionAssert.AreEqual("p"u8.ToArray(), bind.ReadOctetString(new Asn1Tag(TagClass.ContextSpecific, 0)));
        Assert.IsFalse(bind.HasData);
        Assert.IsFalse(message.HasData);
    }

    [TestMethod]
    public void Unbind_WinLdap_MatchesTheWindowsBuild()
    {
        CollectionAssert.AreEqual(Hex.Bytes("30 84 00 00 00 05 02 01 03 42 00"), LdapRequests.Unbind(new LdapBerWriter(LdapDialect.WinLdap), 3));
    }

    [TestMethod]
    public void Unbind_OpenLdap_MatchesTheLinuxBuild()
    {
        CollectionAssert.AreEqual(Hex.Bytes("30 05 02 01 02 42 00"), LdapRequests.Unbind(new LdapBerWriter(LdapDialect.OpenLdap), 2));
    }

    [TestMethod]
    [DataRow(LdapDialect.WinLdap)]
    [DataRow(LdapDialect.OpenLdap)]
    public void Unbind_RoundTripsThroughAsnReader(LdapDialect dialect)
    {
        AsnReader message = new AsnReader(LdapRequests.Unbind(new LdapBerWriter(dialect), 9), AsnEncodingRules.BER).ReadSequence();

        Assert.AreEqual(new BigInteger(9), message.ReadInteger());
        message.ReadNull(new Asn1Tag(TagClass.Application, 2));
        Assert.IsFalse(message.HasData);
    }

    [TestMethod]
    public void Search_OpenLdap_MatchesTheLinuxBuild()
    {
        byte[] request = LdapRequests.Search(
            new LdapBerWriter(LdapDialect.OpenLdap),
            2,
            "dc=example"u8.ToArray(),
            2,
            Hex.Bytes("a3 08 04 03 75 69 64 04 01 61"),
            ["cn"u8.ToArray(), "mail"u8.ToArray()]);

        CollectionAssert.AreEqual(
            Hex.Bytes("30 36 02 01 02 63 31 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 02 0a 01 00 02 01 00 02 01 00 01 01 00 a3 08 04 03 75 69 64 04 01 61 30 0a 04 02 63 6e 04 04 6d 61 69 6c"),
            request);
    }

    [TestMethod]
    public void Search_WinLdapNoAttributes_MatchesTheWindowsBuild()
    {
        byte[] request = LdapRequests.Search(
            new LdapBerWriter(LdapDialect.WinLdap),
            2,
            "dc=example"u8.ToArray(),
            0,
            Hex.Bytes("87 0b 4f 62 6a 65 63 74 43 6c 61 73 73"),
            []);

        CollectionAssert.AreEqual(
            Hex.Bytes("30 84 00 00 00 37 02 01 02 63 84 00 00 00 2e 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 4f 62 6a 65 63 74 43 6c 61 73 73 30 84 00 00 00 00"),
            request);
    }

    [TestMethod]
    public void Abandon_OpenLdap_MatchesTheLinuxBuild()
    {
        CollectionAssert.AreEqual(Hex.Bytes("30 06 02 01 03 50 01 02"), LdapRequests.Abandon(new LdapBerWriter(LdapDialect.OpenLdap), 3, 2));
    }
}
