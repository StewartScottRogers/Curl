using System.Text;
using Curl.Cookies.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Cookies;

/// <summary>
/// Pins the <c>-v</c> lines <see cref="CookieStore.LoadCookieFile(TextReader, bool, DateTimeOffset, ITransferEvents)"/>
/// reports for a <c>-b</c> file (BL-461). Measured on curl 8.21.0 (mingw) on 2026-09-27: <c>Record-CurlExchange.ps1</c>
/// ran <c>curl -s -v -b cf.txt -c - http://127.0.0.1:&lt;port&gt;/</c> with the file's lines ending in LF, and each
/// expected line is the text curl wrote to standard error after <c>* </c>, before its <c>Trying</c> line. BL-461's
/// Notes list every run.
/// </summary>
public sealed partial class CookieStoreTests
{
    /// <summary>
    /// The measured file: header lines refused for their octets or a bad first part, header lines <c>Secure</c> and
    /// <c>Domain</c> never refuse, a first part with no name, and tab-separated lines refused silently.
    /// </summary>
    private const string RefusingCookieFile =
        "Set-Cookie: a=v; X=\u0001\n"
        + "Set-Cookie: b\u0001x=v\n"
        + "Set-Cookie: c\n"
        + "Set-Cookie: s=v; Secure\n"
        + "Set-Cookie: d=v; Domain=example.com\n"
        + "Set-Cookie: =v\n"
        + "127.0.0.1\tFALSE\t/\tFALSE\t0\tn\u0001\tv\n"
        + "127.0.0.1\tFALSE\t/\tFALSE\t0\tok\tv\u0001\n"
        + "bad line\n"
        + "Set-Cookie: e=v; X=\u0001\n";

    [TestMethod]
    public void LoadCookieFile_SetCookieLineWithInvalidOctetsInValue_ReportsTheDrop()
    {
        RecordingTransferEvents events = new();
        Diagnostics.ArrangeText("cookie file", "Set-Cookie: g=v; X=\u0001\n");

        new CookieStore().LoadCookieFile(new StringReader("Set-Cookie: g=v; X=\u0001\n"), discardSessionCookies: false, Now, events);
        Diagnostics.Act("-v lines reported", CookieTestDiagnostics.Shown(events.Info));

        Diagnostics.AssertTexts("-v lines reported", new[] { "invalid octets in value, cookie dropped" }, events.Info);
        CollectionAssert.AreEqual(new[] { "invalid octets in value, cookie dropped" }, events.Info);
    }

    [TestMethod]
    public void LoadCookieFile_CleanFile_ReportsNothing()
    {
        RecordingTransferEvents events = new();
        Diagnostics.ArrangeText("cookie file", "Set-Cookie: f=v\n127.0.0.1\tFALSE\t/\tFALSE\t0\tn\tv\n");

        new CookieStore().LoadCookieFile(
            new StringReader("Set-Cookie: f=v\n127.0.0.1\tFALSE\t/\tFALSE\t0\tn\tv\n"),
            discardSessionCookies: false,
            Now,
            events);
        Diagnostics.Act("-v lines reported", CookieTestDiagnostics.Shown(events.Info));

        Diagnostics.Assert("-v line count", 0, events.Info.Count);
        Assert.IsEmpty(events.Info);
    }

    /// <summary>Measured: curl printed four lines, in file order, and sent <c>Cookie: s=v</c>.</summary>
    [TestMethod]
    public void LoadCookieFile_RefusingFile_ReportsEachRefusedHeaderLineInFileOrder()
    {
        RecordingTransferEvents events = new();
        Diagnostics.ArrangeText("cookie file", RefusingCookieFile);
        CookieStore store = new();

        store.LoadCookieFile(new StringReader(RefusingCookieFile), discardSessionCookies: false, Now, events);
        Diagnostics.ActCookies("stored cookies", store.Cookies);
        Diagnostics.Act("-v lines reported", CookieTestDiagnostics.Shown(events.Info));

        Diagnostics.AssertTexts("-v lines reported", ["invalid octets in value, cookie dropped", "invalid octets in name, cookie dropped", "invalid cookie, dropped", "invalid octets in value, cookie dropped"], events.Info);
        CollectionAssert.AreEqual(
            new[]
            {
                "invalid octets in value, cookie dropped",
                "invalid octets in name, cookie dropped",
                "invalid cookie, dropped",
                "invalid octets in value, cookie dropped",
            },
            events.Info);
        HeaderCheck check = CompareHeader("s=v", store, Loopback, secure: false, Now);
        Assert.AreEqual(check.Expected, check.Actual);
    }

