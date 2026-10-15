using System.Text;

using Curl.Api.Options.Http;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins which <c>-b</c> cookies <see cref="CookieEngine" /> puts in its store: curl 8.21.0 leaves
/// the <c>-b name=value</c> strings out when an <c>-H</c> value names <c>Cookie</c>, and still
/// sends the cookies of a <c>-b</c> file (BL-182 Notes).
/// </summary>
[TestClass]
public sealed class CookieEngineTests
{
    private const string Url = "http://127.0.0.1:18231/";

    private static readonly DateTimeOffset Now = new(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("Cookie: c=d", DisplayName = "Cookie: c=d")]
    [DataRow("cookie: c=d", DisplayName = "cookie: c=d")]
    [DataRow("Cookie:", DisplayName = "Cookie:")]
    [DataRow("COOKIE;", DisplayName = "COOKIE;")]
    public void HandlerStore_CookieStringWithHeaderNamingCookie_SendsNoCookieHeader(string header)
    {
        Diagnostics.Arrange("command line", $"-b a=b -H \"{header}\" {Url}");
        CookieEngine cookies = CookieEngine.FromHttpOptions(new EasyHttpOptions { Cookie = "a=b", HttpHeaders = [header] })!;
        string? cookieHeader = cookies.HandlerStore.GetCookieHeader(CurlUrl.Parse(Url), false, Now, NoTransferEvents.Instance);
        Diagnostics.Act("cookie header", cookieHeader ?? "<null>");

        Diagnostics.Assert("cookie header", "<null>", cookieHeader ?? "<null>");
        Assert.IsNull(cookies.HandlerStore.GetCookieHeader(CurlUrl.Parse(Url), false, Now, NoTransferEvents.Instance));
    }

