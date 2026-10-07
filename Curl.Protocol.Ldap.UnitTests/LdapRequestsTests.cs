using System.Formats.Asn1;
using System.Numerics;
using Curl.Protocol.Ldap.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Pins the BindRequest and UnbindRequest bytes curl sends, recorded with
/// <c>Record-CurlExchange.ps1 -Script</c> on 2026-09-28 (BL-586): curl 8.21.0 with WinLDAP on
/// Windows, curl 8.18.0 with OpenLDAP 2.6.10 on Linux.
/// </summary>
[TestClass]
public sealed class LdapRequestsTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Bind_WinLdapWithUser_MatchesTheWindowsBuild()
    {
        Diagnostics.Arrange("dialect", LdapDialect.WinLdap);
        Diagnostics.Arrange("name and password", "cn=u,dc=x / secret");

        byte[] request = LdapRequests.Bind(new LdapBerWriter(LdapDialect.WinLdap), 1, 3, "cn=u,dc=x", "secret");

        CollectionAssert.AreEqual(
            LoggedExpectedRequest(Hex.Bytes("30 84 00 00 00 1f 02 01 01 60 84 00 00 00 16 02 01 03 04 09 63 6e 3d 75 2c 64 63 3d 78 80 06 73 65 63 72 65 74"), request),
            request);
    }

    [TestMethod]
    public void Bind_WinLdapVersion2Retry_MatchesTheWindowsBuild()
    {
        Diagnostics.Arrange("dialect", LdapDialect.WinLdap);
        Diagnostics.Arrange("version", 2);

        byte[] request = LdapRequests.Bind(new LdapBerWriter(LdapDialect.WinLdap), 2, 2, "cn=u,dc=x", "secret");

        CollectionAssert.AreEqual(
            LoggedExpectedRequest(Hex.Bytes("30 84 00 00 00 1f 02 01 02 60 84 00 00 00 16 02 01 02 04 09 63 6e 3d 75 2c 64 63 3d 78 80 06 73 65 63 72 65 74"), request),
            request);
    }

    [TestMethod]
    public void Bind_OpenLdapWithUser_MatchesTheLinuxBuild()
    {
        Diagnostics.Arrange("dialect", LdapDialect.OpenLdap);
        Diagnostics.Arrange("name and password", "cn=u,dc=x / secret");

        byte[] request = LdapRequests.Bind(new LdapBerWriter(LdapDialect.OpenLdap), 1, 3, "cn=u,dc=x", "secret");

        CollectionAssert.AreEqual(
            LoggedExpectedRequest(Hex.Bytes("30 1b 02 01 01 60 16 02 01 03 04 09 63 6e 3d 75 2c 64 63 3d 78 80 06 73 65 63 72 65 74"), request),
            request);
    }

    [TestMethod]
    public void Bind_OpenLdapAnonymous_MatchesTheLinuxBuild()
    {
        Diagnostics.Arrange("dialect", LdapDialect.OpenLdap);
        Diagnostics.Arrange("name and password", "anonymous");

        byte[] request = LdapRequests.Bind(new LdapBerWriter(LdapDialect.OpenLdap), 1, 3, string.Empty, string.Empty);

        CollectionAssert.AreEqual(LoggedExpectedRequest(Hex.Bytes("30 0c 02 01 01 60 07 02 01 03 04 00 80 00"), request), request);
    }

    [TestMethod]
    public void Bind_NonAsciiName_IsSentAsUtf8()
    {
        Diagnostics.Arrange("dialect", LdapDialect.OpenLdap);
        Diagnostics.Arrange("name", "cn=é");

        byte[] request = LdapRequests.Bind(new LdapBerWriter(LdapDialect.OpenLdap), 1, 3, "cn=é", string.Empty);

        CollectionAssert.AreEqual(LoggedExpectedRequest(Hex.Bytes("30 11 02 01 01 60 0c 02 01 03 04 05 63 6e 3d c3 a9 80 00"), request), request);
    }

    [TestMethod]
    [DataRow(LdapDialect.WinLdap)]
    [DataRow(LdapDialect.OpenLdap)]
    public void Bind_RoundTripsThroughAsnReader(LdapDialect dialect)
    {
        Diagnostics.Arrange("dialect", dialect);
        byte[] request = LdapRequests.Bind(new LdapBerWriter(dialect), 5, 3, "cn=u", "p");
        Diagnostics.Bytes("request", request);

        AsnReader message = new AsnReader(request, AsnEncodingRules.BER).ReadSequence();
        BigInteger messageId = message.ReadInteger();
        AsnReader bind = message.ReadSequence(new Asn1Tag(TagClass.Application, 0));
        BigInteger version = bind.ReadInteger();
        byte[] name = bind.ReadOctetString();
        byte[] password = bind.ReadOctetString(new Asn1Tag(TagClass.ContextSpecific, 0));

        Diagnostics.Act("message id", messageId);
        Diagnostics.Act("version", version);
        Diagnostics.Assert("message id", new BigInteger(5), messageId);
        Assert.AreEqual(new BigInteger(5), messageId);
        Diagnostics.Assert("version", new BigInteger(3), version);
        Assert.AreEqual(new BigInteger(3), version);
        Diagnostics.Diff("name", "cn=u"u8, name);
        CollectionAssert.AreEqual("cn=u"u8.ToArray(), name);
        Diagnostics.Diff("password", "p"u8, password);
        CollectionAssert.AreEqual("p"u8.ToArray(), password);
        Assert.IsFalse(bind.HasData);
        Assert.IsFalse(message.HasData);
    }

    [TestMethod]
    public void Unbind_WinLdap_MatchesTheWindowsBuild()
    {
        Diagnostics.Arrange("dialect", LdapDialect.WinLdap);

        byte[] request = LdapRequests.Unbind(new LdapBerWriter(LdapDialect.WinLdap), 3);

        CollectionAssert.AreEqual(LoggedExpectedRequest(Hex.Bytes("30 84 00 00 00 05 02 01 03 42 00"), request), request);
    }

    [TestMethod]
    public void Unbind_OpenLdap_MatchesTheLinuxBuild()
    {
        Diagnostics.Arrange("dialect", LdapDialect.OpenLdap);

        byte[] request = LdapRequests.Unbind(new LdapBerWriter(LdapDialect.OpenLdap), 2);

        CollectionAssert.AreEqual(LoggedExpectedRequest(Hex.Bytes("30 05 02 01 02 42 00"), request), request);
    }

    [TestMethod]
    [DataRow(LdapDialect.WinLdap)]
    [DataRow(LdapDialect.OpenLdap)]
    public void Unbind_RoundTripsThroughAsnReader(LdapDialect dialect)
    {
        Diagnostics.Arrange("dialect", dialect);
        byte[] request = LdapRequests.Unbind(new LdapBerWriter(dialect), 9);
        Diagnostics.Bytes("request", request);

        AsnReader message = new AsnReader(request, AsnEncodingRules.BER).ReadSequence();
        BigInteger messageId = message.ReadInteger();
        message.ReadNull(new Asn1Tag(TagClass.Application, 2));

        Diagnostics.Act("message id", messageId);
        Diagnostics.Assert("message id", new BigInteger(9), messageId);
        Assert.AreEqual(new BigInteger(9), messageId);
        Assert.IsFalse(message.HasData);
    }

    [TestMethod]
    public void Search_OpenLdap_MatchesTheLinuxBuild()
    {
        Diagnostics.Arrange("dialect", LdapDialect.OpenLdap);
        Diagnostics.Arrange("base DN and attributes", "dc=example / cn, mail");

        byte[] request = LdapRequests.Search(
            new LdapBerWriter(LdapDialect.OpenLdap),
            2,
            "dc=example"u8.ToArray(),
            2,
            Hex.Bytes("a3 08 04 03 75 69 64 04 01 61"),
            ["cn"u8.ToArray(), "mail"u8.ToArray()]);

        CollectionAssert.AreEqual(
            LoggedExpectedRequest(Hex.Bytes("30 36 02 01 02 63 31 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 02 0a 01 00 02 01 00 02 01 00 01 01 00 a3 08 04 03 75 69 64 04 01 61 30 0a 04 02 63 6e 04 04 6d 61 69 6c"), request),
            request);
    }

    [TestMethod]
    public void Search_WinLdapNoAttributes_MatchesTheWindowsBuild()
    {
        Diagnostics.Arrange("dialect", LdapDialect.WinLdap);
        Diagnostics.Arrange("base DN and attributes", "dc=example / none");

        byte[] request = LdapRequests.Search(
            new LdapBerWriter(LdapDialect.WinLdap),
            2,
            "dc=example"u8.ToArray(),
            0,
            Hex.Bytes("87 0b 4f 62 6a 65 63 74 43 6c 61 73 73"),
            []);

        CollectionAssert.AreEqual(
            LoggedExpectedRequest(Hex.Bytes("30 84 00 00 00 37 02 01 02 63 84 00 00 00 2e 04 0a 64 63 3d 65 78 61 6d 70 6c 65 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b 4f 62 6a 65 63 74 43 6c 61 73 73 30 84 00 00 00 00"), request),
            request);
    }

    [TestMethod]
    public void Abandon_OpenLdap_MatchesTheLinuxBuild()
    {
        Diagnostics.Arrange("dialect", LdapDialect.OpenLdap);

        byte[] request = LdapRequests.Abandon(new LdapBerWriter(LdapDialect.OpenLdap), 3, 2);

        CollectionAssert.AreEqual(LoggedExpectedRequest(Hex.Bytes("30 06 02 01 03 50 01 02"), request), request);
    }

    private byte[] LoggedExpectedRequest(byte[] expected, byte[] actual)
    {
        Diagnostics.Act("request length", actual.Length);
        Diagnostics.Bytes("request", actual);
        Diagnostics.Diff("request", expected, actual);
        return expected;
    }
}
