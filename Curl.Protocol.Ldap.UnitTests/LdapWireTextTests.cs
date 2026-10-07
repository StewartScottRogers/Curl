using Curl.Protocol.Ldap.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ldap;

/// <summary>Pins the bytes each build sends for a URL's text, recorded on 2026-09-28 (BL-587).</summary>
[TestClass]
public sealed class LdapWireTextTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("Ã©", "c3 a9")]
    [DataRow("\u0080\u009f", "80 9f")]
    public void Encode_OpenLdap_SendsTheBytesAsTheyAre(string byteString, string expected)
    {
        Diagnostics.Arrange("dialect", LdapDialect.OpenLdap);
        Diagnostics.Arrange("byte string UTF-16 units", string.Join(' ', byteString.Select(c => ((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture))));
        byte[] actual = LdapWireText.Encode(LdapDialect.OpenLdap, byteString);
        Diagnostics.Act("encoded length", actual.Length);
        Diagnostics.Bytes("encoded", actual);
        Diagnostics.Diff("encoded", Hex.Bytes(expected), actual);
        Diagnostics.Assert("encoded length", Hex.Bytes(expected).Length, actual.Length);
        CollectionAssert.AreEqual(Hex.Bytes(expected), LdapWireText.Encode(LdapDialect.OpenLdap, byteString));
    }

    [TestMethod]
    [DataRow("dc=x", "64 63 3d 78")]
    [DataRow("Ã©", "c3 83 c2 a9")]
    [DataRow("\u0080\u009f", "e2 82 ac c5 b8")]
    [DataRow("\u0081ÿ", "c2 81 c3 bf")]
    public void Encode_WinLdap_ReadsWindows1252AndSendsUtf8(string byteString, string expected)
    {
        Diagnostics.Arrange("dialect", LdapDialect.WinLdap);
        Diagnostics.Arrange("byte string UTF-16 units", string.Join(' ', byteString.Select(c => ((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture))));
        byte[] actual = LdapWireText.Encode(LdapDialect.WinLdap, byteString);
        Diagnostics.Act("encoded length", actual.Length);
        Diagnostics.Bytes("encoded", actual);
        Diagnostics.Diff("encoded", Hex.Bytes(expected), actual);
        Diagnostics.Assert("encoded length", Hex.Bytes(expected).Length, actual.Length);
        CollectionAssert.AreEqual(Hex.Bytes(expected), LdapWireText.Encode(LdapDialect.WinLdap, byteString));
    }

    [TestMethod]
    [DataRow("63 6e 3d c3 a9", "63 6e 3d e9")]
    [DataRow("e2 82 ac c2 81", "80 81")]
    [DataRow("c4 80", "3f")]
    [DataRow("c2 80", "3f")]
    [DataRow("61 f0 9f 98 80 62", "61 3f 3f 62")]
    [DataRow("61 ff fe 62", "61 3f 3f 62")]
    [DataRow("61 c2 62 e0 a0 62", "61 3f 62 3f 62")]
    public void ToWindowsAnsi_GivesWindows1252OrAQuestionMarkPerUtf16Unit(string utf8, string expected)
    {
        Diagnostics.Arrange("utf8 input", utf8);
        Diagnostics.Bytes("utf8 input", Hex.Bytes(utf8));
        byte[] actual = LdapWireText.ToWindowsAnsi(Hex.Bytes(utf8));
        Diagnostics.Act("ansi length", actual.Length);
        Diagnostics.Bytes("ansi", actual);
        Diagnostics.Diff("ansi", Hex.Bytes(expected), actual);
        Diagnostics.Assert("ansi length", Hex.Bytes(expected).Length, actual.Length);
        CollectionAssert.AreEqual(Hex.Bytes(expected), LdapWireText.ToWindowsAnsi(Hex.Bytes(utf8)));
    }

    [TestMethod]
    [DataRow("63 6e", true)]
    [DataRow("3f", true)]
    [DataRow("c3 a9 e2 82 ac c2 81", true)]
    [DataRow("c2 80", false)]
    [DataRow("c4 80", false)]
    [DataRow("ff", false)]
    public void SurvivesWindowsAnsi_IsWhetherTheAnsiNameReadsBack(string utf8, bool expected)
    {
        Diagnostics.Arrange("utf8 input", utf8);
        Diagnostics.Bytes("utf8 input", Hex.Bytes(utf8));
        bool survives = LdapWireText.SurvivesWindowsAnsi(Hex.Bytes(utf8));
        Diagnostics.Act("survives", survives);
        Diagnostics.Assert("survives", expected, survives);
        Assert.AreEqual(expected, LdapWireText.SurvivesWindowsAnsi(Hex.Bytes(utf8)));
    }

    [TestMethod]
    public void ToByteString_HoldsOneCharacterPerUtf8Byte()
    {
        string byteString = LdapWireText.ToByteString("aé");
        Diagnostics.Arrange("text UTF-16 units", "0061 00e9");
        Diagnostics.Act("byte string UTF-16 units", string.Join(' ', byteString.Select(c => ((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture))));
        Diagnostics.Assert("byte string length", 3, byteString.Length);
        Assert.AreEqual("aÃ©", LdapWireText.ToByteString("aé"));
    }
}
