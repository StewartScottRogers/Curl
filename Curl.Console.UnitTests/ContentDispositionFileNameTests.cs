using System.Text;

using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the <c>-J</c> name read from a <c>Content-Disposition</c> line, the cases measured with
/// curl 8.21.0 on 2026-09-27 (BL-239 Notes) and the rest following its <c>parse_filename</c>.
/// </summary>
[TestClass]
public sealed class ContentDispositionFileNameTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Find_DoubleQuotedName_GivesTheNameInsideTheQuotes()
    {
        string line = "Content-Disposition: attachment; filename=\"x y.txt\"\r\n";
        Diagnostics.Arrange("header line", Escaped(line));

        string? name = Find(line);

        Diagnostics.Act("file name", name);
        Diagnostics.Assert("file name", "x y.txt", name);
        Assert.AreEqual("x y.txt", Find(line));
    }

    [TestMethod]
    public void Find_SingleQuotedName_GivesTheNameInsideTheQuotes()
    {
        string line = "Content-Disposition: attachment; filename='sq.txt'\r\n";
        Diagnostics.Arrange("header line", Escaped(line));

        string? name = Find(line);

        Diagnostics.Act("file name", name);
        Diagnostics.Assert("file name", "sq.txt", name);
        Assert.AreEqual("sq.txt", Find(line));
    }

    [TestMethod]
    public void Find_UnquotedNameBeforeAnotherParameter_StopsAtTheSemicolon()
    {
        string line = "Content-Disposition: attachment; filename=plain.txt; size=5\r\n";
        Diagnostics.Arrange("header line", Escaped(line));

        string? name = Find(line);

        Diagnostics.Act("file name", name);
        Diagnostics.Assert("file name", "plain.txt", name);
        Assert.AreEqual("plain.txt", Find(line));
    }

    [TestMethod]
    public void Find_UnquotedNameAtLineEnd_StopsAtTheCarriageReturn()
    {
        string line = "Content-Disposition: attachment; filename=plain.txt\r\n";
        Diagnostics.Arrange("header line", Escaped(line));

        string? name = Find(line);

        Diagnostics.Act("file name", name);
        Diagnostics.Assert("file name", "plain.txt", name);
        Assert.AreEqual("plain.txt", Find(line));
    }

    [TestMethod]
    public void Find_UnquotedNameWithBareLineFeed_StopsAtTheLineFeed()
    {
        string line = "Content-Disposition: attachment; filename=plain.txt\n";
        Diagnostics.Arrange("header line", Escaped(line));

        string? name = Find(line);

        Diagnostics.Act("file name", name);
        Diagnostics.Assert("file name", "plain.txt", name);
        Assert.AreEqual("plain.txt", Find(line));
    }

    [TestMethod]
    public void Find_NoSpaceAfterSemicolon_GivesTheName()
    {
        string line = "Content-Disposition: attachment;filename=semi.txt;\r\n";
        Diagnostics.Arrange("header line", Escaped(line));

        string? name = Find(line);

        Diagnostics.Act("file name", name);
        Diagnostics.Assert("file name", "semi.txt", name);
        Assert.AreEqual("semi.txt", Find(line));
    }

    [TestMethod]
    public void Find_HeaderNameInAnyCase_GivesTheName()
    {
        string line = "CONTENT-DISPOSITION: filename=a.txt\r\n";
        Diagnostics.Arrange("header line", Escaped(line));

        string? name = Find(line);

        Diagnostics.Act("file name", name);
        Diagnostics.Assert("file name", "a.txt", name);
        Assert.AreEqual("a.txt", Find(line));
    }

    [TestMethod]
    public void Find_PathWithSlashes_GivesThePartAfterTheLastSlash()
    {
        string line = "Content-Disposition: attachment; filename=\"../../dir/evil.txt\"\r\n";
        Diagnostics.Arrange("header line", Escaped(line));

        string? name = Find(line);

        Diagnostics.Act("file name", name);
        Diagnostics.Assert("file name", "evil.txt", name);
        Assert.AreEqual("evil.txt", Find(line));
    }

    [TestMethod]
    public void Find_PathWithBackslashes_GivesThePartAfterTheLastBackslash()
    {
        string line = "Content-Disposition: attachment; filename=\"C:\\x\\win.txt\"\r\n";
        Diagnostics.Arrange("header line", Escaped(line));

        string? name = Find(line);

        Diagnostics.Act("file name", name);
        Diagnostics.Assert("file name", "win.txt", name);
        Assert.AreEqual("win.txt", Find(line));
    }

    [TestMethod]
    public void Find_NameEndingInSlash_GivesNull()
    {
        string line = "Content-Disposition: attachment; filename=\"dir/\"\r\n";
        Diagnostics.Arrange("header line", Escaped(line));

        string? name = Find(line);

        Diagnostics.Act("file name", name);
        Diagnostics.Assert("file name", null, name);
        Assert.IsNull(Find(line));
    }

    [TestMethod]
    public void Find_EmptyQuotedName_GivesTheEmptyName()
    {
        string line = "Content-Disposition: attachment; filename=\"\"\r\n";
        Diagnostics.Arrange("header line", Escaped(line));

        string? name = Find(line);

        Diagnostics.Act("file name", name);
        Diagnostics.Assert("file name", string.Empty, name);
        Assert.AreEqual(string.Empty, Find(line));
    }

    [TestMethod]
    public void Find_EmptyValueAtLineEnd_GivesTheEmptyName()
    {
        string line = "Content-Disposition: attachment; filename=";
        Diagnostics.Arrange("header line", Escaped(line));

        string? name = Find(line);

        Diagnostics.Act("file name", name);
        Diagnostics.Assert("file name", string.Empty, name);
        Assert.AreEqual(string.Empty, Find(line));
    }

    [TestMethod]
    public void Find_EncodedNameOnly_GivesNull()
    {
        string line = "Content-Disposition: attachment; filename*=UTF-8''star.txt\r\n";
        Diagnostics.Arrange("header line", Escaped(line));

        string? name = Find(line);

        Diagnostics.Act("file name", name);
        Diagnostics.Assert("file name", null, name);
        Assert.IsNull(Find(line));
    }

    [TestMethod]
    public void Find_ParameterNameInOtherCase_GivesNull()
    {
        string line = "Content-Disposition: attachment; FileName=Up.txt\r\n";
        Diagnostics.Arrange("header line", Escaped(line));

        string? name = Find(line);

        Diagnostics.Act("file name", name);
        Diagnostics.Assert("file name", null, name);
        Assert.IsNull(Find(line));
    }

    [TestMethod]
    public void Find_NoParameters_GivesNull()
    {
        string line = "Content-Disposition: inline\r\n";
        Diagnostics.Arrange("header line", Escaped(line));

        string? name = Find(line);

        Diagnostics.Act("file name", name);
        Diagnostics.Assert("file name", null, name);
        Assert.IsNull(Find(line));
    }

    [TestMethod]
    public void Find_LongParameterWithoutSemicolon_GivesNull()
    {
        string line = "Content-Disposition: attachment-without-parameters\r\n";
        Diagnostics.Arrange("header line", Escaped(line));

        string? name = Find(line);

        Diagnostics.Act("file name", name);
        Diagnostics.Assert("file name", null, name);
        Assert.IsNull(Find(line));
    }

    [TestMethod]
    public void Find_OnlySeparatorsAfterTheHeaderName_GivesNull()
    {
        string line = "Content-Disposition: ;;;\r\n";
        Diagnostics.Arrange("header line", Escaped(line));

        string? name = Find(line);

        Diagnostics.Act("file name", name);
        Diagnostics.Assert("file name", null, name);
        Assert.IsNull(Find(line));
    }

    [TestMethod]
    public void Find_OtherHeader_GivesNull()
    {
        string line = "Content-Location: filename=x.txt\r\n";
        Diagnostics.Arrange("header line", Escaped(line));

        string? name = Find(line);

        Diagnostics.Act("file name", name);
        Diagnostics.Assert("file name", null, name);
        Assert.IsNull(Find(line));
    }

    [TestMethod]
    public void Find_HeaderNameOnly_GivesNull()
    {
        string line = "Content-Disposition:";
        Diagnostics.Arrange("header line", Escaped(line));

        string? name = Find(line);

        Diagnostics.Act("file name", name);
        Diagnostics.Assert("file name", null, name);
        Assert.IsNull(Find(line));
    }

    [TestMethod]
    public void Find_Utf8Name_DecodesItAsUtf8()
    {
        byte[] line = Encoding.UTF8.GetBytes("Content-Disposition: attachment; filename=\"caf\u00E9.txt\"\r\n");
        Diagnostics.Bytes("header line", line);
        Diagnostics.Arrange("header line length", line.Length);

        string? name = ContentDispositionFileName.Find(line);

        Diagnostics.Act("file name length", name?.Length);
        Diagnostics.Assert("file name", "caf\u00E9.txt", name);
        Assert.AreEqual("caf\u00E9.txt", ContentDispositionFileName.Find(line));
    }

    private static string Escaped(string line) => line.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    private static string? Find(string line) => ContentDispositionFileName.Find(Encoding.Latin1.GetBytes(line));
}
