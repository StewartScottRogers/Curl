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

    // No normalisation of any kind: a backslash is just a character in the URL, and on
    // Windows it is already the separator the operating system wants.
    [TestMethod]
    public void TryParse_Backslashes_SurviveUnnormalised()
    {
        var url = new Uri(@"file:///C:/dir\sub\file.txt");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual(@"C:/dir\sub\file.txt", path.UrlPath);
        Assert.AreEqual(NativePath(@"C:/dir\sub\file.txt"), path.OsPath);
    }

    // There is no sandboxing: curl passes .. to the operating system unresolved, and so
    // does this parser. Resolving it here would be a behaviour upstream does not have.
    [TestMethod]
    public void TryParse_DotDotSegment_SurvivesUnresolved()
    {
        var url = new Uri("file:///C:/dir/../secret.txt");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/dir/../secret.txt", path.UrlPath);
        Assert.AreEqual(NativePath("C:/dir/../secret.txt"), path.OsPath);
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
