using System.Text;

using Curl.Cli;
using Curl.Protocol.Abstractions;

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

    [TestMethod]
    [DataRow("Cookie: c=d", DisplayName = "Cookie: c=d")]
    [DataRow("cookie: c=d", DisplayName = "cookie: c=d")]
    [DataRow("Cookie:", DisplayName = "Cookie:")]
    [DataRow("COOKIE;", DisplayName = "COOKIE;")]
    public void HandlerStore_CookieStringWithHeaderNamingCookie_SendsNoCookieHeader(string header)
    {
        CookieEngine cookies = CookieEngine.FromCommandLine(Parse("-b", "a=b", "-H", header, Url))!;

        Assert.IsNull(cookies.HandlerStore.GetCookieHeader(CurlUrl.Parse(Url), false, Now));
    }

    [TestMethod]
    [DataRow(new string[0], DisplayName = "no -H")]
    [DataRow(new[] { "-H", "Cookies: c=d" }, DisplayName = "-H Cookies:")]
    [DataRow(new[] { "-H", "X-Cookie: c=d" }, DisplayName = "-H X-Cookie:")]
    public void HandlerStore_CookieStringWithoutHeaderNamingCookie_SendsTheString(string[] headerArguments)
    {
        CookieEngine cookies = CookieEngine.FromCommandLine(Parse(["-b", "a=b", .. headerArguments, Url]))!;

        Assert.AreEqual("a=b", cookies.HandlerStore.GetCookieHeader(CurlUrl.Parse(Url), false, Now));
    }

    [TestMethod]
    public async Task HandlerStore_CookieFileAndStringWithHeaderNamingCookie_SendsOnlyTheFilesCookies()
    {
        InMemoryFileSystem fileSystem = new();
        fileSystem.ExistingContent["jar.txt"] =
            Encoding.Latin1.GetBytes("# Netscape HTTP Cookie File\n127.0.0.1\tFALSE\t/\tFALSE\t0\tsess\ts1\n");
        CookieEngine cookies = CookieEngine.FromCommandLine(
            Parse("-b", "jar.txt", "-b", "a=b", "-H", "Cookie: c=d", Url))!;

        await cookies.LoadCookieFilesAsync(fileSystem, Now);

        Assert.AreEqual("sess=s1", cookies.HandlerStore.GetCookieHeader(CurlUrl.Parse(Url), false, Now));
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
