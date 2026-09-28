namespace Curl.Console;

/// <summary>
/// Pins the remote names curl 8.21.0 rewrites on Windows, each case measured on 2026-09-27
/// (BL-239 Notes). The <c>-o</c> cases are pinned against <c>Curl.Core</c>'s
/// <c>WindowsOutputFileNameSanitizer</c> in <c>Curl.Core.UnitTests</c>.
/// </summary>
[TestClass]
public sealed class WindowsOutputFileNameSanitizerTests
{
    [TestMethod]
    [DataRow("a:b.txt", "a_b.txt", DisplayName = "SanitizeRemoteName_Colon_BecomesUnderscore")]
    [DataRow("a*b.txt", "a_b.txt", DisplayName = "SanitizeRemoteName_Asterisk_BecomesUnderscore")]
    [DataRow("con", "_con", DisplayName = "SanitizeRemoteName_Con_GetsAnUnderscoreFirst")]
    [DataRow("COM1", "_COM1", DisplayName = "SanitizeRemoteName_Com1_GetsAnUnderscoreFirst")]
    [DataRow("clock$", "_clock$", DisplayName = "SanitizeRemoteName_Clock_GetsAnUnderscoreFirst")]
    [DataRow("con.txt", "con_txt", DisplayName = "SanitizeRemoteName_ConWithExtension_LosesTheDot")]
    [DataRow("lpt9.txt", "lpt9_txt", DisplayName = "SanitizeRemoteName_Lpt9WithExtension_LosesTheDot")]
    [DataRow("aux.x.y", "aux_x.y", DisplayName = "SanitizeRemoteName_AuxWithTwoExtensions_LosesTheFirstDot")]
    [DataRow("a.con", "a.con", DisplayName = "SanitizeRemoteName_ReservedExtension_IsKept")]
    [DataRow("nul%20", "nul%20", DisplayName = "SanitizeRemoteName_ReservedNameWithMore_IsKept")]
    [DataRow("CON%3Ax", "CON%3Ax", DisplayName = "SanitizeRemoteName_EncodedColon_IsKept")]
    public void SanitizeRemoteName_MeasuredName_IsRewrittenAsCurlDoes(string fileName, string expected) =>
        Assert.AreEqual(expected, WindowsOutputFileNameSanitizer.SanitizeRemoteName(fileName));
}
