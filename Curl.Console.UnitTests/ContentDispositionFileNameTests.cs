using System.Text;

namespace Curl.Console;

/// <summary>
/// Pins the <c>-J</c> name read from a <c>Content-Disposition</c> line, the cases measured with
/// curl 8.21.0 on 2026-09-27 (BL-239 Notes) and the rest following its <c>parse_filename</c>.
/// </summary>
[TestClass]
public sealed class ContentDispositionFileNameTests
{
    [TestMethod]
    public void Find_DoubleQuotedName_GivesTheNameInsideTheQuotes() =>
        Assert.AreEqual("x y.txt", Find("Content-Disposition: attachment; filename=\"x y.txt\"\r\n"));

    [TestMethod]
    public void Find_SingleQuotedName_GivesTheNameInsideTheQuotes() =>
        Assert.AreEqual("sq.txt", Find("Content-Disposition: attachment; filename='sq.txt'\r\n"));

    [TestMethod]
    public void Find_UnquotedNameBeforeAnotherParameter_StopsAtTheSemicolon() =>
        Assert.AreEqual("plain.txt", Find("Content-Disposition: attachment; filename=plain.txt; size=5\r\n"));

    [TestMethod]
    public void Find_UnquotedNameAtLineEnd_StopsAtTheCarriageReturn() =>
        Assert.AreEqual("plain.txt", Find("Content-Disposition: attachment; filename=plain.txt\r\n"));

    [TestMethod]
    public void Find_UnquotedNameWithBareLineFeed_StopsAtTheLineFeed() =>
        Assert.AreEqual("plain.txt", Find("Content-Disposition: attachment; filename=plain.txt\n"));

    [TestMethod]
    public void Find_NoSpaceAfterSemicolon_GivesTheName() =>
        Assert.AreEqual("semi.txt", Find("Content-Disposition: attachment;filename=semi.txt;\r\n"));

    [TestMethod]
    public void Find_HeaderNameInAnyCase_GivesTheName() =>
        Assert.AreEqual("a.txt", Find("CONTENT-DISPOSITION: filename=a.txt\r\n"));

    [TestMethod]
    public void Find_PathWithSlashes_GivesThePartAfterTheLastSlash() =>
        Assert.AreEqual("evil.txt", Find("Content-Disposition: attachment; filename=\"../../dir/evil.txt\"\r\n"));

    [TestMethod]
    public void Find_PathWithBackslashes_GivesThePartAfterTheLastBackslash() =>
        Assert.AreEqual("win.txt", Find("Content-Disposition: attachment; filename=\"C:\\x\\win.txt\"\r\n"));

    [TestMethod]
    public void Find_NameEndingInSlash_GivesNull() =>
        Assert.IsNull(Find("Content-Disposition: attachment; filename=\"dir/\"\r\n"));

    [TestMethod]
    public void Find_EmptyQuotedName_GivesTheEmptyName() =>
        Assert.AreEqual(string.Empty, Find("Content-Disposition: attachment; filename=\"\"\r\n"));

    [TestMethod]
    public void Find_EmptyValueAtLineEnd_GivesTheEmptyName() =>
        Assert.AreEqual(string.Empty, Find("Content-Disposition: attachment; filename="));

    [TestMethod]
    public void Find_EncodedNameOnly_GivesNull() =>
        Assert.IsNull(Find("Content-Disposition: attachment; filename*=UTF-8''star.txt\r\n"));

    [TestMethod]
    public void Find_ParameterNameInOtherCase_GivesNull() =>
        Assert.IsNull(Find("Content-Disposition: attachment; FileName=Up.txt\r\n"));

    [TestMethod]
    public void Find_NoParameters_GivesNull() =>
        Assert.IsNull(Find("Content-Disposition: inline\r\n"));

    [TestMethod]
    public void Find_LongParameterWithoutSemicolon_GivesNull() =>
        Assert.IsNull(Find("Content-Disposition: attachment-without-parameters\r\n"));

    [TestMethod]
    public void Find_OnlySeparatorsAfterTheHeaderName_GivesNull() =>
        Assert.IsNull(Find("Content-Disposition: ;;;\r\n"));

    [TestMethod]
    public void Find_OtherHeader_GivesNull() =>
        Assert.IsNull(Find("Content-Location: filename=x.txt\r\n"));

    [TestMethod]
    public void Find_HeaderNameOnly_GivesNull() =>
        Assert.IsNull(Find("Content-Disposition:"));

    [TestMethod]
    public void Find_Utf8Name_DecodesItAsUtf8() =>
        Assert.AreEqual("caf\u00E9.txt", ContentDispositionFileName.Find(
            Encoding.UTF8.GetBytes("Content-Disposition: attachment; filename=\"caf\u00E9.txt\"\r\n")));

    private static string? Find(string line) => ContentDispositionFileName.Find(Encoding.Latin1.GetBytes(line));
}
