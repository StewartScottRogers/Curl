using System.Net;
using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Engine;
using Curl.Original.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the <c>-w</c> values after the HTTP handler refuses a response's head: the response it reached still
/// reports its status, header size, version and peer. Measured on 2026-10-22 with curl 8.22.0 (mingw, Schannel)
/// through <c>Record-CurlExchange.ps1</c> on 127.0.0.1:47131 (BL-3195 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerWriteOutAfterProtocolFailureTests
{
    private const string Url = "http://127.0.0.1:47131/";

    private const string Variables = "%{http_code} %{size_header} %{http_version} %{remote_ip} %{remote_port}";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem files = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    /// <summary>curl 8.22.0: exit 8, <c>200 36 1.1 127.0.0.1 47131</c> and <c>curl: (8) Header without colon</c>.</summary>
    [TestMethod]
    public async Task RunAsync_HeaderLineWithoutColon_WritesOutTheResponseReached()
    {
        await AssertWritesOutAsync(
            "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nBad header line\r\n\r\nhello",
            8,
            "200 36 1.1 127.0.0.1 47131",
            "curl: (8) Header without colon");
    }

    /// <summary>curl 8.22.0: exit 100, <c>200 36 1.1 127.0.0.1 47131</c> and the <c>grew larger than allowed</c> line.</summary>
    [TestMethod]
    public async Task RunAsync_HeaderLineOf400Kilobytes_WritesOutTheResponseReached()
    {
        await AssertWritesOutAsync(
            "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nX-Big: " + new string('a', 400_000) + "\r\n\r\nhello",
            100,
            "200 36 1.1 127.0.0.1 47131",
            "curl: (100) A value or data field grew larger than allowed");
    }

    /// <summary>curl 8.22.0: exit 8, <c>302 34 1.1 127.0.0.1 47131</c> and <c>curl: (8) Multiple Location headers</c>.</summary>
    [TestMethod]
    public async Task RunAsync_TwoLocationHeaders_WritesOutTheResponseReached()
    {
        await AssertWritesOutAsync(
            "HTTP/1.1 302 Found\r\nLocation: /a\r\nLocation: /b\r\nContent-Length: 0\r\n\r\n",
            8,
            "302 34 1.1 127.0.0.1 47131",
            "curl: (8) Multiple Location headers");
    }

    private async Task AssertWritesOutAsync(string response, int expectedExitCode, string expectedWriteOut, string expectedErrorLine)
    {
        MemoryStream standardOutput = new();
        EndPointScriptedConnector server = new(new EndPointScriptedConnector.Script(
            new IPEndPoint(IPAddress.Loopback, 50000), new IPEndPoint(IPAddress.Loopback, 47131), Encoding.Latin1.GetBytes(response)));
        string[] arguments = ["-sS", "-w", Variables, Url];
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([
                    new HttpProtocolHandler(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)))])),
                files,
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                timeProvider: new ImmediateTimerTimeProvider())
                .RunAsync(arguments);
        }

        string writtenOut = Encoding.Latin1.GetString(standardOutput.ToArray());
        string errorText = Encoding.UTF8.GetString(standardError.ToArray());
        Diagnostics.Act("stdout", writtenOut);
        Diagnostics.Act("stderr", errorText);
        Assert.AreEqual(expectedExitCode, exitCode);
        Assert.AreEqual(expectedWriteOut, writtenOut);
        Assert.AreEqual(expectedErrorLine + NewLine, errorText);
    }
}