    /// <summary>
    /// Measured, one file line each: those that print a line, and those curl drops silently because their first part
    /// has no name at all.
    /// </summary>
    [TestMethod]
    [DataRow("Set-Cookie: c;a=b", "invalid cookie, dropped", DisplayName = "c;a=b")]
    [DataRow("Set-Cookie: ; ;a=b", "invalid cookie, dropped", DisplayName = "; ;a=b")]
    [DataRow("Set-Cookie: ; =v", "invalid cookie, dropped", DisplayName = "; =v")]
    [DataRow("Set-Cookie: \u0001=v", "invalid octets in name, cookie dropped", DisplayName = "<01>=v")]
    [DataRow("Set-Cookie: =v; X=\u0001", null, DisplayName = "=v; X=<01>")]
    [DataRow("Set-Cookie: =", null, DisplayName = "=")]
    [DataRow("Set-Cookie:=v", null, DisplayName = "no blank")]
    [DataRow("Set-Cookie:  =v", null, DisplayName = "two blanks")]
    [DataRow("Set-Cookie: \t=v", null, DisplayName = "tab")]
    [DataRow("Set-Cookie: =v;", null, DisplayName = "=v;")]
    [DataRow("Set-Cookie: ;=x;a=b", null, DisplayName = ";=x;a=b")]
    [DataRow("Set-Cookie: ;;", null, DisplayName = ";;")]
    [DataRow("Set-Cookie:", null, DisplayName = "empty")]
    [DataRow("Set-Cookie:   ", null, DisplayName = "blanks only")]
    public void LoadCookieFile_RefusedSetCookieLine_ReportsWhatCurlPrinted(string line, string? expected)
    {
        RecordingTransferEvents events = new();
        Diagnostics.ArrangeText("cookie file line", line);
        CookieStore store = new();

        store.LoadCookieFile(new StringReader(line + "\n"), discardSessionCookies: false, Now, events);
        Diagnostics.Act("-v lines reported", CookieTestDiagnostics.Shown(events.Info));

        Diagnostics.AssertTexts("-v lines reported", expected is null ? Array.Empty<string>() : new[] { expected }, events.Info);
        CollectionAssert.AreEqual(expected is null ? Array.Empty<string>() : new[] { expected }, events.Info);
        Diagnostics.Assert("stored cookie count", 0, store.Cookies.Count);
        Assert.IsEmpty(store.Cookies);
    }

    [TestMethod]
    public async Task LoadCookieFileAsync_WithEvents_ReportsTheRefusedLines()
    {
        FakeFileSystem fileSystem = new() { ReadContent = Encoding.Latin1.GetBytes("Set-Cookie: c\nSet-Cookie: f=v\n") };
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("cookie file path", "cf.txt");
        Diagnostics.Bytes("cookie file cf.txt", fileSystem.ReadContent);
        CookieStore store = new();

        await store.LoadCookieFileAsync(fileSystem, "cf.txt", discardSessionCookies: false, Now, events, CancellationToken.None);
        Diagnostics.Act("-v lines reported", CookieTestDiagnostics.Shown(events.Info));

        Diagnostics.AssertTexts("-v lines reported", new[] { "invalid cookie, dropped" }, events.Info);
        CollectionAssert.AreEqual(new[] { "invalid cookie, dropped" }, events.Info);
        Assert.AreEqual("f=v", store.GetCookieHeader(Loopback, secure: false, Now));
    }

    /// <summary>
    /// Measured on curl 8.21.0 (mingw, Schannel) on 2026-09-27 (BL-487 Notes): <c>curl -s -v -b &lt;path&gt;</c>
    /// printed this line first on standard error, with the path exactly as given - absolute, relative, with either
    /// slash, or naming a directory - and exited 0.
    /// </summary>
    [TestMethod]
    [DataRow("sub\\missing.txt", DisplayName = "Relative, backslash")]
    [DataRow("sub/missing.txt", DisplayName = "Relative, slash")]
    public async Task LoadCookieFileAsync_CannotOpen_ReportsTheWarning(string path)
    {
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("cookie file that cannot be opened", path);
        CookieStore store = new();

        await store.LoadCookieFileAsync(new FakeFileSystem(), path, discardSessionCookies: false, Now, events, CancellationToken.None);
        Diagnostics.Act("-v lines reported", CookieTestDiagnostics.Shown(events.Info));

        Diagnostics.AssertTexts("-v lines reported", new[] { $"WARNING: failed to open cookie file \"{path}\"" }, events.Info);
        CollectionAssert.AreEqual(new[] { $"WARNING: failed to open cookie file \"{path}\"" }, events.Info);
        Diagnostics.Assert("stored cookie count", 0, store.Cookies.Count);
        Assert.IsEmpty(store.Cookies);
    }

