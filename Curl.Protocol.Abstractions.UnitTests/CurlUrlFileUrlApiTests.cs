namespace Curl.Protocol.Abstractions.UnitTests;

/// <summary>
/// <c>file</c> URLs as curl 8.22.0's <c>parse_file</c> reads them (lib1560, BL-2737): <c>file:/</c> is
/// <see cref="CurlUrlRejection.BadFileUrl" /> for the tool and the URL API alike; without scheme guessing
/// the URL API also reads <c>file:</c> with no slash as a <c>file</c> URL and rejects its relative path; and
/// the URL API keeps backslashes where the tool turns them into slashes.
/// </summary>
[TestClass]
public sealed class CurlUrlFileUrlApiTests
{
    private static readonly CurlUrlApiFlags NoFlags = new(false, false, false);

    [TestMethod]
    public void TryParse_FileColonSlash_IsBadFileUrl()
    {
        bool parsed = CurlUrl.TryParse("file:/", pathAsIs: false, driveLetters: true, out CurlUrl? url, out CurlUrlRejection rejection);

        Assert.IsFalse(parsed);
        Assert.IsNull(url);
        Assert.AreEqual(CurlUrlRejection.BadFileUrl, rejection);
    }

    [TestMethod]
    [DataRow("file:/")]
    [DataRow("file:./")]
    [DataRow("FILE:foo")]
    [DataRow("file:foo/bar")]
    [DataRow("file:?q")]
    [DataRow("file:#f")]
    public void TryParseForUrlApi_FileUrlTooShortOrWithARelativePath_IsBadFileUrl(string text)
    {
        bool parsed = CurlUrl.TryParseForUrlApi(text, pathAsIs: false, NoFlags, out CurlUrl? url, out CurlUrlRejection rejection);

        Assert.IsFalse(parsed, text);
        Assert.IsNull(url);
        Assert.AreEqual(CurlUrlRejection.BadFileUrl, rejection);
    }

    [TestMethod]
    public void TryParseForUrlApi_FileWithoutSlashWhileGuessingSchemes_IsReadAsAHost()
    {
        bool parsed = CurlUrl.TryParseForUrlApi("file:foo", pathAsIs: false, new CurlUrlApiFlags(false, false, true), out _, out CurlUrlRejection rejection);

        Assert.IsFalse(parsed);
        Assert.AreEqual(CurlUrlRejection.BadPortNumber, rejection);
    }

    [TestMethod]
    [DataRow("file:/h", "/h")]
    [DataRow("file:///", "/")]
    [DataRow("file://localhost/x", "/x")]
    public void TryParseForUrlApi_ShortestFileUrlsWithAnAbsolutePath_AreAccepted(string text, string path)
    {
        bool parsed = CurlUrl.TryParseForUrlApi(text, pathAsIs: false, driveLetters: false, NoFlags, out CurlUrl? url, out _);

        Assert.IsTrue(parsed, text);
        Assert.AreEqual(path, url!.AbsolutePath);
    }

    [TestMethod]
    [DataRow("file://C:\\programs\\foo")]
    [DataRow("file:///C:\\programs\\foo")]
    [DataRow("file:/C:\\programs\\foo")]
    public void TryParseForUrlApi_WindowsDrivePathWithBackslashes_KeepsThem(string text)
    {
        bool parsed = CurlUrl.TryParseForUrlApi(text, pathAsIs: false, driveLetters: true, NoFlags, out CurlUrl? url, out _);

        Assert.IsTrue(parsed, text);
        Assert.AreEqual("C:\\programs\\foo", url!.AbsolutePath);
    }

    [TestMethod]
    public void TryParseForUrlApi_DrivePathOffWindows_IsBadFileUrl()
    {
        bool parsed = CurlUrl.TryParseForUrlApi(
            "file:///C:\\programs\\foo", pathAsIs: false, driveLetters: false, NoFlags, out _, out CurlUrlRejection rejection);

        Assert.IsFalse(parsed);
        Assert.AreEqual(CurlUrlRejection.BadFileUrl, rejection);
    }

    [TestMethod]
    public void TryParse_DrivePathWithBackslashesAfterTwoSlashes_KeepsThemAsTheTool()
    {
        bool parsed = CurlUrl.TryParse("file:///C:\\programs\\foo", pathAsIs: false, driveLetters: true, out CurlUrl? url, out _);

        Assert.IsTrue(parsed);
        Assert.AreEqual("C:\\programs\\foo", url!.AbsolutePath);
    }

    [TestMethod]
    public void TryParseForUrlApi_NetworkUrl_IsParsed()
    {
        bool parsed = CurlUrl.TryParseForUrlApi("http://h/x", pathAsIs: false, NoFlags, out CurlUrl? url, out _);

        Assert.IsTrue(parsed);
        Assert.AreEqual("/x", url!.AbsolutePath);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void TryParseForUrlApi_OneLetterSchemeWithoutGuessing_IsASchemeNotADrive(bool driveLetters)
    {
        // lib1560's huge group (BL-2791): curl skips a drive prefix only when it guesses the scheme.
        bool parsed = CurlUrl.TryParseForUrlApi("c://c:c@c/c?c#c", pathAsIs: false, driveLetters, NoFlags, out CurlUrl? url, out _);

        Assert.IsTrue(parsed);
        Assert.AreEqual("c", url!.Scheme);
        Assert.AreEqual("c", url.Host);
    }

    [TestMethod]
    public void TryParseForUrlApi_OneLetterSchemeWhileGuessingOnWindows_IsNotAScheme()
    {
        bool parsed = CurlUrl.TryParseForUrlApi(
            "c://h/", pathAsIs: false, driveLetters: true, new CurlUrlApiFlags(false, false, true), out CurlUrl? url, out _);

        Assert.IsFalse(parsed && url!.Scheme == "c");
    }

    [TestMethod]
    public void TryParseForUrlApi_NullText_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => CurlUrl.TryParseForUrlApi(null!, pathAsIs: false, NoFlags, out _, out _));
    }
}
