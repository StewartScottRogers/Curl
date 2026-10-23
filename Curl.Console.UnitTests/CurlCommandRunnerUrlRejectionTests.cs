using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins, through the runner, URLs curl 8.22.0 (mingw, Schannel) rejects before it connects, with the
/// <c>%{url_effective}</c> it prints for them: the URL as it was given, before a scheme was guessed
/// for it. Measured on 2026-10-22 with <c>curl -sS -w '[%{url_effective}][%{url.host}][%{url.port}]'</c>
/// (BL-3173 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerUrlRejectionTests
{
    private const string WriteOut = "[%{url_effective}][%{url.host}][%{url.port}]";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();

    private ScriptedConnector server = new([]);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    [TestMethod]
    [DataRow("//127.0.0.1:18080/echo", "No host present", DisplayName = "two leading slashes")]
    [DataRow("/127.0.0.1:9/", "No host present", DisplayName = "one leading slash")]
    [DataRow("http:127.0.0.1:18080/echo", "Port number was not a decimal number between 0 and 65535", DisplayName = "scheme without slashes")]
    [DataRow(" http://127.0.0.1:9/echo", "Malformed input to a URL function", DisplayName = "leading space")]
    [DataRow("http://127.0.0.1:9/echo ", "Malformed input to a URL function", DisplayName = "trailing space")]
    public async Task RunAsync_RejectedUrl_Exits3AndWritesTheUrlAsGiven(string url, string reason)
    {
        int exitCode = await RunAsync("-sS", "-w", WriteOut, url);

        Diagnostics.Assert("exit code", 3, exitCode);
        Diagnostics.Assert("stderr", "curl: (3) URL rejected: " + reason + NewLine, StandardErrorText);
        Diagnostics.Assert("stdout", "[" + url + "][][]", StandardOutputText);
        Assert.AreEqual(3, exitCode);
        Assert.AreEqual("curl: (3) URL rejected: " + reason + NewLine, StandardErrorText);
        Assert.AreEqual("[" + url + "][][]", StandardOutputText);
        Assert.IsEmpty(server.Targets);
    }

    private async Task<int> RunAsync(params string[] arguments)
    {
        server = new ScriptedConnector([]);
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
        InMemoryFileSystem outputFiles = new();

        Diagnostics.Arrange("command line", string.Join(" ", arguments));
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([http])),
                    outputFiles,
                    outputFiles,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: false)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Bytes("stderr", standardError.ToArray());
        return exitCode;
    }
}
