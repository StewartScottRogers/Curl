using System.Text;

using Curl.Api.Options.Http;
using Curl.Cli;
using Curl.Console;
using Curl.Cookies;
using Curl.Core;
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
    public void OverSharedCookies_OptionsLeaveTheEngineOff_StoresInTheSharedCookies()
    {
        Diagnostics.Arrange("http options", "none (the engine is off by the handle's options)");
        CookieStore sharedCookies = new();
        CookieEngine cookies = CookieEngine.OverSharedCookies(new EasyHttpOptions(), sharedCookies);

        cookies.HandlerStore.StoreFromResponse(CurlUrl.Parse(Url), "a=b", 0, Now, NoTransferEvents.Instance);
        CookieEngine otherHandle = CookieEngine.OverSharedCookies(new EasyHttpOptions(), sharedCookies);
        string? stored = otherHandle.HandlerStore.GetStoredCookieHeader(CurlUrl.Parse(Url), false, Now, NoTransferEvents.Instance);
        Diagnostics.Act("cookie header another handle on the share reads", stored ?? "<null>");

        Diagnostics.Assert("cookie header", "a=b", stored ?? "<null>");
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

    [TestMethod]
    [DataRow(NetscapeCookieFile.LongestLine, "\n", true, DisplayName = "4998 characters, LF")]
    [DataRow(NetscapeCookieFile.LongestLine + 1, "\n", false, DisplayName = "4999 characters, LF")]
    [DataRow(NetscapeCookieFile.LongestLine, "\r\n", false, DisplayName = "4998 and a CR, CR LF")]
    [DataRow(NetscapeCookieFile.LongestLine + 1, "", false, DisplayName = "4999 characters, last line")]
    public async Task LoadCookieFilesAsync_FileWithALongLine_TellsWhetherItWasTooLong(int length, string lineEnd, bool expected)
    {
        // curl 8.22.0 fails the transfer with exit 100 past 4998 characters, a CR counted (BL-2916, BL-2971).
        ReadOnlyFileSystem fileSystem = new();
        fileSystem.ExistingContent["jar.txt"] = Encoding.Latin1.GetBytes(LongCookieLine(length) + lineEnd);
        Diagnostics.Arrange("first line length, line end", $"{length}, {lineEnd.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal)}");
        CookieEngine cookies = CookieEngine.FromHttpOptions(new EasyHttpOptions { CookieFiles = ["jar.txt"] })!;

        bool loaded = await cookies.LoadCookieFilesAsync(fileSystem, Stream.Null, Now, NoTransferEvents.Instance);
        Diagnostics.Act("loaded", loaded);

        Diagnostics.Assert("loaded", expected, loaded);
        Assert.AreEqual(expected, loaded);
    }

    [TestMethod]
    public async Task LoadCookieFilesAsync_StandardInputWithA4999CharacterLine_ReturnsFalseAndReadsNoLaterFile()
    {
        ReadOnlyFileSystem fileSystem = new();
        using MemoryStream standardInput = new(Encoding.Latin1.GetBytes(LongCookieLine(NetscapeCookieFile.LongestLine + 1) + "\n"));
        Diagnostics.Arrange("command line", $"-b - -b later.txt {Url}");
        CookieEngine cookies = CookieEngine.FromHttpOptions(new EasyHttpOptions { CookieFiles = ["-", "later.txt"] })!;

        bool loaded = await cookies.LoadCookieFilesAsync(fileSystem, standardInput, Now, NoTransferEvents.Instance);
        Diagnostics.Act("loaded", loaded);

        Diagnostics.Assert("loaded", false, loaded);
        Assert.IsFalse(loaded);
    }

    [TestMethod]
    public async Task LoadCookieFilesAsync_SecondCallAfterALineTooLong_LoadsNothingAndReturnsTrue()
    {
        ReadOnlyFileSystem fileSystem = new();
        fileSystem.ExistingContent["jar.txt"] = Encoding.Latin1.GetBytes(LongCookieLine(NetscapeCookieFile.LongestLine + 1) + "\n");
        Diagnostics.Arrange("command line", $"-b jar.txt {Url}");
        CookieEngine cookies = CookieEngine.FromHttpOptions(new EasyHttpOptions { CookieFiles = ["jar.txt"] })!;

        bool first = await cookies.LoadCookieFilesAsync(fileSystem, Stream.Null, Now, NoTransferEvents.Instance);
        bool second = await cookies.LoadCookieFilesAsync(fileSystem, Stream.Null, Now, NoTransferEvents.Instance);
        Diagnostics.Act("first, second", $"{first}, {second}");

        Diagnostics.Assert("first, second", "False, True", $"{first}, {second}");
        Assert.IsFalse(first);
        Assert.IsTrue(second);
    }

    [TestMethod]
    [DataRow(NetscapeCookieFile.LongestLine, null, DisplayName = "4998 characters goes ahead")]
    [DataRow(NetscapeCookieFile.LongestLine + 1, CurlExitCode.TooLarge, DisplayName = "4999 characters fails with 100")]
    public async Task TransferPerformerLoadCookieFilesAsync_FileWithALongLine_FailsTheTransferPast4998Characters(int length, CurlExitCode? expected)
    {
        ReadOnlyFileSystem fileSystem = new();
        fileSystem.ExistingContent["jar.txt"] = Encoding.Latin1.GetBytes(LongCookieLine(length) + "\n");
        Diagnostics.Arrange("first line length", length);
        CookieEngine cookies = CookieEngine.FromHttpOptions(new EasyHttpOptions { CookieFiles = ["jar.txt"] })!;
        EngineTransferSettings settings = EasyOptionsFromCommandLine.TransferSettingsOf(CommandLineParser.Parse(["-b", "jar.txt", Url], _ => true).Options!);

        TransferResult? failure = await new TransferPerformer(TimeProvider.System, code => $"text of {(int)code}")
            .LoadCookieFilesAsync(cookies, settings, Url, fileSystem, Stream.Null, NoTransferEvents.Instance);
        Diagnostics.Act("failure", failure is null ? "<null>" : $"{(int)failure.ExitCode} {failure.ErrorMessage}");

        Diagnostics.Assert("exit code", expected?.ToString() ?? "<null>", failure?.ExitCode.ToString() ?? "<null>");
        Assert.AreEqual(expected, failure?.ExitCode);
        Assert.AreEqual(expected is null ? null : "text of 100", failure?.ErrorMessage);
    }

    /// <summary>A Netscape cookie line <paramref name="length" /> characters long, before its line end.</summary>
    private static string LongCookieLine(int length)
    {
        const string Start = "127.0.0.1\tFALSE\t/\tFALSE\t0\tname\t";
        return Start + new string('v', length - Start.Length);
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