    /// <summary>
    /// Measured on curl 8.21.0 (mingw, Schannel) on 2026-10-03 (BL-1393): <c>curl -sv -b &lt;existing directory&gt;</c>
    /// wrote this line and carried on, exit 0, because Windows' <c>fopen</c> fails for a directory.
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task LoadCookieFileAsync_Directory_OnWindows_ReportsFailedToOpen()
    {
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("cookie file", "/dir/cookies, a directory, read on Windows");
        CookieStore store = new();

        await store.LoadCookieFileAsync(
            new FakeFileSystem { ReadFailure = FileAccessStatus.IsDirectory }, "/dir/cookies", discardSessionCookies: false, Now, events, CancellationToken.None);
        Diagnostics.Act("-v lines reported", CookieTestDiagnostics.Shown(events.Info));

        Diagnostics.AssertTexts("-v lines reported", new[] { "WARNING: failed to open cookie file \"/dir/cookies\"" }, events.Info);
        CollectionAssert.AreEqual(new[] { "WARNING: failed to open cookie file \"/dir/cookies\"" }, events.Info);
        Diagnostics.Assert("stored cookie count", 0, store.Cookies.Count);
        Assert.IsEmpty(store.Cookies);
    }

    /// <summary>
    /// curl 8.21.0's <c>lib/cookie.c</c> (lines 1146-1156): off Windows <c>fopen</c> opens a directory, and the
    /// <c>S_ISDIR</c> check after it writes this line and loads nothing (BL-1393).
    /// </summary>
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task LoadCookieFileAsync_Directory_OffWindows_ReportsPointsToADirectory()
    {
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("cookie file", "/dir/cookies, a directory, read off Windows");
        CookieStore store = new();

        await store.LoadCookieFileAsync(
            new FakeFileSystem { ReadFailure = FileAccessStatus.IsDirectory }, "/dir/cookies", discardSessionCookies: false, Now, events, CancellationToken.None);
        Diagnostics.Act("-v lines reported", CookieTestDiagnostics.Shown(events.Info));

        Diagnostics.AssertTexts("-v lines reported", new[] { "WARNING: cookie filename points to a directory: \"/dir/cookies\"" }, events.Info);
        CollectionAssert.AreEqual(new[] { "WARNING: cookie filename points to a directory: \"/dir/cookies\"" }, events.Info);
        Diagnostics.Assert("stored cookie count", 0, store.Cookies.Count);
        Assert.IsEmpty(store.Cookies);
    }

    [TestMethod]
    [DataRow(FileAccessStatus.IsDirectory, false, "WARNING: cookie filename points to a directory: \"/dir/cookies\"", DisplayName = "Directory, off Windows")]
    [DataRow(FileAccessStatus.IsDirectory, true, "WARNING: failed to open cookie file \"/dir/cookies\"", DisplayName = "Directory, Windows")]
    [DataRow(FileAccessStatus.NotFound, false, "WARNING: failed to open cookie file \"/dir/cookies\"", DisplayName = "Not found, off Windows")]
    [DataRow(FileAccessStatus.NotFound, true, "WARNING: failed to open cookie file \"/dir/cookies\"", DisplayName = "Not found, Windows")]
    [DataRow(FileAccessStatus.AccessDenied, false, "WARNING: failed to open cookie file \"/dir/cookies\"", DisplayName = "Access denied, off Windows")]
    [DataRow(FileAccessStatus.AccessDenied, true, "WARNING: failed to open cookie file \"/dir/cookies\"", DisplayName = "Access denied, Windows")]
    public void DescribeCookieFileOpenFailure_GivesEachPlatformsLine(FileAccessStatus status, bool isWindows, string expected)
    {
        Diagnostics.Arrange("status", status);
        Diagnostics.Arrange("isWindows", isWindows);

        string line = CookieStore.DescribeCookieFileOpenFailure("/dir/cookies", status, isWindows);
        Diagnostics.ActText("line", line);

        Diagnostics.AssertText("line", expected, line);
        Assert.AreEqual(expected, line);
    }

    [TestMethod]
    public void DescribeCookieFileOpenFailure_NullPath_Throws()
    {
        ArrangeNullArgument("path");

        ArgumentNullException thrown = Assert.ThrowsExactly<ArgumentNullException>(() => CookieStore.DescribeCookieFileOpenFailure(null!, FileAccessStatus.NotFound, isWindows: true));

        LogThrown(thrown);
    }

    [TestMethod]
    public async Task LoadCookieFile_NullEvents_Throw()
    {
        CookieStore store = new();
        ArrangeNullArgument("events, of LoadCookieFile and then LoadCookieFileAsync");

        ArgumentNullException thrown = Assert.ThrowsExactly<ArgumentNullException>(() => store.LoadCookieFile(new StringReader(string.Empty), false, Now, null!));
        LogThrown(thrown);
        thrown = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => store.LoadCookieFileAsync(new FakeFileSystem(), "p", false, Now, null!, CancellationToken.None));
        LogThrown(thrown);
    }
}
