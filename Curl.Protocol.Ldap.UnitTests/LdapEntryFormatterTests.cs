using System.Text;
using Curl.Protocol.Ldap.Fakes;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Pins the printable test both builds apply to a value between the bytes BL-588 recorded
/// (<see cref="LdapProtocolHandlerTests" /> holds the recordings): control bytes other than
/// tab, line feed, vertical tab, form feed and carriage return send a value to base64.
/// </summary>
[TestClass]
public sealed class LdapEntryFormatterTests
{
    [TestMethod]
    [DataRow(LdapDialect.WinLdap, "08", "\tv:: YQhi\n\n")]
    [DataRow(LdapDialect.WinLdap, "0e", "\tv:: YQ5i\n\n")]
    [DataRow(LdapDialect.WinLdap, "1f", "\tv:: YR9i\n\n")]
    [DataRow(LdapDialect.OpenLdap, "0e", "\tv:: YQ5i\n\n\n")]
    [DataRow(LdapDialect.OpenLdap, "1f", "\tv:: YR9i\n\n\n")]
    public void Format_ValueWithAControlByte_IsBase64(LdapDialect dialect, string control, string expectedAfterDn)
    {
        var entry = new LdapSearchEntry(Hex.Bytes("78"), [new LdapEntryAttribute(Hex.Bytes("76"), [Hex.Bytes("61 " + control + " 62")])]);

        byte[] text = [.. LdapEntryFormatter.FormatPieces(dialect, entry).SelectMany(piece => piece)];

        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes("DN: x\n" + expectedAfterDn), text);
    }
}
