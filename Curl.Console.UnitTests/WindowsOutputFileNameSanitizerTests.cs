namespace Curl.Console;

/// <summary>
/// Pins the <c>-o</c> names curl 8.21.0 rewrites on Windows, each case measured on
/// 2026-09-26 with the local curl 8.21.0 (x86_64-w64-mingw32).
/// </summary>
[TestClass]
public sealed class WindowsOutputFileNameSanitizerTests
{
    [TestMethod]
    public void Sanitize_QuestionMark_BecomesUnderscore() =>
        Assert.AreEqual("C:/x_y", WindowsOutputFileNameSanitizer.Sanitize("C:/x?y"));

    [TestMethod]
    public void Sanitize_Asterisk_BecomesUnderscore() =>
        Assert.AreEqual("a_b", WindowsOutputFileNameSanitizer.Sanitize("a*b"));

    [TestMethod]
    public void Sanitize_LessThan_BecomesUnderscore() =>
        Assert.AreEqual("Z:/a_b", WindowsOutputFileNameSanitizer.Sanitize("Z:/a<b"));

    [TestMethod]
    public void Sanitize_GreaterThan_BecomesUnderscore() =>
        Assert.AreEqual("a_b", WindowsOutputFileNameSanitizer.Sanitize("a>b"));

    [TestMethod]
    public void Sanitize_VerticalBar_BecomesUnderscore() =>
        Assert.AreEqual("Z:/a_b", WindowsOutputFileNameSanitizer.Sanitize("Z:/a|b"));

    [TestMethod]
    public void Sanitize_DoubleQuote_BecomesUnderscore() =>
        Assert.AreEqual("a_b", WindowsOutputFileNameSanitizer.Sanitize("a\"b"));

    [TestMethod]
    public void Sanitize_EveryControlCharacter_BecomesUnderscore()
    {
        for (char control = '\u0001'; control <= '\u001F'; control++)
        {
            Assert.AreEqual("a_b", WindowsOutputFileNameSanitizer.Sanitize($"a{control}b"), $"U+{(int)control:X4}");
        }
    }

    [TestMethod]
    public void Sanitize_Delete_IsKept() =>
        Assert.AreEqual("a\u007Fb", WindowsOutputFileNameSanitizer.Sanitize("a\u007Fb"));

    [TestMethod]
    public void Sanitize_ColonOutsideTheDriveLetter_IsKept() =>
        Assert.AreEqual("ab:c", WindowsOutputFileNameSanitizer.Sanitize("ab:c"));

    [TestMethod]
    public void Sanitize_DriveLetterAndSeparators_AreKept() =>
        Assert.AreEqual("C:/dir\\sub/x:y", WindowsOutputFileNameSanitizer.Sanitize("C:/dir\\sub/x:y"));

    [TestMethod]
    public void Sanitize_DirectoryPart_IsRewrittenToo() =>
        Assert.AreEqual("sub_/x", WindowsOutputFileNameSanitizer.Sanitize("sub?/x"));

    [TestMethod]
    [DataRow("con", DisplayName = "Sanitize_ReservedNameCon_IsKept")]
    [DataRow("nul", DisplayName = "Sanitize_ReservedNameNul_IsKept")]
    [DataRow("prn", DisplayName = "Sanitize_ReservedNamePrn_IsKept")]
    [DataRow("aux", DisplayName = "Sanitize_ReservedNameAux_IsKept")]
    [DataRow("com1", DisplayName = "Sanitize_ReservedNameCom1_IsKept")]
    [DataRow("lpt1", DisplayName = "Sanitize_ReservedNameLpt1_IsKept")]
    [DataRow("con.txt", DisplayName = "Sanitize_ReservedNameConWithExtension_IsKept")]
    [DataRow("nul.txt", DisplayName = "Sanitize_ReservedNameNulWithExtension_IsKept")]
    [DataRow("prn.txt", DisplayName = "Sanitize_ReservedNamePrnWithExtension_IsKept")]
    [DataRow("aux.txt", DisplayName = "Sanitize_ReservedNameAuxWithExtension_IsKept")]
    [DataRow("com1.txt", DisplayName = "Sanitize_ReservedNameCom1WithExtension_IsKept")]
    [DataRow("lpt1.txt", DisplayName = "Sanitize_ReservedNameLpt1WithExtension_IsKept")]
    public void Sanitize_ReservedDeviceName_IsKept(string fileName) =>
        Assert.AreEqual(fileName, WindowsOutputFileNameSanitizer.Sanitize(fileName));
}
