using System.Text;

using Curl.Cli;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

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
        CookieEngine cookies = CookieEngine.FromCommandLine(Parse("-b", "a=b", "-H", header, Url))!;
        string? cookieHeader = cookies.HandlerStore.GetCookieHeader(CurlUrl.Parse(Url), false, Now, NoTransferEvents.Instance);
        Diagnostics.Act("cookie header", cookieHeader ?? "<null>");

        Diagnostics.Assert("cookie header", "<null>", cookieHeader ?? "<null>");
        Assert.IsNull(cookies.HandlerStore.GetCookieHeader(CurlUrl.Parse(Url), false, Now, NoTransferEvents.Instance));
    }

    [TestMethod]
    [DataRow(new string[0], DisplayName = "no -H")]
    [DataRow(new[] { "-H", "Cookies: c=d" }, DisplayName = "-H Cookies:")]
    [DataRow(new[] { "-H", "X-Cookie: c=d" }, DisplayName = "-H X-Cookie:")]
    public void HandlerStore_CookieStringWithoutHeaderNamingCookie_SendsTheString(string[] headerArguments)
    {
        Diagnostics.Arrange("command line", $"-b a=b {string.Join(" ", headerArguments)} {Url}");
        CookieEngine cookies = CookieEngine.FromCommandLine(Parse(["-b", "a=b", .. headerArguments, Url]))!;
        string? cookieHeader = cookies.HandlerStore.GetCookieHeader(CurlUrl.Parse(Url), false, Now, NoTransferEvents.Instance);
        Diagnostics.Act("cookie header", cookieHeader ?? "<null>");

        Diagnostics.Assert("cookie header", "a=b", cookieHeader ?? "<null>");
        Assert.AreEqual("a=b", cookies.HandlerStore.GetCookieHeader(CurlUrl.Parse(Url), false, Now, NoTransferEvents.Instance));
    }

    [TestMethod]
    public async Task HandlerStore_CookieFileAndStringWithHeaderNamingCookie_SendsOnlyTheFilesCookies()
    {
        InMemoryFileSystem fileSystem = new();
        fileSystem.ExistingContent["jar.txt"] =
            Encoding.Latin1.GetBytes("# Netscape HTTP Cookie File\n127.0.0.1\tFALSE\t/\tFALSE\t0\tsess\ts1\n");
        Diagnostics.Arrange("command line", $"-b jar.txt -b a=b -H \"Cookie: c=d\" {Url}");
        Diagnostics.Bytes("jar.txt", fileSystem.ExistingContent["jar.txt"]);
        CookieEngine cookies = CookieEngine.FromCommandLine(
            Parse("-b", "jar.txt", "-b", "a=b", "-H", "Cookie: c=d", Url))!;

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
        CookieEngine cookies = CookieEngine.FromCommandLine(Parse("-b", "test=yes", Url))!;

        string? cookieHeader = cookies.HandlerStore.GetStoredCookieHeader(CurlUrl.Parse(Url), false, Now, NoTransferEvents.Instance);
        Diagnostics.Act("cookie header", cookieHeader ?? "<null>");

        Diagnostics.Assert("cookie header", "<null>", cookieHeader ?? "<null>");
        Assert.IsNull(cookieHeader);
    }

    [TestMethod]
    public async Task GetStoredCookieHeader_CookieFileAndString_SendsOnlyTheFilesCookies()
    {
        InMemoryFileSystem fileSystem = new();
        fileSystem.ExistingContent["jar.txt"] =
            Encoding.Latin1.GetBytes("# Netscape HTTP Cookie File\n127.0.0.1\tFALSE\t/\tFALSE\t0\tsess\ts1\n");
        Diagnostics.Arrange("command line", $"-b jar.txt -b test=yes {Url}");
        CookieEngine cookies = CookieEngine.FromCommandLine(Parse("-b", "jar.txt", "-b", "test=yes", Url))!;

        await cookies.LoadCookieFilesAsync(fileSystem, Stream.Null, Now, NoTransferEvents.Instance);
        string? stored = cookies.HandlerStore.GetStoredCookieHeader(CurlUrl.Parse(Url), false, Now, NoTransferEvents.Instance);
        string? full = cookies.HandlerStore.GetCookieHeader(CurlUrl.Parse(Url), false, Now, NoTransferEvents.Instance);
        Diagnostics.Act("stored, full", $"{stored}, {full}");

        Diagnostics.Assert("stored, full", "sess=s1, sess=s1; test=yes", $"{stored}, {full}");
        Assert.AreEqual("sess=s1", stored);
        Assert.AreEqual("sess=s1; test=yes", full);
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
