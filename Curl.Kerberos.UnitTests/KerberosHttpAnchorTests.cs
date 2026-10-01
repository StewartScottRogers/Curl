namespace Curl.Kerberos;

/// <summary>
/// Pins <see cref="KerberosHttpAnchor.Parse" /> and <see cref="KerberosConfiguration.HttpAnchors" />:
/// MIT's three case-sensitive <c>http_anchors</c> prefixes, a value with none of them, the
/// realm's values in file order, and none when the realm sets no <c>http_anchors</c> (ADR-0300).
/// </summary>
[TestClass]
public sealed class KerberosHttpAnchorTests
{
    [TestMethod]
    [DataRow("FILE:/etc/kdcproxy-ca.pem", KerberosHttpAnchorKind.File, "/etc/kdcproxy-ca.pem", DisplayName = "FILE:")]
    [DataRow("DIR:/etc/kdcproxy-cas", KerberosHttpAnchorKind.Directory, "/etc/kdcproxy-cas", DisplayName = "DIR:")]
    [DataRow("ENV:KDCPROXY_CA", KerberosHttpAnchorKind.EnvironmentVariable, "KDCPROXY_CA", DisplayName = "ENV:")]
    [DataRow("FILE:", KerberosHttpAnchorKind.File, "", DisplayName = "FILE: with no path")]
    public void Parse_KnownPrefix_ReturnsItsKindAndTheRest(string written, KerberosHttpAnchorKind kind, string location)
    {
        Assert.AreEqual(new KerberosHttpAnchor(kind, location), KerberosHttpAnchor.Parse(written));
    }

    [TestMethod]
    [DataRow("/etc/kdcproxy-ca.pem", DisplayName = "no prefix")]
    [DataRow("file:/etc/kdcproxy-ca.pem", DisplayName = "lower-case prefix")]
    [DataRow("PKCS11:module.so", DisplayName = "another prefix")]
    public void Parse_NoKnownPrefix_ReturnsNull(string written)
    {
        Assert.IsNull(KerberosHttpAnchor.Parse(written));
    }

    [TestMethod]
    public void HttpAnchors_RealmSetsSeveral_ReturnsThemInFileOrder()
    {
        KerberosConfiguration configuration = Parse(
            "[libdefaults]\n http_anchors = FILE:/ignored.pem\n" +
            "[realms]\n EXAMPLE.TEST = {\n http_anchors = FILE:/a.pem\n http_anchors = DIR:/cas\n http_anchors = ENV:CA\n }\n");

        CollectionAssert.AreEqual(new[] { "FILE:/a.pem", "DIR:/cas", "ENV:CA" }, configuration.HttpAnchors("EXAMPLE.TEST").ToArray());
    }

    [TestMethod]
    public void HttpAnchors_Absent_IsEmptyEvenWhenLibdefaultsSetsThem()
    {
        KerberosConfiguration configuration = Parse(
            "[libdefaults]\n http_anchors = FILE:/ignored.pem\n[realms]\n EXAMPLE.TEST = {\n kdc = https://proxy/\n }\n");

        Assert.IsEmpty(configuration.HttpAnchors("EXAMPLE.TEST"));
        Assert.IsEmpty(KerberosConfiguration.Empty.HttpAnchors("EXAMPLE.TEST"));
    }

    private static KerberosConfiguration Parse(string text)
    {
        KerberosConfigurationNode root = new(string.Empty, null);
        new KerberosConfigurationReader(new InMemoryKerberosFiles()).Parse(text, "test.conf", root);
        return new KerberosConfiguration(root);
    }
}
