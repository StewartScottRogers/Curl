using static Curl.Core.Globbing.WindowsOutputFileNameSanitizer;

namespace Curl.Core.Globbing;

/// <summary>
/// Pins <see cref="WindowsOutputFileNameSanitizer" /> against every row of curl 8.22.0's
/// <c>tests/tunit/tool1604.c</c> table that a Windows (not MSDOS) build runs, output and
/// result, plus the branches that table leaves out. Pure string work, so it runs on every OS.
/// </summary>
[TestClass]
public sealed class WindowsOutputFileNameSanitizerTests
{
    private const SanitizeFlags AllowPath = SanitizeFlags.AllowPath;

    private const SanitizeFlags AllowReserved = SanitizeFlags.AllowReserved;

    private const SanitizeFlags AllowBoth = SanitizeFlags.AllowPath | SanitizeFlags.AllowReserved;

    [TestMethod]
    [DataRow("", SanitizeFlags.None, "")]
    [DataRow("normal filename", SanitizeFlags.None, "normal filename")]
    [DataRow("control\tchar", SanitizeFlags.None, "control_char")]
    [DataRow("banned*char", SanitizeFlags.None, "banned_char")]
    [DataRow("f:foo", SanitizeFlags.None, "f_foo")]
    [DataRow("f:foo", AllowPath, "f:foo")]
    [DataRow("f:\\foo", SanitizeFlags.None, "f__foo")]
    [DataRow("f:\\foo", AllowPath, "f:\\foo")]
    [DataRow("f:/foo", SanitizeFlags.None, "f__foo")]
    [DataRow("f:/foo", AllowPath, "f:/foo")]
    [DataRow("\\\\?\\C:\\foo", AllowPath, "\\\\?\\C:\\foo")]
    [DataRow("\\\\?\\C:\\foo", SanitizeFlags.None, "____C__foo")]
    [DataRow("foo:bar", SanitizeFlags.None, "foo_bar")]
    [DataRow("foo|<>/bar\\\":?*baz", SanitizeFlags.None, "foo____bar_____baz")]
    [DataRow("f:foo::$DATA", SanitizeFlags.None, "f_foo__$DATA")]
    [DataRow("con . air", SanitizeFlags.None, "con _ air")]
    [DataRow("con.air", SanitizeFlags.None, "con_air")]
    [DataRow("con:/x", SanitizeFlags.None, "con__x")]
    [DataRow("file . . . .  ..  .", SanitizeFlags.None, "file")]
    [DataRow("foo . . ? . . ", SanitizeFlags.None, "foo . . _")]
    [DataRow("com1", SanitizeFlags.None, "_com1")]
    [DataRow("com1", AllowReserved, "com1")]
    [DataRow("f:\\com1", SanitizeFlags.None, "f__com1")]
    [DataRow("f:\\com1", AllowPath, "f:\\_com1")]
    [DataRow("f:\\com1", AllowReserved, "f__com1")]
    [DataRow("f:\\com1", AllowBoth, "f:\\com1")]
    [DataRow("com1:\\com1", AllowPath, "_com1:\\_com1")]
    [DataRow("com1:\\com1", AllowBoth, "com1:\\com1")]
    [DataRow("com1:\\com1", AllowReserved, "com1__com1")]
    [DataRow("\\com1", AllowPath, "\\_com1")]
    [DataRow("\\\\com1", AllowPath, "\\\\com1")]
    [DataRow("\\\\?\\C:\\com1", AllowPath, "\\\\?\\C:\\com1")]
    [DataRow("CoM1", SanitizeFlags.None, "_CoM1")]
    [DataRow("CoM1", AllowReserved, "CoM1")]
    [DataRow("COM56", SanitizeFlags.None, "COM56")]
    public void SanitizeFileName_Tool1604Row_GivesCurlsOutputAndOk(string input, SanitizeFlags flags, string expected)
    {
        SanitizeResult result = SanitizeFileName(input, flags, out string? sanitized);

        Assert.AreEqual(SanitizeResult.Ok, result);
        Assert.AreEqual(expected, sanitized);
    }

    [TestMethod]
    public void SanitizeFileName_NullName_IsBadArgumentWithNoOutput()
    {
        SanitizeResult result = SanitizeFileName(null, SanitizeFlags.None, out string? sanitized);

        Assert.AreEqual(SanitizeResult.BadArgument, result);
        Assert.IsNull(sanitized);
    }

    [TestMethod]
    [DataRow("clock$", "_clock$")]
    [DataRow("PRN.txt", "PRN_txt")]
    [DataRow("aux", "_aux")]
    [DataRow("nul  ", "_nul")]
    [DataRow("lpt9.a.b", "lpt9_a.b")]
    [DataRow("COM", "_COM")]
    [DataRow("com0", "com0")]
    [DataRow("conx", "conx")]
    [DataRow("lp", "lp")]
    [DataRow("a.con", "a.con")]
    public void SanitizeFileName_ReservedDeviceNameCandidates_AreRenamedAsCurlDoes(string input, string expected)
    {
        SanitizeFileName(input, SanitizeFlags.None, out string? sanitized);

        Assert.AreEqual(expected, sanitized);
    }

    [TestMethod]
    [DataRow("com1x", "com1x")]
    [DataRow("dir/con . air", "dir/con _ air")]
    [DataRow("prn:ads", "_prn:ads")]
    [DataRow("dir/x", "dir/x")]
    [DataRow("trailing. ", "trailing. ")]
    public void SanitizeFileName_AllowPath_KeepsPathsAndRenamesOnlyWholeDeviceNames(string input, string expected)
    {
        SanitizeFileName(input, AllowPath, out string? sanitized);

        Assert.AreEqual(expected, sanitized);
    }

    [TestMethod]
    [DataRow("a?b", "a_b")]
    [DataRow("C:/dir/x|y", "C:/dir/x_y")]
    [DataRow("\\\\?\\C:\\a<b", "\\\\?\\C:\\a_b")]
    [DataRow("con", "con")]
    [DataRow("a\u007Fb.", "a\u007Fb.")]
    public void Sanitize_FileName_ReplacesOnlyBannedCharactersAsGlobMatchUrlDoes(string input, string expected)
    {
        Assert.AreEqual(expected, Sanitize(input));
    }
}
