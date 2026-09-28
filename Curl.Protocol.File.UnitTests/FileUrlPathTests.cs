using Curl.Protocol.Abstractions;

namespace Curl.Protocol.File;

/// <summary>
/// Pins the <c>file://</c> URL path split against curl 8.21.0. Every expectation below
/// was measured against that build. A URL <see cref="CurlUrl" /> rejects never reaches
/// <see cref="FileUrlPath" />, so the hosts curl refuses are pinned here against
/// <see cref="CurlUrl.TryParse(string, bool, out CurlUrl)" />.
/// </summary>
/// <remarks>
/// curl accepts a Windows drive letter in a <c>file://</c> URL only on Windows (ADR-0010),
/// so a test whose subject is the drive letter runs only there; the non-Windows rejection
/// is pinned in <c>Curl.Protocol.Abstractions.UnitTests</c>. The exceptions are the tests
/// that pass <c>driveLetters</c> explicitly with a URL every build accepts: they pin
/// both platforms' slash rule on every OS. Every other test uses a
/// drive-less path, which every curl build accepts. The drive-less expectations follow
/// the same rules curl 8.21.0 was measured applying on Windows with a <c>C:</c> path.
/// </remarks>
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
        var url = CurlUrl.Parse("http://example.com/x");

        bool parsed = FileUrlPath.TryParse(url, out _);

        Assert.IsFalse(parsed);
    }

    // curl matches the scheme case-insensitively, so FILE: is the same URL as file:.
    [TestMethod]
    public void TryParse_UppercaseScheme_IsAccepted()
    {
        var url = CurlUrl.Parse("FILE:///tmp/x");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/x", path.UrlPath);
        Assert.AreEqual(NativePath("/tmp/x"), path.OsPath);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TryParse_EmptyHostAndDriveLetter_ReturnsBothFormsOfThePath()
    {
        var url = CurlUrl.Parse("file:///C:/dir/hello.txt");

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
        var url = CurlUrl.Parse("file:///tmp/dir/my%20file.txt");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/dir/my%20file.txt", path.UrlPath);
        Assert.AreEqual(NativePath("/tmp/dir/my file.txt"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_PercentTwentyFiveEscape_DecodesToASinglePercent()
    {
        var url = CurlUrl.Parse("file:///tmp/a%25b.txt");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/a%25b.txt", path.UrlPath);
        Assert.AreEqual(NativePath("/tmp/a%b.txt"), path.OsPath);
    }

    // A malformed escape is not an error: curl hands the literal text to the operating
    // system, which is what makes a file genuinely named "a%2" openable.
    [TestMethod]
    public void TryParse_TruncatedEscape_IsLeftLiteral()
    {
        var url = CurlUrl.Parse("file:///tmp/a%2");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/a%2", path.UrlPath);
        Assert.AreEqual(NativePath("/tmp/a%2"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_NonHexadecimalEscape_IsLeftLiteral()
    {
        var url = CurlUrl.Parse("file:///tmp/a%GGb");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/a%GGb", path.UrlPath);
        Assert.AreEqual(NativePath("/tmp/a%GGb"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_TrailingPercent_IsLeftLiteral()
    {
        var url = CurlUrl.Parse("file:///tmp/a%");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/a%", path.UrlPath);
        Assert.AreEqual(NativePath("/tmp/a%"), path.OsPath);
    }

    // %2F decodes like any other escape and then becomes a directory separator: curl
    // does not treat an encoded slash as different from a written one.
    [TestMethod]
    public void TryParse_PercentTwoFEscape_DecodesToASeparator()
    {
        var url = CurlUrl.Parse("file:///tmp/dir%2Fhello.txt");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/dir%2Fhello.txt", path.UrlPath);
        Assert.AreEqual(NativePath("/tmp/dir/hello.txt"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_LocalhostHost_IsAcceptedAndDropped()
    {
        var url = CurlUrl.Parse("file://localhost/tmp/x");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/x", path.UrlPath);
        Assert.AreEqual(NativePath("/tmp/x"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_UppercaseLocalhostHost_IsAcceptedAndDropped()
    {
        var url = CurlUrl.Parse("file://LOCALHOST/tmp/x");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/x", path.UrlPath);
        Assert.AreEqual(NativePath("/tmp/x"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_LoopbackAddressHost_IsAcceptedAndDropped()
    {
        var url = CurlUrl.Parse("file://127.0.0.1/tmp/x");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/x", path.UrlPath);
        Assert.AreEqual(NativePath("/tmp/x"), path.OsPath);
    }

    [TestMethod]
    public void CurlUrlTryParse_NamedHost_ReturnsFalse()
    {
        bool parsed = CurlUrl.TryParse("file://example.com/x", pathAsIs: false, out _);

        Assert.IsFalse(parsed);
    }

    // Even the IPv6 spelling of loopback is rejected: curl accepts only the empty host,
    // localhost and 127.0.0.1.
    [TestMethod]
    public void CurlUrlTryParse_IpVersionSixLoopbackHost_ReturnsFalse()
    {
        bool parsed = CurlUrl.TryParse("file://[::1]/x", pathAsIs: false, out _);

        Assert.IsFalse(parsed);
    }

    [TestMethod]
    public void CurlUrlTryParse_NonLoopbackAddressHost_ReturnsFalse()
    {
        bool parsed = CurlUrl.TryParse("file://127.0.0.2/x", pathAsIs: false, out _);

        Assert.IsFalse(parsed);
    }

    [TestMethod]
    public void CurlUrlTryParse_SchemeAndEmptyAuthorityOnly_ReturnsFalse()
    {
        bool parsed = CurlUrl.TryParse("file://", pathAsIs: false, out _);

        Assert.IsFalse(parsed);
    }

    [TestMethod]
    public void CurlUrlTryParse_AcceptedHostWithNoPath_ReturnsFalse()
    {
        bool parsed = CurlUrl.TryParse("file://localhost", pathAsIs: false, out _);

        Assert.IsFalse(parsed);
    }

    // Without a drive letter there is nothing to strip, so the root keeps its slash.
    [TestMethod]
    public void TryParse_RootPath_KeepsTheLeadingSlash()
    {
        var url = CurlUrl.Parse("file:///");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/", path.UrlPath);
        Assert.AreEqual(NativePath("/"), path.OsPath);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TryParse_LowercaseDriveLetter_LosesTheLeadingSlash()
    {
        var url = CurlUrl.Parse("file:///c:/Windows/win.ini");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("c:/Windows/win.ini", path.UrlPath);
        Assert.AreEqual(NativePath("c:/Windows/win.ini"), path.OsPath);
    }

    // The bar spelling loses its leading slash like a colon does, but the bar itself is
    // NOT rewritten to a colon. Measured against curl 8.21.0 (Windows, Schannel) with
    // Record-CurlExchange.ps1: `curl -sS -o NUL file:///c|/Windows/win.ini` exits 37 with
    // "curl: (37) Could not open file c|/Windows/win.ini" although C:\Windows\win.ini
    // exists, while the same URL spelled c: exits 0. The X| to X: rewrite in lib/file.c
    // only fires on a path that still starts with a slash, and on Windows the URL parser
    // has already dropped it, so curl opens c|\Windows\win.ini and fails.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TryParse_DriveLetterSpelledWithABar_LosesTheLeadingSlashAndKeepsTheBar()
    {
        var url = CurlUrl.Parse("file:///c|/Windows/win.ini");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("c|/Windows/win.ini", path.UrlPath);
        Assert.AreEqual(NativePath("c|/Windows/win.ini"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_PathWithoutADriveLetter_KeepsTheLeadingSlash()
    {
        var url = CurlUrl.Parse("file:///Windows/win.ini");

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
        var url = CurlUrl.Parse("file:/tmp/x");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/x", path.UrlPath);
        Assert.AreEqual(NativePath("/tmp/x"), path.OsPath);
    }

    // The one UNC spelling curl accepts. It works precisely by not being mangled: the
    // empty authority goes, both remaining slashes stay.
    [TestMethod]
    public void TryParse_FourSlashUncPath_KeepsBothLeadingSlashes()
    {
        var url = CurlUrl.Parse("file:////localhost/C$/x");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("//localhost/C$/x", path.UrlPath);
        Assert.AreEqual(NativePath("//localhost/C$/x"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_FiveSlashPath_KeepsThreeLeadingSlashes()
    {
        var url = CurlUrl.Parse("file://///localhost/x");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("///localhost/x", path.UrlPath);
        Assert.AreEqual(NativePath("///localhost/x"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_Query_IsDropped()
    {
        var url = CurlUrl.Parse("file:///tmp/x?a=1");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/x", path.UrlPath);
        Assert.AreEqual(NativePath("/tmp/x"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_Fragment_IsDropped()
    {
        var url = CurlUrl.Parse("file:///tmp/x#frag");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/x", path.UrlPath);
        Assert.AreEqual(NativePath("/tmp/x"), path.OsPath);
    }

    // curl 8.21.0 opens C:/secret.txt for file:///C:/dir\..\secret.txt: every backslash
    // becomes a slash, and only then are the dot segments removed.
    [TestMethod]
    public void TryParse_BackslashDotDotSegments_ResolveBeforeTheOpen()
    {
        var url = CurlUrl.Parse(@"file:///tmp/dir\..\secret.txt");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/secret.txt", path.UrlPath);
        Assert.AreEqual(NativePath("/tmp/secret.txt"), path.OsPath);
    }

    // /tmp/b holds only if the backslash became a separator before the .. was resolved:
    // resolved first, "a\.." is one ordinary segment and the path would stay /tmp/a\../b.
    [TestMethod]
    public void TryParse_BackslashBeforeDotDot_BecomesASeparatorBeforeTheDotDotIsResolved()
    {
        var url = CurlUrl.Parse(@"file:///tmp/a\../b");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/b", path.UrlPath);
        Assert.AreEqual(NativePath("/tmp/b"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_SingleDotSegments_AreRemoved()
    {
        var url = CurlUrl.Parse("file:///tmp/./a/./b.txt");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/a/b.txt", path.UrlPath);
        Assert.AreEqual(NativePath("/tmp/a/b.txt"), path.OsPath);
    }

    // Measured against curl 8.21.0: -w "%{exitcode}" on this URL prints 0 and the body is
    // C:\Windows\win.ini, and file:///C:/../../nosuch.txt is quoted as C:/nosuch.txt. The
    // drive is the root; a .. with nothing left above it is dropped.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TryParse_DotDotAboveTheDrive_StopsAtTheDrive()
    {
        var url = CurlUrl.Parse("file:///C:/../../Windows/win.ini");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/Windows/win.ini", path.UrlPath);
        Assert.AreEqual(NativePath("C:/Windows/win.ini"), path.OsPath);
    }

    // A drive letter not followed by a slash is not a root, so the .. removes it: curl
    // 8.21.0 quotes file://localhost/Q:dir/../x as /x. The three-slash spelling is the
    // same to curl; TryParse_SpellingUriRefused_QuotesThePathCurlQuotes covers it.
    [TestMethod]
    public void TryParse_DriveLetterWithoutASlash_IsRemovedByDotDotLikeAnySegment()
    {
        var url = CurlUrl.Parse("file://localhost/Q:dir/../x");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/x", path.UrlPath);
        Assert.AreEqual(NativePath("/x"), path.OsPath);
    }

    // curl 8.21.0 quotes file://localhost/C: as C: — a drive with nothing after it is
    // still a root. TryParse_SpellingUriRefused_QuotesThePathCurlQuotes covers file:///C:.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TryParse_BareDrive_IsKeptWhole()
    {
        var url = CurlUrl.Parse("file://localhost/C:");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:", path.UrlPath);
        Assert.AreEqual("C:", path.OsPath);
    }

    // The spellings System.Uri refused (ADR-0010). Measured 2026-09-27 against curl 8.21.0
    // with curl -sS -o /dev/null URL: file://C: and file:///C: print
    // "curl: (37) Could not open file C:" and file:///Q:dir/../x prints
    // "curl: (37) Could not open file /x".
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("file://C:", "C:")]
    [DataRow("file:///C:", "C:")]
    [DataRow("file:///Q:dir/../x", "/x")]
    public void TryParse_SpellingUriRefused_QuotesThePathCurlQuotes(string text, string urlPath)
    {
        var url = CurlUrl.Parse(text);

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual(urlPath, path.UrlPath);
        Assert.AreEqual(NativePath(urlPath), path.OsPath);
    }

    // curl 8.21.0 exits 0 for file:///C:%2FWindows/win.ini and writes C:\Windows\win.ini:
    // the escaped slash ends the drive, and the drive's leading slash still goes.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TryParse_DriveFollowedByAnEscapedSlash_OpensThePathTheEscapeSpells()
    {
        var url = CurlUrl.Parse("file:///C:%2FWindows/win.ini");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:%2FWindows/win.ini", path.UrlPath);
        Assert.AreEqual(NativePath("C:/Windows/win.ini"), path.OsPath);
    }

    // curl 8.21.0 strips the slash in front of a drive only inside #ifdef DOS_FILESYSTEM
    // in lib/file.c (file_connect), so the Linux and macOS builds open these URLs as the
    // absolute paths /C:/Windows/win.ini and /Q:dir/x. CurlUrl accepts both off Windows
    // because neither drive is followed by a written slash.
    [TestMethod]
    [DataRow("file:///C:%2FWindows/win.ini", "/C:%2FWindows/win.ini", "/C:/Windows/win.ini")]
    [DataRow("file://localhost/Q:dir/x", "/Q:dir/x", "/Q:dir/x")]
    public void TryParse_DriveLettersOff_KeepsTheLeadingSlash(
        string text,
        string urlPath,
        string osPath)
    {
        var url = CurlUrl.Parse(text);

        bool parsed = FileUrlPath.TryParse(url, driveLetters: false, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual(urlPath, path.UrlPath);
        Assert.AreEqual(NativePath(osPath), path.OsPath);
        Assert.StartsWith(Path.DirectorySeparatorChar.ToString(), path.OsPath);
    }

    [TestMethod]
    [DataRow("file:///C:%2FWindows/win.ini", "C:%2FWindows/win.ini", "C:/Windows/win.ini")]
    [DataRow("file://localhost/Q:dir/x", "Q:dir/x", "Q:dir/x")]
    public void TryParse_DriveLettersOn_DropsTheLeadingSlash(
        string text,
        string urlPath,
        string osPath)
    {
        var url = CurlUrl.Parse(text);

        bool parsed = FileUrlPath.TryParse(url, driveLetters: true, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual(urlPath, path.UrlPath);
        Assert.AreEqual(NativePath(osPath), path.OsPath);
    }

    // Only the Windows build accepts a drive followed by a written slash.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TryParse_DriveLettersOnWithAWrittenSlash_DropsTheLeadingSlash()
    {
        var url = CurlUrl.Parse("file:///C:/Windows/win.ini");

        bool parsed = FileUrlPath.TryParse(url, driveLetters: true, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/Windows/win.ini", path.UrlPath);
        Assert.AreEqual(NativePath("C:/Windows/win.ini"), path.OsPath);
    }

    // The /tmp/a/../b row and the //server rows were quoted by curl 8.21.0 in its exit 37
    // message exactly as expected here; the other rows are the C: rows of
    // TryParse_DotSegmentsAfterADrive_AreRemovedAsCurlQuotesThem without the drive.
    [TestMethod]
    [DataRow("file:///tmp/dir/..", "/tmp/")]
    [DataRow("file:///tmp/dir/.", "/tmp/dir/")]
    [DataRow("file:///tmp/dir/../", "/tmp/")]
    [DataRow("file:///tmp/dir/.../nosuch.txt", "/tmp/dir/.../nosuch.txt")]
    [DataRow("file:///tmp/a/../b", "/tmp/b")]
    [DataRow("file:////server/share/../x.txt", "//server/x.txt")]
    [DataRow("file:////server/../x", "//x")]
    [DataRow("file:////server/share/../../../x", "/x")]
    [DataRow("file://localhost/tmp/dir/../nosuch.txt", "/tmp/nosuch.txt")]
    [DataRow("file:///tmp/dir/..?q=1", "/tmp/")]
    [DataRow(@"file://localhost\tmp/dir/../nosuch.txt", "/tmp/nosuch.txt")]
    [DataRow(@"file:///tmp\dir\..\nosuch.txt", "/tmp/nosuch.txt")]
    public void TryParse_DotSegments_AreRemovedAsCurlQuotesThem(string urlText, string expected)
    {
        var url = CurlUrl.Parse(urlText);

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual(expected, path.UrlPath);
        Assert.AreEqual(NativePath(expected), path.OsPath);
    }

    // Each row was quoted by curl 8.21.0 in its exit 37 message exactly as expected here.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("file:///C:/dir/..", "C:/")]
    [DataRow("file:///C:/dir/.", "C:/dir/")]
    [DataRow("file:///C:/dir/../", "C:/")]
    [DataRow("file:///C:/dir/.../nosuch.txt", "C:/dir/.../nosuch.txt")]
    [DataRow("file:///C|/dir/../nosuch.txt", "C|/nosuch.txt")]
    [DataRow("file://localhost/C:/dir/../nosuch.txt", "C:/nosuch.txt")]
    [DataRow("file:///C:/dir/..?q=1", "C:/")]
    [DataRow(@"file://localhost\C:/dir/../nosuch.txt", "C:/nosuch.txt")]
    [DataRow(@"file:///C:\dir\..\nosuch.txt", "C:/nosuch.txt")]
    public void TryParse_DotSegmentsAfterADrive_AreRemovedAsCurlQuotesThem(
        string urlText,
        string expected)
    {
        var url = CurlUrl.Parse(urlText);

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual(expected, path.UrlPath);
        Assert.AreEqual(NativePath(expected), path.OsPath);
    }

    // curl 8.21.0 reads an encoded dot as a dot when it decides what a dot segment is.
    [TestMethod]
    [DataRow("file:///tmp/dir/.%2e/x", "/tmp/x")]
    [DataRow("file:///tmp/dir/%2E%2E/x", "/tmp/x")]
    [DataRow("file:///tmp/dir/%2e%2e/x", "/tmp/x")]
    [DataRow("file:///tmp/dir/%2e/x", "/tmp/dir/x")]
    [DataRow("file:///tmp/dir/%2e", "/tmp/dir/")]
    public void TryParse_EncodedDotSegments_AreRemovedLikePlainOnes(string urlText, string expected)
    {
        var url = CurlUrl.Parse(urlText);

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
        var url = CurlUrl.Parse("file:///tmp/dir%5c..%5cx");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/dir%5C..%5Cx", path.UrlPath);
        Assert.AreEqual(NativePath("/tmp/dir") + @"\..\x", path.OsPath);
    }

    [TestMethod]
    public void TryParse_PathAsIsFalse_RemovesDotSegments()
    {
        var url = CurlUrl.Parse("file:///tmp/dir/../x");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/x", path.UrlPath);
        Assert.AreEqual(NativePath("/tmp/x"), path.OsPath);
    }

    // curl 8.21.0 with --path-as-is quotes file:///C:/dir\..\x as C:/dir/../x: the dot
    // segments survive, the backslashes do not.
    [TestMethod]
    public void TryParse_PathAsIs_KeepsDotDotButStillConvertsBackslashes()
    {
        var url = CurlUrl.Parse(@"file:///tmp/dir\..\x", pathAsIs: true);

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/dir/../x", path.UrlPath);
        Assert.AreEqual(NativePath("/tmp/dir/../x"), path.OsPath);
    }

    [TestMethod]
    public void TryParse_PathAsIs_KeepsSingleDotSegments()
    {
        var url = CurlUrl.Parse("file:///tmp/dir/./x", pathAsIs: true);

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/dir/./x", path.UrlPath);
    }

    // curl 8.21.0 quotes every well-formed escape it keeps with uppercase hexadecimal
    // digits in its exit 37 message; the operating-system path is decoded either way.
    [TestMethod]
    [DataRow("file:///tmp/dir/a%2eb/x", "/tmp/dir/a%2Eb/x")]
    [DataRow("file:///tmp/dir/../a%20b%2fc", "/tmp/a%20b%2Fc")]
    [DataRow("file:///tmp/dir/a%5cb", "/tmp/dir/a%5Cb")]
    [DataRow("file:///tmp/dir/a%e9b", "/tmp/dir/a%E9b")]
    [DataRow("file:///tmp/dir/%7e", "/tmp/dir/%7E")]
    [DataRow("file:///tmp/dir/a%2Eb", "/tmp/dir/a%2Eb")]
    public void TryParse_LowercaseEscape_IsQuotedWithUppercaseHexDigits(string url, string expected)
    {
        bool parsed = FileUrlPath.TryParse(CurlUrl.Parse(url), out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual(expected, path.UrlPath);
    }

    [TestMethod]
    public void TryParse_PathAsIsLowercaseEncodedDots_AreQuotedWithUppercaseHexDigits()
    {
        var url = CurlUrl.Parse("file:///tmp/dir/%2e%2e/x", pathAsIs: true);

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/dir/%2E%2E/x", path.UrlPath);
    }

    // Measured against curl 8.21.0: only a percent sign followed by two hexadecimal
    // digits is uppercased. A malformed escape is quoted exactly as written, and in
    // "a%%2eb" the first percent is malformed while the "%2e" after it is not.
    [TestMethod]
    [DataRow("file:///tmp/dir/a%2/x", "/tmp/dir/a%2/x")]
    [DataRow("file:///tmp/dir/a%GG/x", "/tmp/dir/a%GG/x")]
    [DataRow("file:///tmp/dir/a%g2b", "/tmp/dir/a%g2b")]
    [DataRow("file:///tmp/dir/a%2gb", "/tmp/dir/a%2gb")]
    [DataRow("file:///tmp/dir/a%", "/tmp/dir/a%")]
    [DataRow("file:///tmp/dir/a%2", "/tmp/dir/a%2")]
    [DataRow("file:///tmp/dir/a%%2eb", "/tmp/dir/a%%2Eb")]
    public void TryParse_MalformedEscape_IsQuotedExactlyAsWritten(string url, string expected)
    {
        bool parsed = FileUrlPath.TryParse(CurlUrl.Parse(url), out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual(expected, path.UrlPath);
    }

    // Measured against the Unicode build of curl 8.21.0 (C:\Windows\System32\curl.exe),
    // which, like .NET, receives its arguments as UTF-16: a non-ASCII character written
    // unescaped is quoted as its UTF-8 bytes, each an uppercase escape, whether or not it
    // is in code page 1252. The ANSI mingw build quotes code page bytes instead and loses
    // characters outside the code page, an artefact of how that build reads its arguments.
    [TestMethod]
    [DataRow("file:///tmp/nodir/a\u00E9b", "/tmp/nodir/a%C3%A9b")]
    [DataRow("file:///tmp/nodir/a\u20ACb", "/tmp/nodir/a%E2%82%ACb")]
    [DataRow("file:///tmp/nodir/a\u03A9b", "/tmp/nodir/a%CE%A9b")]
    [DataRow("file:///tmp/nodir/a\u65E5b", "/tmp/nodir/a%E6%97%A5b")]
    [DataRow("file:///tmp/nodir/a\uD83D\uDE00b", "/tmp/nodir/a%F0%9F%98%80b")]
    public void TryParse_UnescapedNonAsciiCharacter_IsQuotedAsUppercaseUtf8Escapes(
        string url,
        string expected)
    {
        bool parsed = FileUrlPath.TryParse(CurlUrl.Parse(url), out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual(expected, path.UrlPath);
    }

    // Only the quoted form is encoded: the operating-system path keeps the character.
    [TestMethod]
    public void TryParse_UnescapedNonAsciiCharacter_IsKeptInTheOperatingSystemPath()
    {
        var url = CurlUrl.Parse("file:///tmp/nodir/a\u00E9b");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual(NativePath("/tmp/nodir/a\u00E9b"), path.OsPath);
    }

    // Printable ASCII is never re-encoded (curl 8.21.0 quotes file:///C:/dir/a"b as
    // C:/dir/a"b), and an escape written beside a non-ASCII character is only uppercased.
    [TestMethod]
    public void TryParse_NonAsciiBesideAsciiAndAnEscape_EncodesOnlyTheNonAsciiCharacter()
    {
        var url = CurlUrl.Parse("file:///tmp/dir/../a\"%e9\u00E9b");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("/tmp/a\"%E9%C3%A9b", path.UrlPath);
    }

    // file://C:/dir/hello.txt is not a host named "C:" — curl 8.21.0 reads the authority
    // as the head of the path, transfers the file and exits 0. It therefore has to parse
    // to exactly what the three-slash spelling parses to.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TryParse_DriveLetterAuthority_KeepsItAsTheHeadOfThePath()
    {
        var url = CurlUrl.Parse("file://C:/dir/hello.txt");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("C:/dir/hello.txt", path.UrlPath);
        Assert.AreEqual(NativePath("C:/dir/hello.txt"), path.OsPath);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TryParse_DriveLetterAuthority_MatchesTheEmptyAuthorityForm()
    {
        var twoSlashes = CurlUrl.Parse("file://C:/dir/hello.txt");
        var threeSlashes = CurlUrl.Parse("file:///C:/dir/hello.txt");

        bool parsedTwo = FileUrlPath.TryParse(twoSlashes, out var fromTwo);
        bool parsedThree = FileUrlPath.TryParse(threeSlashes, out var fromThree);

        Assert.IsTrue(parsedTwo);
        Assert.IsTrue(parsedThree);
        Assert.AreEqual(fromThree, fromTwo);
    }

    // FileUrlPath is a record so that two parses compare by value; a with expression is
    // part of that surface and copies both forms of the path unchanged.
    [TestMethod]
    public void With_NoChanges_CopiesAnEqualPath()
    {
        bool parsed = FileUrlPath.TryParse(CurlUrl.Parse("file:///tmp/dir/hello.txt"), out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        FileUrlPath copy = path with { };
        Assert.AreNotSame(path, copy);
        Assert.AreEqual(path, copy);
    }

    // The letter's case does not matter: file://d:/nope.txt was measured at exit 37, which
    // is a failed open of a path and not the exit 3 a rejected host would give.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TryParse_LowercaseDriveLetterAuthority_KeepsItAsTheHeadOfThePath()
    {
        var url = CurlUrl.Parse("file://d:/nope.txt");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("d:/nope.txt", path.UrlPath);
        Assert.AreEqual(NativePath("d:/nope.txt"), path.OsPath);
    }

    // file://D|/nope.txt was measured at exit 37 quoting the path D|/nope.txt, bar and
    // all. curl does not rewrite the bar to a colon, so the bar has to survive both
    // halves of the pair.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TryParse_BarDriveLetterAuthority_KeepsTheBarUnrewritten()
    {
        var url = CurlUrl.Parse("file://D|/nope.txt");

        bool parsed = FileUrlPath.TryParse(url, out var path);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(path);
        Assert.AreEqual("D|/nope.txt", path.UrlPath);
        Assert.AreEqual(NativePath("D|/nope.txt"), path.OsPath);
    }

    // The drive-letter exception is exactly two characters wide, a letter and then a colon
    // or a bar: everything here was measured at exit 3 instead, and CurlUrl rejects each
    // before a handler runs.
    [TestMethod]
    [DataRow("file://c/x")]
    [DataRow("file://zz/x")]
    [DataRow("file://1/x")]
    [DataRow("file://ab:/x")]
    [DataRow("file://example.com/x")]
    public void CurlUrlTryParse_AuthorityThatIsNeitherADriveNorAnAcceptedHost_ReturnsFalse(
        string candidate)
    {
        bool parsed = CurlUrl.TryParse(candidate, pathAsIs: false, out _);

        Assert.IsFalse(parsed);
    }

    private static string NativePath(string slashedPath) =>
        slashedPath.Replace('/', Path.DirectorySeparatorChar);
}
