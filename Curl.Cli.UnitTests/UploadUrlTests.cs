namespace Curl.Cli;

/// <summary>
/// Pins how <c>-T</c> / <c>--upload-file</c> completes a URL whose path names no file:
/// curl appends the local file's base name, percent-encoded, when the URL has no non-empty
/// query and its path ends in a slash or is absent, and leaves the URL alone otherwise.
/// Pure string work; no test here touches the file system or the network.
/// </summary>
[TestClass]
public sealed class UploadUrlTests
{
    private const string DirectoryUrl = "http://host/dir/";

    // ---- IsStandardInput ----------------------------------------------------------

    [TestMethod]
    [DataRow("-")]
    [DataRow(".")]
    public void IsStandardInput_DashOrDot_ReturnsTrue(string uploadFile)
    {
        bool isStandardInput = UploadUrl.IsStandardInput(uploadFile);

        Assert.IsTrue(isStandardInput);
    }

    [TestMethod]
    [DataRow("local.txt")]
    [DataRow("--")]
    [DataRow("./-")]
    [DataRow("..")]
    [DataRow("")]
    public void IsStandardInput_AnythingElse_ReturnsFalse(string uploadFile)
    {
        bool isStandardInput = UploadUrl.IsStandardInput(uploadFile);

        Assert.IsFalse(isStandardInput);
    }