    [TestMethod]
    [DataRow(new string[0], DisplayName = "no -H")]
    [DataRow(new[] { "Cookies: c=d" }, DisplayName = "-H Cookies:")]
    [DataRow(new[] { "X-Cookie: c=d" }, DisplayName = "-H X-Cookie:")]
    public void HandlerStore_CookieStringWithoutHeaderNamingCookie_SendsTheString(string[] headers)
    {
        Diagnostics.Arrange("command line", $"-b a=b {string.Join(" ", headers.Select(header => $"-H \"{header}\""))} {Url}");
        CookieEngine cookies = CookieEngine.FromHttpOptions(new EasyHttpOptions { Cookie = "a=b", HttpHeaders = headers })!;
        string? cookieHeader = cookies.HandlerStore.GetCookieHeader(CurlUrl.Parse(Url), false, Now, NoTransferEvents.Instance);
        Diagnostics.Act("cookie header", cookieHeader ?? "<null>");

        Diagnostics.Assert("cookie header", "a=b", cookieHeader ?? "<null>");
        Assert.AreEqual("a=b", cookies.HandlerStore.GetCookieHeader(CurlUrl.Parse(Url), false, Now, NoTransferEvents.Instance));
    }

    [TestMethod]
    public async Task HandlerStore_CookieFileAndStringWithHeaderNamingCookie_SendsOnlyTheFilesCookies()
    {
        ReadOnlyFileSystem fileSystem = new();
        fileSystem.ExistingContent["jar.txt"] =
            Encoding.Latin1.GetBytes("# Netscape HTTP Cookie File\n127.0.0.1\tFALSE\t/\tFALSE\t0\tsess\ts1\n");
        Diagnostics.Arrange("command line", $"-b jar.txt -b a=b -H \"Cookie: c=d\" {Url}");
        Diagnostics.Bytes("jar.txt", fileSystem.ExistingContent["jar.txt"]);
        CookieEngine cookies = CookieEngine.FromHttpOptions(new EasyHttpOptions { Cookie = "a=b", CookieFiles = ["jar.txt"], HttpHeaders = ["Cookie: c=d"] })!;

        await cookies.LoadCookieFilesAsync(fileSystem, Stream.Null, Now, NoTransferEvents.Instance);
        string? cookieHeader = cookies.HandlerStore.GetCookieHeader(CurlUrl.Parse(Url), false, Now, NoTransferEvents.Instance);
        Diagnostics.Act("cookie header", cookieHeader ?? "<null>");

        Diagnostics.Assert("cookie header", "sess=s1", cookieHeader ?? "<null>");
        Assert.AreEqual("sess=s1", cookies.HandlerStore.GetCookieHeader(CurlUrl.Parse(Url), false, Now, NoTransferEvents.Instance));
    }

    [TestMethod]
    public void GetStoredCookieHeader_CookieStringOnly_SendsNothing()
    {
        // curl -b test=yes -L to another host sends no Cookie (upstream test2015, BL-1846).
        Diagnostics.Arrange("command line", $"-b test=yes {Url}");
        CookieEngine cookies = CookieEngine.FromHttpOptions(new EasyHttpOptions { Cookie = "test=yes" })!;

        string? cookieHeader = cookies.HandlerStore.GetStoredCookieHeader(CurlUrl.Parse(Url), false, Now, NoTransferEvents.Instance);
        Diagnostics.Act("cookie header", cookieHeader ?? "<null>");

        Diagnostics.Assert("cookie header", "<null>", cookieHeader ?? "<null>");
        Assert.IsNull(cookieHeader);
    }

    [TestMethod]
    public async Task GetStoredCookieHeader_CookieFileAndString_SendsOnlyTheFilesCookies()
    {
        ReadOnlyFileSystem fileSystem = new();
        fileSystem.ExistingContent["jar.txt"] =
            Encoding.Latin1.GetBytes("# Netscape HTTP Cookie File\n127.0.0.1\tFALSE\t/\tFALSE\t0\tsess\ts1\n");
        Diagnostics.Arrange("command line", $"-b jar.txt -b test=yes {Url}");
        CookieEngine cookies = CookieEngine.FromHttpOptions(new EasyHttpOptions { Cookie = "test=yes", CookieFiles = ["jar.txt"] })!;

        await cookies.LoadCookieFilesAsync(fileSystem, Stream.Null, Now, NoTransferEvents.Instance);
        string? stored = cookies.HandlerStore.GetStoredCookieHeader(CurlUrl.Parse(Url), false, Now, NoTransferEvents.Instance);
        string? full = cookies.HandlerStore.GetCookieHeader(CurlUrl.Parse(Url), false, Now, NoTransferEvents.Instance);
        Diagnostics.Act("stored, full", $"{stored}, {full}");

        Diagnostics.Assert("stored, full", "sess=s1, sess=s1; test=yes", $"{stored}, {full}");
        Assert.AreEqual("sess=s1", stored);
        Assert.AreEqual("sess=s1; test=yes", full);
    }

    [TestMethod]
    public void FromHttpOptions_NoCookieOptions_GivesNoCookies()
    {
        Diagnostics.Arrange("http options", "no Cookie, CookieFiles or CookieJar");

        CookieEngine? cookies = CookieEngine.FromHttpOptions(new EasyHttpOptions { CookieSession = true });
        Diagnostics.Act("cookies", cookies is null ? "<null>" : "created");

        Diagnostics.Assert("cookies", "<null>", cookies is null ? "<null>" : "created");
        Assert.IsNull(cookies);
    }

    [TestMethod]
    public void FromHttpOptions_CookieJarOnly_TurnsTheCookieEngineOn()
    {
        Diagnostics.Arrange("http options", "CookieJar = jar.txt");
        CookieEngine cookies = CookieEngine.FromHttpOptions(new EasyHttpOptions { CookieJar = "jar.txt" })!;

        cookies.HandlerStore.StoreFromResponse(CurlUrl.Parse(Url), "a=b", 0, Now, NoTransferEvents.Instance);
        string? stored = cookies.HandlerStore.GetStoredCookieHeader(CurlUrl.Parse(Url), false, Now, NoTransferEvents.Instance);
        Diagnostics.Act("stored cookie header", stored ?? "<null>");

        Diagnostics.Assert("stored cookie header", "a=b", stored ?? "<null>");
        Assert.AreEqual("a=b", stored);
    }

    [TestMethod]
    [DataRow(CookieEngine.LongestCookieLine - 1, false, DisplayName = "one byte short")]
    [DataRow(CookieEngine.LongestCookieLine, true, DisplayName = "at the limit")]
    public void CookieLineTooLong_JoinedCookieLength_ComparesWithTheLongestCookieLine(int length, bool expected)
    {
        string cookie = "a=" + new string('x', length - 2);
        Diagnostics.Arrange("Cookie length", length);

        bool tooLong = CookieEngine.FromHttpOptions(new EasyHttpOptions { Cookie = cookie })!.CookieLineTooLong;
        Diagnostics.Act("too long", tooLong);

        Diagnostics.Assert("too long", expected, tooLong);
        Assert.AreEqual(expected, tooLong);
    }

    private sealed class ReadOnlyFileSystem : IFileSystem
    {
        public Dictionary<string, byte[]> ExistingContent { get; } = [];

        public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken) =>
            ValueTask.FromResult(FileOpenResult.Opened(new MemoryStream(ExistingContent[path], writable: false), ExistingContent[path].Length, null));

        public ValueTask<FileOpenResult> OpenForWriteAsync(string path, FileWriteMode mode, UnixFileMode createMode, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
