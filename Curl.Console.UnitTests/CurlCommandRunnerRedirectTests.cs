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
