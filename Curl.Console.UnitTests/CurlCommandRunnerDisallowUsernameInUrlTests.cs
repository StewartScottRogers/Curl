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

    // curl 8.21.0 checks CURLU_DISALLOW_USER while parsing the login, so a bad host or port, or an
    // unsupported scheme, after it is never reached (measured 2026-10-01, BL-910 Notes).
    [TestMethod]
    [DataRow("http://u@127.0.0.1:99999/", DisplayName = "user and bad port")]
    [DataRow("http://u:p@127.0.0.1:abc/", DisplayName = "user, password and bad port")]
    [DataRow("http://u@exa%20mple.com/", DisplayName = "user and bad host")]
    [DataRow("http://u@[::1]x/", DisplayName = "user and text after an IPv6 host")]
    [DataRow("http://u@:80/", DisplayName = "user and no host")]
    [DataRow("foo://u@127.0.0.1/", DisplayName = "user and unsupported scheme")]
    public async Task RunAsync_UrlWithUserInformationAndALaterBadPart_Exits67(string url)
    {
        int exitCode = await RunAsync([Ok], "-sS", "--disallow-username-in-url", url);

        Assert.AreEqual(67, exitCode);
        Assert.AreEqual(Refused + NewLine, StandardErrorText);
        Assert.IsEmpty(server.Targets);
    }

    [TestMethod]
    [DataRow("http://u@127.0.0.1:99999/", 3, "curl: (3) URL rejected: Port number was not a decimal number between 0 and 65535", DisplayName = "user and bad port")]
    [DataRow("http://u:p@127.0.0.1:abc/", 3, "curl: (3) URL rejected: Port number was not a decimal number between 0 and 65535", DisplayName = "user, password and bad port")]
    [DataRow("http://u@exa%20mple.com/", 3, "curl: (3) URL rejected: Bad hostname", DisplayName = "user and bad host")]
    [DataRow("http://u@[::1]x/", 3, "curl: (3) URL rejected: Port number was not a decimal number between 0 and 65535", DisplayName = "user and text after an IPv6 host")]
    [DataRow("http://u@:80/", 3, "curl: (3) URL rejected: No host part in the URL", DisplayName = "user and no host")]
    [DataRow("foo://u@127.0.0.1/", 1, "curl: (1) Protocol \"foo\" not supported", DisplayName = "user and unsupported scheme")]
    public async Task RunAsync_UrlWithUserInformationAndALaterBadPartWithoutTheOption_KeepsItsOwnFailure(
        string url,
        int expectedExitCode,
        string expectedError)
    {
        int exitCode = await RunAsync([Ok], "-sS", url);

        Assert.AreEqual(expectedExitCode, exitCode);
        Assert.AreEqual(expectedError + NewLine, StandardErrorText);
    }

    [TestMethod]
    [DataRow("http://u@ex ample/", "curl: (3) URL rejected: Malformed input to a URL function", DisplayName = "space in the URL")]
    [DataRow("http:////u@127.0.0.1/", "curl: (3) URL rejected: Unsupported number of slashes following scheme", DisplayName = "four slashes")]
    [DataRow("http://u@[::1/", "curl: (3) bad range specification in position 11:", DisplayName = "unclosed bracket, a glob error")]
    public async Task RunAsync_UrlRejectedBeforeItsLoginIsParsed_KeepsExit3(string url, string expectedFirstLine)
    {
        int exitCode = await RunAsync([Ok], "-sS", "--disallow-username-in-url", url);

        Assert.AreEqual(3, exitCode);
        Assert.StartsWith(expectedFirstLine + NewLine, StandardErrorText);
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
