using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;

namespace Curl.Console;

/// <summary>
/// Pins <c>--disallow-username-in-url</c> end to end through the runner, the redirect follower and
/// <see cref="HttpProtocolHandler" /> over a <see cref="ScriptedConnector" />. Every expectation was
/// measured on 2026-09-29 with curl 8.21.0 (mingw, Schannel) through <c>Record-CurlExchange.ps1</c>
/// against 127.0.0.1:18626 (BL-626 Notes): a URL with user information is refused with exit 67 and
/// <c>URL rejected: Credentials was passed in the URL when prohibited</c> before any connection, and a
/// redirect to one after the first hop.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerDisallowUsernameInUrlTests
{
    private const string Url = "http://127.0.0.1:18626/";

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    private const string Refused = "curl: (67) URL rejected: Credentials was passed in the URL when prohibited";

    private const string WriteOut = "[%{num_redirects}|%{url_effective}|%{redirect_url}|%{http_code}|%{num_connects}]";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    private ScriptedConnector server = new([]);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    [TestMethod]
    [DataRow("http://u@127.0.0.1:18626/", DisplayName = "u@")]
    [DataRow("http://u:p@127.0.0.1:18626/", DisplayName = "u:p@")]
    [DataRow("http://:p@127.0.0.1:18626/", DisplayName = ":p@")]
    [DataRow("http://@127.0.0.1:18626/", DisplayName = "@")]
    public async Task RunAsync_UrlWithUserInformation_Exits67WithoutConnecting(string url)
    {
        int exitCode = await RunAsync([Ok], "-sS", "--disallow-username-in-url", url);

        Assert.AreEqual(67, exitCode);
        Assert.AreEqual(Refused + NewLine, StandardErrorText);
        Assert.IsEmpty(server.Targets);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_UrlWithUserInformationAndSilent_Exits67Quietly()
    {
        int exitCode = await RunAsync([Ok], "-s", "--disallow-username-in-url", "http://u@127.0.0.1:18626/");

        Assert.AreEqual(67, exitCode);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_UrlWithUserInformationAndWriteOut_WritesNothingTransferred()
    {
        // curl -sS --disallow-username-in-url -w ... http://u@127.0.0.1:18626/ -> [0|http://u@127.0.0.1:18626/||000|0].
        int exitCode = await RunAsync([Ok], "-sS", "--disallow-username-in-url", "-w", WriteOut, "http://u@127.0.0.1:18626/");

        Assert.AreEqual(67, exitCode);
        Assert.AreEqual("[0|http://u@127.0.0.1:18626/||000|0]", StandardOutputText);
    }

    [TestMethod]
    [DataRow("-u", "x:y", "http://127.0.0.1:18626/", DisplayName = "-u without user information in the URL")]
    [DataRow("--no-disallow-username-in-url", null, "http://u@127.0.0.1:18626/", DisplayName = "--no-disallow-username-in-url last")]
    public async Task RunAsync_NoUserInformationOrOptionTurnedOff_Transfers(string option, string? value, string url)
    {
        string[] arguments = value is null
            ? ["-sS", "--disallow-username-in-url", option, url]
            : ["-sS", "--disallow-username-in-url", option, value, url];

        int exitCode = await RunAsync([Ok], arguments);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, standardError.Length);
        Assert.HasCount(1, server.Targets);
    }

    [TestMethod]
    public async Task RunAsync_LocationWithUserInformation_Exits67AfterTheFirstHop()
    {
        // curl -sS -L --disallow-username-in-url -w ..., Location: http://u:p@127.0.0.1:18626/x
        // -> exit 67, the refusal, [1|http://u:p@127.0.0.1:18626/x||302|1], one request sent.
        const string Found = "HTTP/1.1 302 Found\r\nLocation: http://u:p@127.0.0.1:18626/x\r\nContent-Length: 0\r\n\r\n";

        int exitCode = await RunAsync([Found, Ok], "-sS", "-L", "--disallow-username-in-url", "-w", WriteOut, Url);

        Assert.AreEqual(67, exitCode);
        Assert.AreEqual(Refused + NewLine, StandardErrorText);
        Assert.AreEqual("[1|http://u:p@127.0.0.1:18626/x||302|1]", StandardOutputText);
        Assert.AreEqual(
            "GET / HTTP/1.1\r\nHost: 127.0.0.1:18626\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n",
            Encoding.Latin1.GetString(server.Written));
    }

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
