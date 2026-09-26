namespace Curl.Protocol.File;

/// <summary>
/// Pins the <c>file://</c> URL path split against curl 8.21.0. Every expectation below
/// was measured against that build; where .NET's <see cref="Uri" /> disagrees with curl,
/// curl wins.
/// </summary>
[TestClass]
public sealed class FileUrlPathTests
{
    [TestMethod]
    public void TryParse_NullUrl_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => FileUrlPath.TryParse(null!, out _));
    }

    [TestMethod]
    public void TryParse_NonFileScheme_ReturnsFalse()
    {
        var url = new Uri("http://example.com/x");

        bool parsed = FileUrlPath.TryParse(url, out _);

        Assert.IsFalse(parsed);
    }

    // curl matches the scheme case-insensitively, so FILE: is the same URL as file:.
    [TestMethod]
    public void TryParse_UppercaseScheme_IsAccepted()
    {
        var url = new Uri("FILE:///C:/x");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/x", path.UrlPath);
        Assert.AreEqual(NativePath("C:/x"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_EmptyHostAndDriveLetter_ReturnsBothFormsOfThePath()
    {
        var url = new Uri("file:///C:/dir/hello.txt");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/dir/hello.txt", path.UrlPath);
        Assert.AreEqual(NativePath("C:/dir/hello.txt"), path.OsPath);
    }

    // UrlPath stays encoded because it is the text curl echoes in its exit 37 message;
    // only OsPath is decoded, because only OsPath reaches the operating system.
    [TestMethod]
    public void TryParse_PercentTwentyEscape_DecodesToASpaceInTheOperatingSystemPathOnly()
    {
        var url = new Uri("file:///C:/dir/my%20file.txt");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/dir/my%20file.txt", path.UrlPath);
        Assert.AreEqual(NativePath("C:/dir/my file.txt"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_PercentTwentyFiveEscape_DecodesToASinglePercent()
    {
        var url = new Uri("file:///C:/a%25b.txt");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/a%25b.txt", path.UrlPath);
        Assert.AreEqual(NativePath("C:/a%b.txt"), path.OsPath);
    }

    // A malformed escape is not an error: curl hands the literal text to the operating
    // system, which is what makes a file genuinely named "a%2" openable.
    [TestMethod]
    public void TryParse_TruncatedEscape_IsLeftLiteral()
    {
        var url = new Uri("file:///C:/a%2");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/a%2", path.UrlPath);
        Assert.AreEqual(NativePath("C:/a%2"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_NonHexadecimalEscape_IsLeftLiteral()
    {
        var url = new Uri("file:///C:/a%GGb");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/a%GGb", path.UrlPath);
        Assert.AreEqual(NativePath("C:/a%GGb"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_TrailingPercent_IsLeftLiteral()
    {
        var url = new Uri("file:///C:/a%");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/a%", path.UrlPath);
        Assert.AreEqual(NativePath("C:/a%"), path.OsPath);
    }

    // %2F decodes like any other escape and then becomes a directory separator: curl
    // does not treat an encoded slash as different from a written one.
    [TestMethod]
    public void TryParse_PercentTwoFEscape_DecodesToASeparator()
    {
        var url = new Uri("file:///C:/dir%2Fhello.txt");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/dir%2Fhello.txt", path.UrlPath);
        Assert.AreEqual(NativePath("C:/dir/hello.txt"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_LocalhostHost_IsAcceptedAndDropped()
    {
        var url = new Uri("file://localhost/C:/x");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/x", path.UrlPath);
        Assert.AreEqual(NativePath("C:/x"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_UppercaseLocalhostHost_IsAcceptedAndDropped()
    {
        var url = new Uri("file://LOCALHOST/C:/x");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/x", path.UrlPath);
        Assert.AreEqual(NativePath("C:/x"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_LoopbackAddressHost_IsAcceptedAndDropped()
    {
        var url = new Uri("file://127.0.0.1/C:/x");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/x", path.UrlPath);
        Assert.AreEqual(NativePath("C:/x"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_NamedHost_ReturnsFalse()
    {
        var url = new Uri("file://example.com/x");

        bool parsed = FileUrlPath.TryParse(url, out _);

        Assert.IsFalse(parsed);
    }

    // Even the IPv6 spelling of loopback is rejected: curl accepts only the empty host,
    // localhost and 127.0.0.1.
    [TestMethod]
    public void TryParse_IpVersionSixLoopbackHost_ReturnsFalse()
    {
        var url = new Uri("file://[::1]/x");

        bool parsed = FileUrlPath.TryParse(url, out _);

        Assert.IsFalse(parsed);
    }

    [TestMethod]
    public void TryParse_NonLoopbackAddressHost_ReturnsFalse()
    {
        var url = new Uri("file://127.0.0.2/x");

        bool parsed = FileUrlPath.TryParse(url, out _);

        Assert.IsFalse(parsed);
    }

    [TestMethod]
    public void TryParse_SchemeAndEmptyAuthorityOnly_ReturnsFalse()
    {
        var url = new Uri("file://");

        bool parsed = FileUrlPath.TryParse(url, out _);

        Assert.IsFalse(parsed);
    }

    [TestMethod]
    public void TryParse_AcceptedHostWithNoPath_ReturnsFalse()
    {
        var url = new Uri("file://localhost");

        bool parsed = FileUrlPath.TryParse(url, out _);

        Assert.IsFalse(parsed);
    }

    // Without a drive letter there is nothing to strip, so the root keeps its slash.
    [TestMethod]
    public void TryParse_RootPath_KeepsTheLeadingSlash()
    {
        var url = new Uri("file:///");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/", path.UrlPath);
        Assert.AreEqual(NativePath("/"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_LowercaseDriveLetter_LosesTheLeadingSlash()
    {
        var url = new Uri("file:///c:/Windows/win.ini");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("c:/Windows/win.ini", path.UrlPath);
        Assert.AreEqual(NativePath("c:/Windows/win.ini"), path.OsPath);
    }

    // The bar spelling loses its leading slash like a colon does, but the bar itself is
    // NOT rewritten to a colon. Uri.LocalPath rewrites it; curl 8.21.0 does not, and this
    // test is the reason parsing works from Uri.OriginalString.
    [TestMethod]
    public void TryParse_DriveLetterSpelledWithABar_LosesTheLeadingSlashAndKeepsTheBar()
    {
        var url = new Uri("file:///c|/Windows/win.ini");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("c|/Windows/win.ini", path.UrlPath);
        Assert.AreEqual(NativePath("c|/Windows/win.ini"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_PathWithoutADriveLetter_KeepsTheLeadingSlash()
    {
        var url = new Uri("file:///Windows/win.ini");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/Windows/win.ini", path.UrlPath);
        Assert.AreEqual(NativePath("/Windows/win.ini"), path.OsPath);
    }

    // One slash means there is no authority at all, which curl accepts.
    [TestMethod]
    public void TryParse_SingleSlashAfterScheme_IsAccepted()
    {
        var url = new Uri("file:/C:/x");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/x", path.UrlPath);
        Assert.AreEqual(NativePath("C:/x"), path.OsPath);
    }

    // The one UNC spelling curl accepts. It works precisely by not being mangled: the
    // empty authority goes, both remaining slashes stay.
    [TestMethod]
    public void TryParse_FourSlashUncPath_KeepsBothLeadingSlashes()
    {
        var url = new Uri("file:////localhost/C$/x");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("//localhost/C$/x", path.UrlPath);
        Assert.AreEqual(NativePath("//localhost/C$/x"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_FiveSlashPath_KeepsThreeLeadingSlashes()
    {
        var url = new Uri("file://///localhost/x");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("///localhost/x", path.UrlPath);
        Assert.AreEqual(NativePath("///localhost/x"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_Query_IsDropped()
    {
        var url = new Uri("file:///C:/x?a=1");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/x", path.UrlPath);
        Assert.AreEqual(NativePath("C:/x"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_Fragment_IsDropped()
    {
        var url = new Uri("file:///C:/x#frag");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/x", path.UrlPath);
        Assert.AreEqual(NativePath("C:/x"), path.OsPath);
    }

    // curl 8.21.0 opens C:/secret.txt for this URL: every backslash becomes a slash, and
    // only then are the dot segments removed.
    [TestMethod]
    public void TryParse_BackslashDotDotSegments_ResolveBeforeTheOpen()
    {
        var url = new Uri(@"file:///C:/dir\..\secret.txt");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/secret.txt", path.UrlPath);
        Assert.AreEqual(NativePath("C:/secret.txt"), path.OsPath);
    }

    // C:/b holds only if the backslash became a separator before the .. was resolved:
    // resolved first, "a\.." is one ordinary segment and the path would stay C:/a\../b.
    [TestMethod]
    public void TryParse_BackslashBeforeDotDot_BecomesASeparatorBeforeTheDotDotIsResolved()
    {
        var url = new Uri(@"file:///C:/a\../b");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/b", path.UrlPath);
        Assert.AreEqual(NativePath("C:/b"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_SingleDotSegments_AreRemoved()
    {
        var url = new Uri("file:///C:/./a/./b.txt");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/a/b.txt", path.UrlPath);
        Assert.AreEqual(NativePath("C:/a/b.txt"), path.OsPath);
    }

    // Measured against curl 8.21.0: -w "%{exitcode}" on this URL prints 0 and the body is
    // C:\Windows\win.ini, and file:///C:/../../nosuch.txt is quoted as C:/nosuch.txt. The
    // drive is the root; a .. with nothing left above it is dropped.
    [TestMethod]
    public void TryParse_DotDotAboveTheDrive_StopsAtTheDrive()
    {
        var url = new Uri("file:///C:/../../Windows/win.ini");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/Windows/win.ini", path.UrlPath);
        Assert.AreEqual(NativePath("C:/Windows/win.ini"), path.OsPath);
    }

    // A drive letter not followed by a slash is not a root, so the .. removes it: curl
    // 8.21.0 quotes file://localhost/Q:dir/../x as /x. The three-slash spelling is the
    // same to curl, but Uri throws on it.
    [TestMethod]
    public void TryParse_DriveLetterWithoutASlash_IsRemovedByDotDotLikeAnySegment()
    {
        var url = new Uri("file://localhost/Q:dir/../x");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/x", path.UrlPath);
        Assert.AreEqual(NativePath("/x"), path.OsPath);
    }

    // curl 8.21.0 quotes file://localhost/C: as C: — a drive with nothing after it is
    // still a root. Uri throws on the three-slash spelling, file:///C:.
    [TestMethod]
    public void TryParse_BareDrive_IsKeptWhole()
    {
        var url = new Uri("file://localhost/C:");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:", path.UrlPath);
        Assert.AreEqual("C:", path.OsPath);
    }

    // Each row was quoted by curl 8.21.0 in its exit 37 message exactly as expected here.
    [TestMethod]
    [DataRow("file:///C:/dir/..", "C:/")]
    [DataRow("file:///C:/dir/.", "C:/dir/")]
    [DataRow("file:///C:/dir/../", "C:/")]
    [DataRow("file:///C:/dir/.../nosuch.txt", "C:/dir/.../nosuch.txt")]
    [DataRow("file:///C|/dir/../nosuch.txt", "C|/nosuch.txt")]
    [DataRow("file:///tmp/a/../b", "/tmp/b")]
    [DataRow("file:////server/share/../x.txt", "//server/x.txt")]
    [DataRow("file:////server/../x", "//x")]
    [DataRow("file:////server/share/../../../x", "/x")]
    [DataRow("file://localhost/C:/dir/../nosuch.txt", "C:/nosuch.txt")]
    [DataRow("file:///C:/dir/..?q=1", "C:/")]
    [DataRow(@"file://localhost\C:/dir/../nosuch.txt", "C:/nosuch.txt")]
    [DataRow(@"file:///C:\dir\..\nosuch.txt", "C:/nosuch.txt")]
    public void TryParse_DotSegments_AreRemovedAsCurlQuotesThem(string urlText, string expected)
    {
        var url = new Uri(urlText);

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual(expected, path.UrlPath);
        Assert.AreEqual(NativePath(expected), path.OsPath);
    }

    // curl 8.21.0 reads an encoded dot as a dot when it decides what a dot segment is.
    [TestMethod]
    [DataRow("file:///C:/dir/.%2e/x", "C:/x")]
    [DataRow("file:///C:/dir/%2E%2E/x", "C:/x")]
    [DataRow("file:///C:/dir/%2e%2e/x", "C:/x")]
    [DataRow("file:///C:/dir/%2e/x", "C:/dir/x")]
    [DataRow("file:///C:/dir/%2e", "C:/dir/")]
    public void TryParse_EncodedDotSegments_AreRemovedLikePlainOnes(string urlText, string expected)
    {
        var url = new Uri(urlText);

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual(expected, path.UrlPath);
    }

    // An escaped backslash is not a separator in the URL, so "dir%5c..%5cx" is one
    // segment and nothing is removed.
    [TestMethod]
    public void TryParse_EscapedBackslash_IsNotASeparatorForDotSegmentRemoval()
    {
        var url = new Uri("file:///C:/dir%5c..%5cx");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/dir%5c..%5cx", path.UrlPath);
        Assert.AreEqual(NativePath("C:/dir") + @"\..\x", path.OsPath);
    }

    [TestMethod]
    public void TryParse_PathAsIsFalse_RemovesDotSegments()
    {
        var url = new Uri("file:///C:/dir/../x");

        bool parsed = FileUrlPath.TryParse(url, pathAsIs: false, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/x", path.UrlPath);
        Assert.AreEqual(NativePath("C:/x"), path.OsPath);
    }

    // curl 8.21.0 with --path-as-is quotes file:///C:/dir\..\x as C:/dir/../x: the dot
    // segments survive, the backslashes do not.
    [TestMethod]
    public void TryParse_PathAsIs_KeepsDotDotButStillConvertsBackslashes()
    {
        var url = new Uri(@"file:///C:/dir\..\x");

        bool parsed = FileUrlPath.TryParse(url, pathAsIs: true, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/dir/../x", path.UrlPath);
        Assert.AreEqual(NativePath("C:/dir/../x"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_PathAsIs_KeepsSingleDotSegments()
    {
        var url = new Uri("file:///C:/dir/./x");

        bool parsed = FileUrlPath.TryParse(url, pathAsIs: true, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/dir/./x", path.UrlPath);
    }

    [TestMethod]
    public void TryParse_PathAsIsNullUrl_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => FileUrlPath.TryParse(null!, pathAsIs: true, out _));
    }

    // file://C:/dir/hello.txt is not a host named "C:" — curl 8.21.0 reads the authority
    // as the head of the path, transfers the file and exits 0. It therefore has to parse
    // to exactly what the three-slash spelling parses to.
    [TestMethod]
    public void TryParse_DriveLetterAuthority_KeepsItAsTheHeadOfThePath()
    {
        var url = new Uri("file://C:/dir/hello.txt");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/dir/hello.txt", path.UrlPath);
        Assert.AreEqual(NativePath("C:/dir/hello.txt"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_DriveLetterAuthority_MatchesTheEmptyAuthorityForm()
    {
        var twoSlashes = new Uri("file://C:/dir/hello.txt");
        var threeSlashes = new Uri("file:///C:/dir/hello.txt");

        bool parsedTwo = FileUrlPath.TryParse(twoSlashes, out var fromTwo);
        bool parsedThree = FileUrlPath.TryParse(threeSlashes, out var fromThree);

        Assert.IsTrue(parsedTwo);
        Assert.IsTrue(parsedThree);
        Assert.AreEqual(fromThree, fromTwo);
    }

    // The letter's case does not matter: file://d:/nope.txt was measured at exit 37, which
    // is a failed open of a path and not the exit 3 a rejected host would give.
    [TestMethod]
    public void TryParse_LowercaseDriveLetterAuthority_KeepsItAsTheHeadOfThePath()
    {
        var url = new Uri("file://d:/nope.txt");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("d:/nope.txt", path.UrlPath);
        Assert.AreEqual(NativePath("d:/nope.txt"), path.OsPath);
    }

    // file://D|/nope.txt was measured at exit 37 quoting the path D|/nope.txt, bar and
    // all. Uri.LocalPath would have rewritten the bar to a colon; curl does not, so the
    // bar has to survive both halves of the pair.
    [TestMethod]
    public void TryParse_BarDriveLetterAuthority_KeepsTheBarUnrewritten()
    {
        var url = new Uri("file://D|/nope.txt");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("D|/nope.txt", path.UrlPath);
        Assert.AreEqual(NativePath("D|/nope.txt"), path.OsPath);
    }

    // The drive-letter exception is exactly two characters wide, a letter and then a colon
    // or a bar: everything here was measured at exit 3 instead. The fourth authority curl
    // rejects the same way, ab:, cannot be written as a test at all — new Uri("file://ab:/x")
    // throws UriFormatException ("The hostname could not be parsed") long before this
    // parser sees it, so there is no Uri to hand over.
    [TestMethod]
    [DataRow("file://c/x")]
    [DataRow("file://zz/x")]
    [DataRow("file://1/x")]
    [DataRow("file://example.com/x")]
    public void TryParse_AuthorityThatIsNeitherADriveNorAnAcceptedHost_ReturnsFalse(
        string candidate)
    {
        var url = new Uri(candidate);

        bool parsed = FileUrlPath.TryParse(url, out _);

        Assert.IsFalse(parsed);
    }

    private static string NativePath(string slashedPath) =>
        slashedPath.Replace('/', Path.DirectorySeparatorChar);
}