    [TestMethod]
    public void IsStandardInput_NullUploadFile_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => UploadUrl.IsStandardInput(null!));
    }

    // ---- AppendLocalFileNameWhenUrlNamesNoFile: argument checks --------------------

    [TestMethod]
    public void AppendLocalFileNameWhenUrlNamesNoFile_NullUrl_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(null!, "local.txt"));
    }

    [TestMethod]
    public void AppendLocalFileNameWhenUrlNamesNoFile_NullUploadFile_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(DirectoryUrl, null!));
    }

    // ---- URL shape -----------------------------------------------------------------

    [TestMethod]
    [DataRow("ftp://host/dir/", "ftp://host/dir/local.txt")]
    [DataRow("http://host/", "http://host/local.txt")]
    [DataRow("http://host/dir/", "http://host/dir/local.txt")]
    public void AppendLocalFileNameWhenUrlNamesNoFile_PathEndsInSlash_AppendsFileName(
        string url, string expected)
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(url, "local.txt");

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("ftp://host/dir")]
    [DataRow("http://host/dir/file.bin")]
    public void AppendLocalFileNameWhenUrlNamesNoFile_PathNamesAFile_ReturnsUrlUnchanged(string url)
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(url, "local.txt");

        Assert.AreEqual(url, result);
    }

    [TestMethod]
    [DataRow("ftp://host", "ftp://host/local.txt")]
    [DataRow("http://host", "http://host/local.txt")]
    public void AppendLocalFileNameWhenUrlNamesNoFile_UrlHasNoPath_AppendsSlashAndFileName(
        string url, string expected)
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(url, "local.txt");

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("http://host/dir/?q=1")]
    [DataRow("ftp://host?x")]
    public void AppendLocalFileNameWhenUrlNamesNoFile_NonEmptyQuery_ReturnsUrlUnchanged(string url)
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(url, "local.txt");

        Assert.AreEqual(url, result);
    }

    [TestMethod]
    public void AppendLocalFileNameWhenUrlNamesNoFile_EmptyQuery_DropsQuestionMarkAndAppends()
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(
            "http://host/dir/?", "local.txt");

        Assert.AreEqual("http://host/dir/local.txt", result);
    }

    [TestMethod]
    [DataRow("http://host/dir/#frag", "http://host/dir/local.txt#frag")]
    [DataRow("http://host/dir/#a?b", "http://host/dir/local.txt#a?b")]
    public void AppendLocalFileNameWhenUrlNamesNoFile_Fragment_AppendsBeforeFragment(
        string url, string expected)
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(url, "local.txt");

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void AppendLocalFileNameWhenUrlNamesNoFile_FragmentAfterPathNamingAFile_ReturnsUrlUnchanged()
    {
        const string url = "http://host/dir#frag";

        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(url, "local.txt");

        Assert.AreEqual(url, result);
    }

    [TestMethod]
    public void AppendLocalFileNameWhenUrlNamesNoFile_EmptyQueryThenFragment_DropsQuestionMarkAndAppendsBeforeFragment()
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(
            "http://host/dir/?#frag", "local.txt");

        Assert.AreEqual("http://host/dir/local.txt#frag", result);
    }

    [TestMethod]
    [DataRow("host/dir/", "host/dir/local.txt")]
    [DataRow("host", "host/local.txt")]
    [DataRow("host/#a?b", "host/local.txt#a?b")]
    [DataRow("mailto:x/dir/", "mailto:x/dir/local.txt")]
    public void AppendLocalFileNameWhenUrlNamesNoFile_NoScheme_TreatsWholeUrlAsHostAndPath(
        string url, string expected)
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(url, "local.txt");

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("http://[::1]:21/dir/", "http://[::1]:21/dir/local.txt")]
    [DataRow("http://[::1]:21", "http://[::1]:21/local.txt")]
    public void AppendLocalFileNameWhenUrlNamesNoFile_IPv6HostWithPort_AppendsAfterAuthority(
        string url, string expected)
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(url, "local.txt");

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void AppendLocalFileNameWhenUrlNamesNoFile_UserInfo_AppendsAfterPath()
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(
            "http://user:pw@host/dir/", "local.txt");

        Assert.AreEqual("http://user:pw@host/dir/local.txt", result);
    }

    [TestMethod]
    public void AppendLocalFileNameWhenUrlNamesNoFile_FtpTypeSuffix_ReturnsUrlUnchanged()
    {
        const string url = "ftp://host/dir/;type=a";

        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(url, "local.txt");

        Assert.AreEqual(url, result);
    }

    [TestMethod]
    [DataRow("bogus://host/dir/", "bogus://host/dir/local.txt")]
    [DataRow("a1+.-://host/", "a1+.-://host/local.txt")]
    public void AppendLocalFileNameWhenUrlNamesNoFile_UnknownScheme_AppendsFileName(
        string url, string expected)
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(url, "local.txt");

        Assert.AreEqual(expected, result);
    }

    // Measured on curl 8.21.0: a scheme followed by ':' and one to three slashes is a
    // scheme. This method does not normalise the slashes (the URL layer does); what it
    // must do is find the path after the authority and append the name.
    [TestMethod]
    [DataRow("http:/host", "http:/host/local.txt")]
    [DataRow("http:///host", "http:///host/local.txt")]
    [DataRow("http:/host/dir/", "http:/host/dir/local.txt")]
    public void AppendLocalFileNameWhenUrlNamesNoFile_SchemeWithOneToThreeSlashes_AppendsFileName(
        string url, string expected)
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(url, "local.txt");

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void AppendLocalFileNameWhenUrlNamesNoFile_SchemeWithOneSlashAndPathNamingAFile_ReturnsUrlUnchanged()
    {
        const string url = "http:/host/dir";

        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(url, "local.txt");

        Assert.AreEqual(url, result);
    }

    // '_' is not a scheme character, so "a_b" is not a scheme: the whole text is host and
    // path, the path starts at the first '/' ("//host/dir/"), and it ends in a slash.
    [TestMethod]
    public void AppendLocalFileNameWhenUrlNamesNoFile_SchemeCandidateWithInvalidCharacter_TreatsWholeUrlAsHostAndPath()
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(
            "a_b://host/dir/", "local.txt");

        Assert.AreEqual("a_b://host/dir/local.txt", result);
    }

    // A scheme must start with a letter, so "1ftp" is not one: the whole text is host
    // and path, the path is "/dir/", and it ends in a slash.
    [TestMethod]
    public void AppendLocalFileNameWhenUrlNamesNoFile_SchemeStartsWithDigit_TreatsWholeUrlAsHostAndPath()
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(
            "1ftp://host/dir/", "local.txt");

        Assert.AreEqual("1ftp://host/dir/local.txt", result);
    }

    [TestMethod]
    public void AppendLocalFileNameWhenUrlNamesNoFile_PercentEncodedPath_LeavesPathEncodingAlone()
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(
            "http://host/d%20x/", "local.txt");

        Assert.AreEqual("http://host/d%20x/local.txt", result);
    }

    [TestMethod]
    public void AppendLocalFileNameWhenUrlNamesNoFile_SpaceInUrlPath_AppendsWithoutValidatingUrl()
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(
            "http://host/d ir/", "local.txt");

        Assert.AreEqual("http://host/d ir/local.txt", result);
    }

    // ---- Upload file name ----------------------------------------------------------

    [TestMethod]
    [DataRow("sub/in.txt", "http://host/dir/in.txt")]
    [DataRow(@"sub\in.txt", "http://host/dir/in.txt")]
    [DataRow(@"C:\Users\x\sub\in.txt", "http://host/dir/in.txt")]
    [DataRow(@"a/b\c/d.txt", "http://host/dir/d.txt")]
    [DataRow(@"a\b/c.txt", "http://host/dir/c.txt")]
    [DataRow("./local.txt", "http://host/dir/local.txt")]
    public void AppendLocalFileNameWhenUrlNamesNoFile_UploadFileHasDirectories_AppendsBaseNameOnly(
        string uploadFile, string expected)
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(DirectoryUrl, uploadFile);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("a b+c%.txt", "http://host/dir/a%20b%2Bc%25.txt")]
    [DataRow("x:y.txt", "http://host/dir/x%3Ay.txt")]
    [DataRow("é.txt", "http://host/dir/%C3%A9.txt")]
    public void AppendLocalFileNameWhenUrlNamesNoFile_ReservedOrNonAsciiCharacters_PercentEncodesAsUtf8(
        string uploadFile, string expected)
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(DirectoryUrl, uploadFile);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void AppendLocalFileNameWhenUrlNamesNoFile_UnreservedCharacters_AppendsUnencoded()
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(DirectoryUrl, "~-_.txt");

        Assert.AreEqual("http://host/dir/~-_.txt", result);
    }

    [TestMethod]
    [DataRow("sub/")]
    [DataRow(@"sub\")]
    [DataRow("")]
    public void AppendLocalFileNameWhenUrlNamesNoFile_UploadFileHasEmptyBaseName_ReturnsUrlUnchanged(
        string uploadFile)
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(DirectoryUrl, uploadFile);

        Assert.AreEqual(DirectoryUrl, result);
    }

    [TestMethod]
    [DataRow("-")]
    [DataRow(".")]
    public void AppendLocalFileNameWhenUrlNamesNoFile_StandardInputUpload_ReturnsUrlUnchanged(
        string uploadFile)
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(DirectoryUrl, uploadFile);

        Assert.AreEqual(DirectoryUrl, result);
    }

    // The name is taken from the argument text alone; "nosuchfile" does not exist, and
    // whether it does is the upload's concern, not this method's.
    [TestMethod]
    public void AppendLocalFileNameWhenUrlNamesNoFile_AnyUploadFileName_AppendsNameFromArgumentText()
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(DirectoryUrl, "nosuchfile");

        Assert.AreEqual("http://host/dir/nosuchfile", result);
    }
}
