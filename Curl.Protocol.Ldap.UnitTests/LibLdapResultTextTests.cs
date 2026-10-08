using Curl.Testing;

namespace Curl.Protocol.Ldap;

/// <summary>Pins <c>libldap</c>'s texts, recorded from curl 8.18.0 with OpenLDAP 2.6.10 on 2026-09-28 (BL-587).</summary>
[TestClass]
public sealed class LibLdapResultTextTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(32, "No such object")]
    [DataRow(47, "Proxy Authorization Failure (X)")]
    [DataRow(80, "Other (e.g., implementation specific) error")]
    [DataRow(4096, "Content Sync Refresh Required")]
    [DataRow(15, "Unknown error")]
    [DataRow(128, "Unknown error")]
    public void Of_ResultCode_IsLibLdapsText(int resultCode, string expected)
    {
        Diagnostics.Arrange("result code", resultCode);
        string text = LibLdapResultText.Of(resultCode);
        Diagnostics.Act("text", text);
        Diagnostics.Assert("text", expected, text);
        Assert.AreEqual(expected, LibLdapResultText.Of(resultCode));
    }
}
