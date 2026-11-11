using System.Text;
using Curl.Testing;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Pins curl 8.22.0's <c>ldap_value_needs_base64</c> rule as BL-4913 measured it with the
/// WinLDAP build (ADR-1238): a value starting with a blank, a colon or a less-than sign,
/// ending with a blank, or holding any byte outside printable ASCII - tab, line feed and
/// carriage return included - is written in base64 after <c>::</c>.
/// </summary>
[TestClass]
public sealed class LdapValueBase64RuleTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("a\nb", "\tv:: YQpi\n\n", DisplayName = "line feed")]
    [DataRow("a\tb", "\tv:: YQli\n\n", DisplayName = "tab")]
    [DataRow("a\rb", "\tv:: YQ1i\n\n", DisplayName = "carriage return")]
    [DataRow("a\vb", "\tv:: YQti\n\n", DisplayName = "vertical tab")]
    [DataRow(":colon", "\tv:: OmNvbG9u\n\n", DisplayName = "leading colon")]
    [DataRow("<lt", "\tv:: PGx0\n\n", DisplayName = "leading less-than")]
    [DataRow(" lead", "\tv:: IGxlYWQ=\n\n", DisplayName = "leading blank")]
    [DataRow("trail ", "\tv:: dHJhaWwg\n\n", DisplayName = "trailing blank")]
    [DataRow("plain", "\tv: plain\n\n", DisplayName = "plain")]
    [DataRow("a:b<", "\tv: a:b<\n\n", DisplayName = "colon and less-than inside")]
    [DataRow("", "\tv: \n\n", DisplayName = "empty")]
    public void Format_WinLdapValue_IsBase64WhenCurlsRuleSaysSo(string value, string expectedAfterDn)
    {
        AssertFormats(LdapDialect.WinLdap, value, "DN: x\n" + expectedAfterDn);
    }

    [TestMethod]
    [DataRow("a\nb", "\tv:: YQpi\n\n\n", DisplayName = "line feed")]
    [DataRow(":colon", "\tv:: OmNvbG9u\n\n\n", DisplayName = "leading colon")]
    [DataRow("<lt", "\tv:: PGx0\n\n\n", DisplayName = "leading less-than")]
    [DataRow("\tlead", "\tv:: CWxlYWQ=\n\n\n", DisplayName = "leading tab")]
    [DataRow("plain", "\tv: plain\n\n\n", DisplayName = "plain")]
    public void Format_OpenLdapValue_IsBase64WhenCurlsRuleSaysSo(string value, string expectedAfterDn)
    {
        AssertFormats(LdapDialect.OpenLdap, value, "DN: x\n" + expectedAfterDn);
    }

    [TestMethod]
    public void Format_MeasuredEntry_IsWhatCurl8220Wrote()
    {
        var entry = new LdapSearchEntry(
            Encoding.ASCII.GetBytes("cn=a"),
            [Attribute("d1", "a\nb"), Attribute("d2", ":colon"), Attribute("d3", "<lt"), Attribute("d4", "plain")]);

        byte[] text = [.. LdapEntryFormatter.FormatPieces(LdapDialect.WinLdap, entry).SelectMany(piece => piece)];

        Diagnostics.Bytes("formatted text", text);
        Assert.AreEqual("DN: cn=a\n\td1:: YQpi\n\n\td2:: OmNvbG9u\n\n\td3:: PGx0\n\n\td4: plain\n\n", Encoding.Latin1.GetString(text));
    }

    private static LdapEntryAttribute Attribute(string name, string value) =>
        new(Encoding.ASCII.GetBytes(name), [Encoding.Latin1.GetBytes(value)]);

    private void AssertFormats(LdapDialect dialect, string value, string expected)
    {
        Diagnostics.Arrange("dialect", dialect);
        Diagnostics.Arrange("value", value);
        var entry = new LdapSearchEntry(Encoding.ASCII.GetBytes("x"), [Attribute("v", value)]);

        byte[] text = [.. LdapEntryFormatter.FormatPieces(dialect, entry).SelectMany(piece => piece)];

        Diagnostics.Bytes("formatted text", text);
        Assert.AreEqual(expected, Encoding.Latin1.GetString(text));
    }
}
