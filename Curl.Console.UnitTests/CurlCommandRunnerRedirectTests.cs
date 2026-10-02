using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;

namespace Curl.Console;

/// <summary>
/// Pins <c>-L</c> and <c>--max-redirs</c> end to end through the runner, the redirect
/// follower and <see cref="HttpProtocolHandler" /> over a <see cref="ScriptedConnector" />.
/// Every expectation was measured on 2026-09-26 with curl 8.21.0 (mingw, Schannel) against a
/// loopback server on 127.0.0.1 answering <c>/a</c> with a <c>302 Found</c> to <c>/b</c> and
/// <c>/b</c> with <c>200 OK</c> and <c>hello</c> (BL-234 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerRedirectTests
{
    private const string Url = "http://127.0.0.1:18244/a";

    private const string FoundHead = "HTTP/1.1 302 Found\r\nLocation: /b\r\nContent-Length: 3\r\nConnection: close\r\n\r\n";

    private const string OkHead = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\n";

    private const string Found = FoundHead + "xyz";

    private const string Ok = OkHead + "hello";

    private const string Request = "Host: 127.0.0.1:18244\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    private ScriptedConnector server = new([]);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    private string RequestsText => Encoding.Latin1.GetString(server.Written);

    [TestMethod]
    public async Task RunAsync_LocationWithInclude_WritesEveryHeadAndTheLastBody()
    {
        int exitCode = await RunAsync([Found, Ok], "-L", "-i", "-s", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(FoundHead + Ok, StandardOutputText);
        Assert.AreEqual("GET /a HTTP/1.1\r\n" + Request + "GET /b HTTP/1.1\r\n" + Request, RequestsText);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_LocationWithIncludeToOutputFile_WritesEveryHeadAndTheLastBodyToTheFile()
    {
        int exitCode = await RunAsync([Found, Ok], "-L", "-i", "-s", "-o", "out.txt", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(FoundHead + Ok, Encoding.Latin1.GetString(outputFiles.Written["out.txt"].ToArray()));
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_WithoutLocation_WritesTheRedirectAndDoesNotFollowIt()
    {
        int exitCode = await RunAsync([Found, Ok], "-i", "-sS", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Found, StandardOutputText);
        Assert.AreEqual("GET /a HTTP/1.1\r\n" + Request, RequestsText);
    }

    [TestMethod]
    public async Task RunAsync_MaxRedirsZero_Exits47AfterTheFirstHead()
    {
        int exitCode = await RunAsync([Found, Ok], "-L", "--max-redirs", "0", "-i", "-sS", Url);

        Assert.AreEqual(47, exitCode);
        Assert.AreEqual(FoundHead, StandardOutputText);
        Assert.AreEqual("curl: (47) Maximum (0) redirects followed" + NewLine, StandardErrorText);
        Assert.AreEqual("GET /a HTTP/1.1\r\n" + Request, RequestsText);
    }

    [TestMethod]
    public async Task RunAsync_MaxRedirsOne_FollowsOnceThenExits47()
    {
        int exitCode = await RunAsync([Found, Found], "-L", "-sS", "--max-redirs", "1", Url);

        Assert.AreEqual(47, exitCode);
        Assert.AreEqual(0, standardOutput.Length);
        Assert.AreEqual("curl: (47) Maximum (1) redirects followed" + NewLine, StandardErrorText);
        Assert.AreEqual("GET /a HTTP/1.1\r\n" + Request + "GET /b HTTP/1.1\r\n" + Request, RequestsText);
    }

    /// <summary>
    /// Runs <paramref name="arguments" /> with <see cref="HttpProtocolHandler" /> over a
    /// <see cref="ScriptedConnector" /> serving <paramref name="responses" />, one per
    /// connection, and <see cref="outputFiles" /> as the <c>-o</c> file system.
    /// </summary>
    [TestMethod]
    [DataRow("=https")]
    [DataRow("-http")]
    public async Task RunAsync_ProtoExcludesTheUrlScheme_Exits1ProtocolDisabledWithoutARequest(string proto)
    {
        // Measured against curl 8.21.0 on 2026-09-28 (BL-523 Notes): curl -sS --proto =https
        // http://127.0.0.1:48523/ -> exit 1, "curl: (1) Protocol "http" is disabled", no request.
        int exitCode = await RunAsync([Ok], "-sS", "--proto", proto, Url);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual("curl: (1) Protocol \"http\" is disabled" + NewLine, StandardErrorText);
        Assert.AreEqual(string.Empty, RequestsText);
    }

    [TestMethod]
    [DataRow("file:///dir/x", "file")]
    [DataRow("dict://127.0.0.1:48523/x", "dict")]
    public async Task RunAsync_LocationToASchemeOutsideTheDefaultRedirectSet_Exits1ProtocolDisabledInRedirect(string target, string scheme)
    {
        // curl -sS -L, Location: file:///dir/x -> exit 1, "curl: (1) Protocol "file" is disabled (in redirect)" (BL-523 Notes).
        int exitCode = await RunAsync([RedirectTo(target)], "-L", "-sS", Url);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual($"curl: (1) Protocol \"{scheme}\" is disabled (in redirect)" + NewLine, StandardErrorText);
    }

    [TestMethod]
    [DataRow("file:///dir/x", "file", "--proto-redir", "=http,dict")]
    [DataRow("dict://127.0.0.1:48523/x", "dict", "--proto", "=http")]
    public async Task RunAsync_ProtoRedirOrProtoExcludesTheLocationScheme_Exits1ProtocolDisabledInRedirect(string target, string scheme, string option, string value)
    {
        // curl -sS -L --proto-redir =http,dict, Location: file:///dir/x, and curl -sS -L --proto =http
        // --proto-redir =http,dict, Location: dict://... -> exit 1, "Protocol "..." is disabled (in redirect)" (BL-523 Notes).
        int exitCode = await RunAsync([RedirectTo(target)], "-L", "-sS", "--proto-redir", "=http,dict", option, value, Url);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual($"curl: (1) Protocol \"{scheme}\" is disabled (in redirect)" + NewLine, StandardErrorText);
        Assert.AreEqual("GET /a HTTP/1.1\r\n" + Request, RequestsText);
    }

    [TestMethod]
    public async Task RunAsync_VerboseProtoExcludesTheUrlScheme_WritesTheVerboseLineBeforeTheErrorLine()
    {
        // Measured against curl 8.21.0 on 2026-10-01 (BL-805 Notes): curl -v --proto -http
        // http://127.0.0.1:48805/ -> exit 1, "* Protocol "http" is disabled" then the curl: (1) line.
        int exitCode = await RunAsync([Ok], "-v", "-sS", "--proto", "-http", Url);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(
            "* Protocol \"http\" is disabled\n" + "curl: (1) Protocol \"http\" is disabled" + NewLine,
            StandardErrorText);
        Assert.AreEqual(string.Empty, RequestsText);
    }

    [TestMethod]
    public async Task RunAsync_VerboseUnsupportedScheme_WritesTheVerboseLineBeforeTheErrorLine()
    {
        // curl -v --proto =http bogus://127.0.0.1:48806/ -> exit 1, "* Protocol "bogus" not supported"
        // then the curl: (1) line (BL-805 Notes).
        int exitCode = await RunAsync([Ok], "-v", "-sS", "--proto", "=http", "bogus://127.0.0.1:18244/");

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(
            "* Protocol \"bogus\" not supported\n" + "curl: (1) Protocol \"bogus\" not supported" + NewLine,
            StandardErrorText);
        Assert.AreEqual(string.Empty, RequestsText);
    }

    [TestMethod]
    public async Task RunAsync_VerboseLocationToADisabledScheme_WritesTheVerboseLineBeforeTheErrorLine()
    {
        // curl -v -L, Location: file:///dir/x -> "* Issue another request to this URL: 'file:///dir/x'",
        // "* Protocol "file" is disabled (in redirect)", then the curl: (1) line (BL-805 Notes).
        int exitCode = await RunAsync([RedirectTo("file:///dir/x")], "-v", "-L", "-sS", Url);

        Assert.AreEqual(1, exitCode);
        StringAssert.EndsWith(
            StandardErrorText,
            "* Issue another request to this URL: 'file:///dir/x'\n"
            + "* Protocol \"file\" is disabled (in redirect)\n"
            + "curl: (1) Protocol \"file\" is disabled (in redirect)" + NewLine);
    }

    private static string RedirectTo(string target) =>
        $"HTTP/1.1 302 Found\r\nLocation: {target}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";

    private Task<int> RunAsync(string[] responses, params string[] arguments)
    {
        server = new ScriptedConnector(responses.Select(Encoding.Latin1.GetBytes));
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));

        return new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                outputFiles,
                outputFiles,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false)
            .RunAsync(arguments);
    }
}
