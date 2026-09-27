using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Http;

namespace Curl.Console;

/// <summary>
/// Pins <c>--request-target</c> and <c>--path-as-is</c> end to end through the runner and
/// <see cref="HttpProtocolHandler" /> over a <see cref="ScriptedConnector" />, with the request
/// lines curl 8.21.0 (mingw, Schannel) sent a loopback recorder on 2026-09-26 (BL-186 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerRequestTargetTests
{
    private const string Response = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    private ScriptedConnector server = new([]);

    private string RequestText => Encoding.Latin1.GetString(server.Written);

    /// <summary>Measured: <c>curl -X OPTIONS --request-target '*' http://127.0.0.1:18186/a/b?q=1</c>.</summary>
    [TestMethod]
    public async Task RunAsync_RequestTargetStarWithOptions_SendsTheMeasuredRequestLine()
    {
        int exitCode = await RunAsync("-sS", "-X", "OPTIONS", "--request-target", "*", "http://127.0.0.1:18186/a/b?q=1");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Request("OPTIONS * HTTP/1.1", "127.0.0.1:18186"), RequestText);
    }

    /// <summary>Measured: <c>curl --request-target /x/../y?z http://127.0.0.1:18189/a</c>.</summary>
    [TestMethod]
    public async Task RunAsync_RequestTargetWithDotSegments_SendsItVerbatim()
    {
        int exitCode = await RunAsync("-sS", "--request-target", "/x/../y?z", "http://127.0.0.1:18189/a");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Request("GET /x/../y?z HTTP/1.1", "127.0.0.1:18189"), RequestText);
    }

    /// <summary>Measured: <c>curl --path-as-is http://127.0.0.1:18187/a/../b</c>.</summary>
    [TestMethod]
    public async Task RunAsync_PathAsIs_SendsTheDotSegmentsUnsquashed()
    {
        int exitCode = await RunAsync("-sS", "--path-as-is", "http://127.0.0.1:18187/a/../b");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Request("GET /a/../b HTTP/1.1", "127.0.0.1:18187"), RequestText);
    }

    /// <summary>Measured: <c>curl http://127.0.0.1:18188/a/../b</c>.</summary>
    [TestMethod]
    public async Task RunAsync_WithoutPathAsIs_SendsTheDotSegmentsSquashed()
    {
        int exitCode = await RunAsync("-sS", "http://127.0.0.1:18188/a/../b");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Request("GET /b HTTP/1.1", "127.0.0.1:18188"), RequestText);
    }

    private static string Request(string requestLine, string host) =>
        $"{requestLine}\r\nHost: {host}\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";

    /// <summary>
    /// Runs <paramref name="arguments" /> with <see cref="HttpProtocolHandler" /> over a
    /// <see cref="ScriptedConnector" /> answering an empty 200 and then closing.
    /// </summary>
    private Task<int> RunAsync(params string[] arguments)
    {
        server = new ScriptedConnector([Encoding.Latin1.GetBytes(Response)]);
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));

        return new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                outputFiles,
                outputFiles,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                writesProgressMeter: false)
            .RunAsync(arguments);
    }
}
