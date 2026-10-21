using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Cookies;

/// <summary>
/// Pins what <see cref="CookieStore"/>'s cookie-file loaders return for a line longer than
/// <see cref="NetscapeCookieFile.LongestLine"/>: measured on curl 8.22.0 (mingw, Schannel) on 2026-10-21 (BL-2916),
/// such a line is <c>CURLE_TOO_LARGE</c>, which ends the transfer with exit 100, so the loaders say so.
/// </summary>
public sealed partial class CookieStoreTests
{
    private static string CookieFileWithLineOf(int length)
    {
        string prefix = "127.0.0.1\tFALSE\t/\tFALSE\t0\tlong\t";
        return "127.0.0.1\tFALSE\t/\tFALSE\t0\tbefore\t1\n" + prefix + new string('v', length - prefix.Length) + "\n";
    }

    [TestMethod]
    [DataRow(NetscapeCookieFile.LongestLine, true, "before=1; long=")]
    [DataRow(NetscapeCookieFile.LongestLine + 1, false, "before=1")]
    public void LoadCookieFile_LineAroundTheLimit_ReturnsWhetherEveryLineFit(int length, bool expectedFit, string expectedHeaderStart)
    {
        Diagnostics.Arrange("long line length", length);
        CookieStore store = new();

        bool fit = store.LoadCookieFile(new StringReader(CookieFileWithLineOf(length)), discardSessionCookies: false, Now);
        string header = store.GetCookieHeader(Loopback, secure: false, Now) ?? string.Empty;

        Diagnostics.Act("every line fit", fit);
        Diagnostics.Assert("every line fit", expectedFit, fit);
        Assert.AreEqual(expectedFit, fit);
        Assert.StartsWith(expectedHeaderStart, header);
    }

    [TestMethod]
    [DataRow(NetscapeCookieFile.LongestLine, true)]
    [DataRow(NetscapeCookieFile.LongestLine + 1, false)]
    public async Task LoadCookieFileAsync_LineAroundTheLimit_ReturnsWhetherEveryLineFit(int length, bool expectedFit)
    {
        FakeFileSystem fileSystem = new() { ReadContent = Encoding.Latin1.GetBytes(CookieFileWithLineOf(length)) };
        Diagnostics.Arrange("long line length", length);
        CookieStore store = new();

        bool fit = await store.LoadCookieFileAsync(fileSystem, "cf.txt", discardSessionCookies: false, Now, CancellationToken.None);

        Diagnostics.Act("every line fit", fit);
        Diagnostics.Assert("every line fit", expectedFit, fit);
        Assert.AreEqual(expectedFit, fit);
    }

    [TestMethod]
    public async Task LoadCookieFileAsync_FileCannotBeOpened_ReturnsTrue()
    {
        Diagnostics.Arrange("cookie file path", "none.txt");
        CookieStore store = new();

        bool fit = await store.LoadCookieFileAsync(new FakeFileSystem(), "none.txt", discardSessionCookies: false, Now, NoTransferEvents.Instance, CancellationToken.None);

        Diagnostics.Act("every line fit", fit);
        Diagnostics.Assert("every line fit", true, fit);
        Assert.IsTrue(fit);
    }
}
