using Curl.Engine;
using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Original.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// CGR-055 (BL-3584): curl 8.22.0 appends a <c>-G</c> / <c>--url-query</c> query by re-parsing the URL
/// without <c>--path-as-is</c>, so its dot segments are removed from the request line and
/// <c>%{url_effective}</c> even under <c>--path-as-is</c>. Measured on Windows with curl 8.22.0 (Schannel)
/// on 2026-10-26 against Record-CurlExchange.ps1 (BL-3584 Notes).
/// </summary>
[TestClass]
public sealed class Cgr055Tests
{
    private const string Response = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    private ScriptedConnector server = new([]);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string RequestText => Encoding.Latin1.GetString(server.Written);

    [TestMethod]
    [DataRow("-G", "-d", "a=b", "GET /y?a=b HTTP/1.1", "http://127.0.0.1:20601/y?a=b")]
    [DataRow("--url-query", "a=b", "--path-as-is", "GET /y?a=b HTTP/1.1", "http://127.0.0.1:20601/y?a=b")]
    [DataRow("-G", "-d", "", "GET /y HTTP/1.1", "http://127.0.0.1:20601/y")]
    public async Task RunAsync_QueryAppendedUnderPathAsIs_RemovesTheDotSegments(string first, string second, string third, string requestLine, string effectiveUrl)
    {
        int exitCode = await RunAsync("-sS", first, second, third, "--path-as-is", "-w", "%{url_effective}", "http://127.0.0.1:20601/x/../y");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Request(requestLine), RequestText);
        Assert.AreEqual(effectiveUrl, Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_QueryAppendedUnderPathAsIs_RemovesEncodedDotSegments()
    {
        int exitCode = await RunAsync("-sS", "--url-query", "a=b", "--path-as-is", "-w", "%{url_effective}", "http://127.0.0.1:20601/x/%2e%2e/y/./z");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Request("GET /y/z?a=b HTTP/1.1"), RequestText);
        Assert.AreEqual("http://127.0.0.1:20601/y/z?a=b", Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_PathAsIsWithoutQuery_KeepsTheDotSegments()
    {
        int exitCode = await RunAsync("-sS", "--path-as-is", "-w", "%{url_effective}", "http://127.0.0.1:20601/x/../y");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Request("GET /x/../y HTTP/1.1"), RequestText);
        Assert.AreEqual("http://127.0.0.1:20601/x/../y", Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    private static string Request(string requestLine) =>
        $"{requestLine}\r\nHost: 127.0.0.1:20601\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\n\r\n";

    private async Task<int> RunAsync(params string[] arguments)
    {
        server = new ScriptedConnector([Encoding.Latin1.GetBytes(Response)]);
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                outputFiles,
                outputFiles,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                writesProgressMeter: false)
            .RunAsync(arguments);
        Diagnostics.Act("request bytes", RequestText);
        return exitCode;
    }
}
