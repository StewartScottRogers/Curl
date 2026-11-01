using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins curl 8.22.0's refusal of binary output to a terminal (<c>tool_write_cb</c> in <c>src/tool_cb_wrt.c</c>):
/// a body holding a NUL byte written to a terminal standard output is refused with the warning and exit 23 and no
/// <c>curl: (23)</c> line, unless <c>--output -</c> was given. Real curl's release build cannot be shown a terminal
/// through a redirected standard output, so the text is pinned from that source (BL-2908).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerTerminalBinaryOutputTests
{
    private const string Url = "http://127.0.0.1:48495/a";

    private const string Binary = "HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\na\0b";

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_BinaryBodyToTerminal_WarnsAndExits23WithNoErrorLine()
    {
        int exitCode = await RunAsync(standardOutputIsTerminal: true, Url);

        Diagnostics.Assert("exit code", 23, exitCode);
        Diagnostics.Diff("stdout", string.Empty, StandardOutputText);
        Assert.AreEqual(23, exitCode);
        Assert.AreEqual(string.Empty, StandardOutputText);
        Assert.AreEqual(
            "Warning: Binary output can mess up your terminal. Use \"--output -\" to tell \n"
            + "Warning: curl to output it to your terminal anyway, or consider \"--output \n"
            + "Warning: <FILE>\" to save to a file.\n",
            StandardErrorText.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task RunAsync_BinaryBodyToTerminalUnderSilent_ExitsWith23AndWritesNothing()
    {
        int exitCode = await RunAsync(standardOutputIsTerminal: true, "-s", Url);

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_BinaryBodyToTerminalWithOutputDash_WritesTheBody()
    {
        int exitCode = await RunAsync(standardOutputIsTerminal: true, Url, "--output", "-");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "a\0b", StandardOutputText);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("a\0b", StandardOutputText);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_BinaryBodyToRedirectedOutput_WritesTheBody()
    {
        int exitCode = await RunAsync(standardOutputIsTerminal: false, Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("a\0b", StandardOutputText);
    }

    private async Task<int> RunAsync(bool standardOutputIsTerminal, params string[] arguments)
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes(Binary)]);
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("standard output is terminal", standardOutputIsTerminal);
        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                outputFiles,
                outputFiles,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                standardOutputIsTerminal: standardOutputIsTerminal,
                outputPaths: outputFiles)
            .RunAsync(arguments);
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stdout", StandardOutputText);
        Diagnostics.Act("stderr", StandardErrorText);
        return exitCode;
    }
}
