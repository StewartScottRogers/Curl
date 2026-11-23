using System.Text;
using Curl.Original.Protocol.Ldap.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Pins the printable test both builds apply to a value between the bytes BL-588 recorded
/// (<see cref="LdapProtocolHandlerTests" /> holds the recordings): a control byte sends a value
/// to base64 (<see cref="LdapValueBase64RuleTests" /> pins the rest of curl 8.22.0's rule).
/// </summary>
[TestClass]
public sealed class LdapEntryFormatterTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(LdapDialect.WinLdap, "08", "\tv:: YQhi\n\n")]
    [DataRow(LdapDialect.WinLdap, "0e", "\tv:: YQ5i\n\n")]
    [DataRow(LdapDialect.WinLdap, "1f", "\tv:: YR9i\n\n")]
    [DataRow(LdapDialect.OpenLdap, "0e", "\tv:: YQ5i\n\n\n")]
    [DataRow(LdapDialect.OpenLdap, "1f", "\tv:: YR9i\n\n\n")]
    public void Format_ValueWithAControlByte_IsBase64(LdapDialect dialect, string control, string expectedAfterDn)
    {
        Diagnostics.Arrange("dialect", dialect);
        Diagnostics.Arrange("control byte", control);
        var entry = new LdapSearchEntry(Hex.Bytes("78"), [new LdapEntryAttribute(Hex.Bytes("76"), [Hex.Bytes("61 " + control + " 62")])]);

        byte[] text = [.. LdapEntryFormatter.FormatPieces(dialect, entry).SelectMany(piece => piece)];

        Diagnostics.Bytes("formatted text", text);
        Diagnostics.Act("formatted length", text.Length);
        Diagnostics.Diff("formatted text", Encoding.Latin1.GetBytes("DN: x\n" + expectedAfterDn), text);
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes("DN: x\n" + expectedAfterDn), text);
    }

    [TestMethod]
    [DataRow("61 09 62", "\tv: a\tb\n\n\n")]
    [DataRow("61 0d 62", "\tv: a\rb\n\n\n")]
    [DataRow("6e 6c 0a", "\tv: nl\n\n\n\n")]
    [DataRow("09 61", "\tv:: CWE=\n\n\n")]
    [DataRow("61 20", "\tv:: YSA=\n\n\n")]
    [DataRow("61 7f", "\tv:: YX8=\n\n\n")]
    [DataRow("61 00 62", "\tv:: YQBi\n\n\n")]
    public void Format_OpenLdapValueWithInteriorTabCrLf_IsPrintedRaw(string valueHex, string expectedAfterDn)
    {
        Diagnostics.Arrange("value", valueHex);
        var entry = new LdapSearchEntry(Hex.Bytes("78"), [new LdapEntryAttribute(Hex.Bytes("76"), [Hex.Bytes(valueHex)])]);

        byte[] text = [.. LdapEntryFormatter.FormatPieces(LdapDialect.OpenLdap, entry).SelectMany(piece => piece)];

        Diagnostics.Bytes("formatted text", text);
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes("DN: x\n" + expectedAfterDn), text);
    }

    [TestMethod]
    public void Format_WinLdapValueWithInteriorTab_IsBase64()
    {
        var entry = new LdapSearchEntry(Hex.Bytes("78"), [new LdapEntryAttribute(Hex.Bytes("76"), [Hex.Bytes("61 09 62")])]);

        byte[] text = [.. LdapEntryFormatter.FormatPieces(LdapDialect.WinLdap, entry).SelectMany(piece => piece)];

        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes("DN: x\n\tv:: YQli\n\n"), text);
    }
}
