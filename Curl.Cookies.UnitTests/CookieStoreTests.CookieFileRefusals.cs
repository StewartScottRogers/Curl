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

        new CookieStore().LoadCookieFile(new StringReader("Set-Cookie: g=v; X=\u0001\n"), discardSessionCookies: false, Now, events);

        CollectionAssert.AreEqual(new[] { "invalid octets in value, cookie dropped" }, events.Info);
    }

    [TestMethod]
    public void LoadCookieFile_CleanFile_ReportsNothing()
    {
        RecordingTransferEvents events = new();

        new CookieStore().LoadCookieFile(
            new StringReader("Set-Cookie: f=v\n127.0.0.1\tFALSE\t/\tFALSE\t0\tn\tv\n"),
            discardSessionCookies: false,
            Now,
            events);

        Assert.IsEmpty(events.Info);
    }

    /// <summary>Measured: curl printed four lines, in file order, and sent <c>Cookie: s=v</c>.</summary>
    [TestMethod]
    public void LoadCookieFile_RefusingFile_ReportsEachRefusedHeaderLineInFileOrder()
    {
        RecordingTransferEvents events = new();
        CookieStore store = new();

        store.LoadCookieFile(new StringReader(RefusingCookieFile), discardSessionCookies: false, Now, events);

        CollectionAssert.AreEqual(
            new[]
            {
                "invalid octets in value, cookie dropped",
                "invalid octets in name, cookie dropped",
                "invalid cookie, dropped",
                "invalid octets in value, cookie dropped",
            },
            events.Info);
        Assert.AreEqual("s=v", store.GetCookieHeader(Loopback, secure: false, Now));
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
        CookieStore store = new();

        store.LoadCookieFile(new StringReader(line + "\n"), discardSessionCookies: false, Now, events);

        CollectionAssert.AreEqual(expected is null ? Array.Empty<string>() : new[] { expected }, events.Info);
        Assert.IsEmpty(store.Cookies);
    }

    [TestMethod]
    public async Task LoadCookieFileAsync_WithEvents_ReportsTheRefusedLines()
    {
        FakeFileSystem fileSystem = new() { ReadContent = Encoding.Latin1.GetBytes("Set-Cookie: c\nSet-Cookie: f=v\n") };
        RecordingTransferEvents events = new();
        CookieStore store = new();

        await store.LoadCookieFileAsync(fileSystem, "cf.txt", discardSessionCookies: false, Now, events, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "invalid cookie, dropped" }, events.Info);
        Assert.AreEqual("f=v", store.GetCookieHeader(Loopback, secure: false, Now));
    }

    [TestMethod]
    public async Task LoadCookieFile_NullEvents_Throw()
    {
        CookieStore store = new();

        Assert.ThrowsExactly<ArgumentNullException>(() => store.LoadCookieFile(new StringReader(string.Empty), false, Now, null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => store.LoadCookieFileAsync(new FakeFileSystem(), "p", false, Now, null!, CancellationToken.None));
    }
}
